using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Periodikusan logolja a skill & difficulty állapotokat CSV-be.
/// Egy sor = egy idõpillanat (frame-csökkentett), globális + aktuális driver adatokkal.
/// </summary>
public class SkillTelemetryLogger : MonoBehaviour
{
    [Header("Logging frequency")]
    [Tooltip("Seconds between log rows.")]
    public float logInterval = 0.5f;

    [Header("File naming")]
    [Tooltip("Prefix for the CSV file name.")]
    public string filePrefix = "SkillLog_";

    [Header("Behaviour")]
    [Tooltip("Start logging automatically when the race starts.")]
    public bool autoStartWhenRaceStarts = true;

    [Tooltip("If false, logging only happens while a race is running.")]
    public bool logWhileNotRacing = false;

    [Header("Editor-only override")]
    [Tooltip("If true (and in Editor), logs go to this directory instead of persistentDataPath.")]
    public bool useCustomDirectoryInEditor = true;

    [Tooltip("Relative or absolute path. Example: Assets/SCRIPTS/SkillBasedAISystem/TelemetryLogs")]
    public string editorDirectory = "Assets/SCRIPTS/SkillBasedAISystem/TelemetryLogs";

    string _filePath;
    StreamWriter _writer;
    float _nextLogTime;
    bool _headerWritten;
    bool _raceWasRunning;

    void OnEnable()
    {
        _nextLogTime = Time.time + logInterval;
    }

    void OnDisable()
    {
        CloseWriter();
    }

    void Update()
    {
        bool raceRunning = PlayerJoinManager.IsRaceStarted;

        // Race indulásakor nyitjuk a fájlt (ha kértük)
        if (autoStartWhenRaceStarts && raceRunning && !_raceWasRunning)
        {
            EnsureWriter();
        }

        _raceWasRunning = raceRunning;

        if (!logWhileNotRacing && !raceRunning)
            return;

        if (Time.time < _nextLogTime)
            return;

        _nextLogTime = Time.time + logInterval;

        if (_writer == null)
            EnsureWriter();

        if (_writer == null)
            return;

        LogFrame();
    }

    void EnsureWriter()
    {
        if (_writer != null) return;

        string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string fileName = filePrefix + timestamp + ".csv";

        string dirPath;

#if UNITY_EDITOR
        if (useCustomDirectoryInEditor && !string.IsNullOrEmpty(editorDirectory))
        {
            // Ha relatív path, akkor a projekt gyökeréhez képest értelmezzük.
            if (!Path.IsPathRooted(editorDirectory))
            {
                // Application.dataPath = ...\YourProject\Assets
                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                dirPath = Path.Combine(projectRoot, editorDirectory);
            }
            else
            {
                dirPath = editorDirectory;
            }
        }
        else
        {
            dirPath = Application.persistentDataPath;
        }
#else
        // Buildben sose írjunk az Assetsbe, maradjon a persistentDataPath
        dirPath = Application.persistentDataPath;
#endif

        try
        {
            Directory.CreateDirectory(dirPath);
            _filePath = Path.Combine(dirPath, fileName);
            _writer = new StreamWriter(_filePath, false, Encoding.UTF8);
            _headerWritten = false;
            Debug.Log("[SkillTelemetryLogger] Logging to: " + _filePath);
        }
        catch (System.Exception ex)
        {
            Debug.LogError("[SkillTelemetryLogger] Could not open log file: " + ex.Message);
            _writer = null;
        }
    }

    void CloseWriter()
    {
        if (_writer != null)
        {
            _writer.Flush();
            _writer.Close();
            _writer.Dispose();
            _writer = null;
            Debug.Log("[SkillTelemetryLogger] Log closed: " + _filePath);
        }
    }

    void LogFrame()
    {
        var est = SkillEstimator.Instance;
        var dir = DifficultyDirector.Instance;
        var pm = PlayerManager.Instance;

        if (est == null || dir == null)
            return;

        // Global difficulty (skill-átlag)
        DifficultyState global = dir.GlobalCurrent;
        var globalProf = est.Global;

        // Driver-specifikus difficulty (multi-player aware directorbõl)
        DifficultyState current = dir.Current;
        var activeProf = est.Active;
        var activePassenger = est.ActivePassenger;

        int driverNum = activePassenger ? activePassenger.PlayerNumber : -1;
        int driverPoints = (pm != null && pm.CurrentDriver != null) ? pm.CurrentDriver.Points : 0;

        float gSkill = (globalProf != null) ? globalProf.Skill01 : 0.5f;
        float gConf = (globalProf != null) ? globalProf.Confidence : 0f;

        float aSkill = (activeProf != null) ? activeProf.Skill01 : 0.5f;
        float aConf = (activeProf != null) ? activeProf.Confidence : 0f;

        float aColSucc = (activeProf != null) ? activeProf.CollectSuccessRate : 0f;
        float aColMiss = (activeProf != null) ? activeProf.CollectMissRate : 0f;
        float aHitRate = (activeProf != null) ? activeProf.ObstacleHitRate : 0f;

        float aMgPass = (activeProf != null) ? activeProf.MiniGamePassRate : 0f;
        float aMgPerf = (activeProf != null) ? activeProf.MiniGamePerfectRate : 0f;
        float aMgFail = (activeProf != null) ? activeProf.MiniGameFailRate : 0f;

        if (!_headerWritten)
        {
            _writer.WriteLine(
                "time,driverPlayerNum,driverPoints," +
                "globalSkill,globalConf,globalOverall,globalSpawn,globalPrecision,globalPunishment,globalRewardBias," +
                "driverSkill,driverConf,driverOverall,driverSpawn,driverPrecision,driverPunishment,driverRewardBias," +
                "driverCollectSuccess,driverCollectMiss,driverObstacleHit," +
                "driverMgPass,driverMgPerfect,driverMgFail");

            _headerWritten = true;
        }

        float t = Time.time;

        _writer.WriteLine(string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            "{0:F3},{1},{2}," +                                // time, driverPlayerNum, driverPoints
            "{3:F4},{4:F4},{5:F4},{6:F4},{7:F4},{8:F4},{9:F4}," + // global*
            "{10:F4},{11:F4},{12:F4},{13:F4},{14:F4},{15:F4},{16:F4}," + // driver difficulty*
            "{17:F4},{18:F4},{19:F4}," +                      // driver collect/obstacle rates
            "{20:F4},{21:F4},{22:F4}",                        // driver mini-game rates
            t, driverNum, driverPoints,
            gSkill, gConf, global.overall, global.spawnPressure, global.precision, global.punishment, global.rewardBias,
            aSkill, aConf, current.overall, current.spawnPressure, current.precision, current.punishment, current.rewardBias,
            aColSucc, aColMiss, aHitRate,
            aMgPass, aMgPerf, aMgFail));

        _writer.Flush();
    }
}
