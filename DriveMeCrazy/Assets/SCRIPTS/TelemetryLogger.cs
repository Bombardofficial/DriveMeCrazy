using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using ArcadeVP;
using UnityEngine.SceneManagement;

public class TelemetryLogger : MonoBehaviour
{
    public static TelemetryLogger Instance { get; private set; }

    [Header("References")]
    [SerializeField] private ArcadeVehicleController vehicle;
    [SerializeField] private VehicleInputMixer inputMixer;

    [Header("CSV Output")]
    //[SerializeField] private string outputDirectory = @"C:\Users\sagan\Documents\GitHub\DriveMeCrazy\DriveMeCrazy\GameLogging_LM";
    [SerializeField] private string outputDirectory = "";
    [SerializeField] private string filePrefix = "telemetry";

    private readonly List<string> _rows = new();
    private readonly Queue<float> _driverInputTimes = new();

    private float _inputsPerSecond;
    private int _collisionCountTotal;
    private bool _warningActive;
    private bool _curveActive;

    private int _player1CollisionCounter;
    private int _player2CollisionCounter;
    private int _player3CollisionCounter;
    private int _player4CollisionCounter;

    private bool _driverInputThisFrame;

    private bool _isLogging;
    private float _raceStartTime;

    private int _currentDriverPlayerNumber = -1;
    private bool _driverChangedThisFrame;
    private int _driverChangeCountTotal;

    private int _warningObstacleId = -1;
    private int _hitObstacleIdThisFrame = -1;

    private bool _driftCheckActive;
    private bool _driftCheckResult;

    public float CurrentRaceTime => _isLogging ? Time.time - _raceStartTime : 0f;

    private void Awake()
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            outputDirectory = Path.Combine(Application.persistentDataPath, "MasterthesisLogs");
        }

        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    // Füge das am Ende von Update() hinzu, um jeden Frame zu loggen
    private void LateUpdate()
    {
        if (!_isLogging) return;

        while (_driverInputTimes.Count > 0 && Time.time - _driverInputTimes.Peek() > 1f)
        {
            _driverInputTimes.Dequeue();
        }
        _inputsPerSecond = _driverInputTimes.Count;

        // Erstelle eine Zeile mit allen aktuellen Werten
        string row = $"{Time.time.ToString(CultureInfo.InvariantCulture)}," +
                     $"{_currentDriverPlayerNumber}," +
                     $"{(_driverChangedThisFrame ? 1 : 0)}," +
                     $"{_driverChangeCountTotal}," +
                     $"{(_curveActive ? 1 : 0)}," +
                     $"{(_driftCheckActive ? 1 : 0)}," +
                     $"{(_driftCheckResult ? 1 : 0)}," +
                     $"{(_warningActive ? 1 : 0)}," +
                     $"{_warningObstacleId}," +
                     $"{(_driverInputThisFrame ? 1 : 0)}," +
                     $"{_inputsPerSecond.ToString("F4", CultureInfo.InvariantCulture)}," +
                     $"{_hitObstacleIdThisFrame}," +
                     $"{_player1CollisionCounter},{_player2CollisionCounter}," +
                     $"{_player3CollisionCounter},{_player4CollisionCounter}," +
                     $"{_collisionCountTotal}";

        _rows.Add(row);

        // WICHTIG: Setze Event-Flags nach jedem Frame zurück, damit sie nicht "kleben"
        _driverInputThisFrame = false;
        _driverChangedThisFrame = false;
        _driftCheckResult = false;
        _hitObstacleIdThisFrame = -1; 
    }

    public void StartLogging()
    {
        _rows.Clear();

        _raceStartTime = Time.time;
        _isLogging = true;

        _currentDriverPlayerNumber = -1;
        _driverChangedThisFrame = false;
        _driverChangeCountTotal = 0;
        _curveActive = false;
        _driftCheckActive = false;
        _driftCheckResult = false;
        _warningActive = false;
        _warningObstacleId = -1;
        _driverInputTimes.Clear();
        _driverInputThisFrame = false;
        _inputsPerSecond = 0f;
        _hitObstacleIdThisFrame = -1;
        _player1CollisionCounter = 0;
        _player2CollisionCounter = 0;
        _player3CollisionCounter = 0;
        _player4CollisionCounter = 0;
        _collisionCountTotal = 0;

        if (PlayerManager.Instance != null && PlayerManager.Instance.CurrentDriver != null)
        {
            _currentDriverPlayerNumber = PlayerManager.Instance.CurrentDriver.PlayerNumber;
        }

        _rows.Add("run_time,current_driver_player_number,driver_changed_this_frame,driver_change_count_total,curve_active,drift_check_active,drift_check_result,warning_active,warning_obstacle_id,driver_input_this_frame,inputs_per_second,hit_obstacle_id_this_frame,player1_collision_counter,player2_collision_counter,player3_collision_counter,player4_collision_counter,collision_count_total");
    }

    public void StopLogging()
    {
        Debug.Log("[TelemetryLogger] StopLogging called");

        if (!_isLogging)
            return;

        _isLogging = false;
        SaveCsv();
    }

    private void OnApplicationQuit()
    {
        StopLogging();
    }

    public void RegisterDriverChange(int newDriverPlayerNumber)
    {
        if (!_isLogging)
            return;

        _currentDriverPlayerNumber = newDriverPlayerNumber;
        _driverChangedThisFrame = true;
        _driverChangeCountTotal++;
    }

    
    public void SetWarningActive(bool active, int obstacleId)
    {
        if (!_isLogging)
            return;

        _warningActive = active;
        _warningObstacleId = active ? obstacleId : -1;
    }

    public void RegisterDriverInputThisFrame(bool countsAsWarningReaction)
    {
        if (!_isLogging)
            return;

        _driverInputTimes.Enqueue(Time.time);
        _driverInputThisFrame = true;
    }

    private void SaveCsv()
    {
        try
        {
            if (!Directory.Exists(outputDirectory))
                Directory.CreateDirectory(outputDirectory);

            string sceneName = SceneManager.GetActiveScene().name;
            string fileName = $"{filePrefix}_{sceneName}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            string fullPath = Path.Combine(outputDirectory, fileName);

            File.WriteAllText(fullPath, string.Join("\n", _rows), Encoding.UTF8);
            Debug.Log($"Telemetry CSV saved: {fullPath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to save telemetry CSV: {ex.Message}");
        }
    }

    public void RegisterObstacleHit(int obstacleId)
    {
        if (!_isLogging)
            return;

        _hitObstacleIdThisFrame = obstacleId;
    }

    private void OnEnable()
    {
        PlayerManager.OnDriverChanged += HandleDriverChanged;
    }

    private void OnDisable()
    {
        PlayerManager.OnDriverChanged -= HandleDriverChanged;
    }

    private void HandleDriverChanged(Passenger oldDriver, Passenger newDriver)
    {
        int playerNumber = newDriver != null ? newDriver.PlayerNumber : -1;
        RegisterDriverChange(playerNumber);
    }

    public void RegisterCollisionForCurrentDriver()
    {
        if (!_isLogging)
            return;

        switch (_currentDriverPlayerNumber)
        {
            case 1:
                _player1CollisionCounter++;
                break;
            case 2:
                _player2CollisionCounter++;
                break;
            case 3:
                _player3CollisionCounter++;
                break;
            case 4:
                _player4CollisionCounter++;
                break;
        }

        _collisionCountTotal++;
    }

    public void SetCurveActive(bool active)
    {
        if (!_isLogging)
            return;

        _curveActive = active;
    }

    public void SetDriftCheckActive(bool active)
    {
        if (!_isLogging)
            return;

        _driftCheckActive = active;

        if (active)
            _driftCheckResult = false;
    }

    public void RegisterDriftCheckResult(bool success)
    {
        if (!_isLogging)
            return;

        if (success)
            _driftCheckResult = true;
    }

}