using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using ArcadeVP;

public class TelemetryLogger : MonoBehaviour
{
    public static TelemetryLogger Instance { get; private set; }

    [Header("References")]
    [SerializeField] private ArcadeVehicleController vehicle;
    [SerializeField] private VehicleInputMixer inputMixer;

    [Header("CSV Output")]
    //[SerializeField] private string outputDirectory = @"C:\Users\sagan\Documents\Ausbildung\FH-Technikum\MAI\Master_Arbeit\GameLogs";
    [SerializeField] private string outputDirectory = @"C:\Users\sagan\Documents\GitHub\DriveMeCrazy\DriveMeCrazy\GameLogging_LM";
    [SerializeField] private string filePrefix = "telemetry";

    private readonly List<string> _rows = new();
    private readonly Queue<float> _driverInputTimes = new();

    private float _inputsPerSecond;

    private int _collisionCountTotal;

    private bool _warningActive;
    private int _activeWarningObstacleId = -1;
    private bool _warningOutcomePending;
    private string _warningObstacleResult = "";

    private float _warningReactionTime = -1f;
    private float _warningStartTime = -1f;
    private bool _waitingForWarningReaction;

    private bool _driverInputThisFrame;

    private bool _isLogging;
    private float _raceStartTime;

    private int _currentDriverPlayerNumber = -1;
    private bool _driverChangedThisFrame;
    private int _driverChangeCountTotal;

    

    public float CurrentRaceTime => _isLogging ? Time.time - _raceStartTime : 0f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void LateUpdate()
    {
        if (!_isLogging)
            return;

        while (_driverInputTimes.Count > 0 && Time.time - _driverInputTimes.Peek() > 1f)
        {
            _driverInputTimes.Dequeue();
        }

        _inputsPerSecond = _driverInputTimes.Count;

        float runTime = Time.time - _raceStartTime;

        _rows.Add(string.Join(",",
            runTime.ToString("F4", CultureInfo.InvariantCulture),
            _currentDriverPlayerNumber,
            _driverChangedThisFrame ? 1 : 0,
            _driverChangeCountTotal,
            _warningActive ? 1 : 0,
            _driverInputThisFrame ? 1 : 0,
            _warningReactionTime.ToString("F4", CultureInfo.InvariantCulture),
            _inputsPerSecond.ToString("F4", CultureInfo.InvariantCulture),
            Escape(_warningObstacleResult),
            _collisionCountTotal
        ));

        // frame flags nach dem Schreiben zurücksetzen
        _driverChangedThisFrame = false;
        _driverInputThisFrame = false;
    }

    public void StartLogging()
    {
        _rows.Clear();

        _raceStartTime = Time.time;
        _isLogging = true;

        _currentDriverPlayerNumber = -1;
        _driverChangedThisFrame = false;
        _driverChangeCountTotal = 0;
        _warningActive = false;
        _driverInputThisFrame = false;
        _warningReactionTime = -1f;
        _warningStartTime = -1f;
        _waitingForWarningReaction = false;
        _driverInputTimes.Clear();
        _inputsPerSecond = 0f;
        _collisionCountTotal = 0;
        _activeWarningObstacleId = -1;
        _warningOutcomePending = false;
        _warningObstacleResult = "";

        _rows.Add("run_time,current_driver_player_number,driver_changed_this_frame,driver_change_count_total,warning_active,driver_input_this_frame,warning_reaction_time,inputs_per_second,warning_obstacle_result,collision_count_total");
    }

    public void StopLogging()
    {
        Debug.Log("[TelemetryLogger] StopLogging called");

        if (!_isLogging)
            return;

        _isLogging = false;
        SaveCsv();
    }

    public void RegisterDriverChange(int newDriverPlayerNumber)
    {
        if (!_isLogging)
            return;

        _currentDriverPlayerNumber = newDriverPlayerNumber;
        _driverChangedThisFrame = true;
        _driverChangeCountTotal++;
    }

    /*
    public void SetWarningActive(bool active)
    {
        if (!_isLogging)
            return;

        // Warnung startet neu
        if (active && !_warningActive)
        {
            _warningStartTime = Time.time;
            _warningReactionTime = -1f;
            _waitingForWarningReaction = true;
        }

        // Warnung endet
        if (!active && _warningActive)
        {
            _waitingForWarningReaction = false;
        }

        _warningActive = active;

    }
    */

    public void RegisterDriverInputThisFrame()
    {
        if (!_isLogging)
            return;

        _driverInputTimes.Enqueue(Time.time);
        _driverInputThisFrame = true;

        if (_warningActive && _waitingForWarningReaction)
        {
            _warningReactionTime = Time.time - _warningStartTime;
            _waitingForWarningReaction = false;
        }
    }

    private void SaveCsv()
    {
        try
        {
            if (!Directory.Exists(outputDirectory))
                Directory.CreateDirectory(outputDirectory);

            string fileName = $"{filePrefix}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            string fullPath = Path.Combine(outputDirectory, fileName);

            File.WriteAllText(fullPath, string.Join("\n", _rows), Encoding.UTF8);
            Debug.Log($"Telemetry CSV saved: {fullPath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to save telemetry CSV: {ex.Message}");
        }
    }

    private void OnEnable()
    {
        PlayerManager.OnDriverChanged += HandleDriverChanged;

        if (vehicle != null)
            vehicle.LaneChanged += HandleLaneChanged;
    }

    private void OnDisable()
    {
        PlayerManager.OnDriverChanged -= HandleDriverChanged;

        if (vehicle != null)
            vehicle.LaneChanged -= HandleLaneChanged;
    }

    private void HandleLaneChanged()
    {
        RegisterDriverInputThisFrame();
    }

    private void HandleDriverChanged(Passenger oldDriver, Passenger newDriver)
    {
        int playerNumber = newDriver != null ? newDriver.PlayerNumber : -1;
        RegisterDriverChange(playerNumber);
    }

    private static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    public void RegisterWarningStartedForObstacle(int obstacleId)
    {
        if (!_isLogging)
            return;

        if (_warningActive && _activeWarningObstacleId == obstacleId)
            return;

        _warningActive = true;

        _activeWarningObstacleId = obstacleId;
        _warningOutcomePending = true;
        _warningObstacleResult = "";

        _warningStartTime = Time.time;
        _warningReactionTime = -1f;
        _waitingForWarningReaction = true;
    }

    public void RegisterCollision(int obstacleId)
    {
        if (!_isLogging)
            return;

        _collisionCountTotal++;

        if (_warningOutcomePending && obstacleId == _activeWarningObstacleId)
        {
            _warningObstacleResult = "hit";
            _warningOutcomePending = false;

            _warningActive = false;
            _waitingForWarningReaction = false;
            _activeWarningObstacleId = -1;
        }
    }

    public void RegisterWarningObstacleAvoided(int obstacleId)
    {
        if (!_isLogging)
            return;

        if (_warningOutcomePending && obstacleId == _activeWarningObstacleId)
        {
            _warningObstacleResult = "avoided";
            _warningOutcomePending = false;
        }
    }
}