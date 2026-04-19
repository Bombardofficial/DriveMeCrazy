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

    private int _player1CollisionCounter;
    private int _player2CollisionCounter;
    private int _player3CollisionCounter;
    private int _player4CollisionCounter;

    /*
    private float _warningReactionTime = -1f;
    private float _warningStartTime = -1f;
    private bool _waitingForWarningReaction;
    */

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
                     $"{(_warningActive ? 1 : 0)}," +
                     $"{(_driverInputThisFrame ? 1 : 0)}," +
                     //$"{_warningReactionTime.ToString("F4", CultureInfo.InvariantCulture)}," +
                     $"{_inputsPerSecond.ToString("F4", CultureInfo.InvariantCulture)}," +
                     $"{_player1CollisionCounter},{_player2CollisionCounter}," +
                     $"{_player3CollisionCounter},{_player4CollisionCounter}," +
                     $"{_collisionCountTotal}";

        _rows.Add(row);

        // WICHTIG: Setze Event-Flags nach jedem Frame zurück, damit sie nicht "kleben"
        _driverInputThisFrame = false;
        _driverChangedThisFrame = false;
    }

    /*
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
            _player1CollisionCounter,
            _player2CollisionCounter,
            _player3CollisionCounter,
            _player4CollisionCounter,
            _collisionCountTotal
        ));

        // frame flags nach dem Schreiben zurücksetzen
        _driverChangedThisFrame = false;
        _driverInputThisFrame = false;
    }
    */

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
        //_warningReactionTime = -1f;
        //_warningStartTime = -1f;
        //_waitingForWarningReaction = false;
        _driverInputTimes.Clear();
        _inputsPerSecond = 0f;
        _player1CollisionCounter = 0;
        _player2CollisionCounter = 0;
        _player3CollisionCounter = 0;
        _player4CollisionCounter = 0;
        _collisionCountTotal = 0;

        if (PlayerManager.Instance != null && PlayerManager.Instance.CurrentDriver != null)
        {
            _currentDriverPlayerNumber = PlayerManager.Instance.CurrentDriver.PlayerNumber;
        }

        _rows.Add("run_time,current_driver_player_number,driver_changed_this_frame,driver_change_count_total,warning_active,driver_input_this_frame,inputs_per_second,player1_collision_counter,player2_collision_counter,player3_collision_counter,player4_collision_counter,collision_count_total");
        //_rows.Add("run_time,current_driver_player_number,driver_changed_this_frame,driver_change_count_total,warning_active,driver_input_this_frame,warning_reaction_time,inputs_per_second,player1_collision_counter,player2_collision_counter,player3_collision_counter,player4_collision_counter,collision_count_total");
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

    
    public void SetWarningActive(bool active)
    {
        if (!_isLogging)
            return;

        _warningActive = active; 

        /*
        // Warnung gestartet
        if (active)
        {
            _warningStartTime = Time.time;
            //_warningReactionTime = -1f;
            _waitingForWarningReaction = true;
        }
        else // Warnung beendet
        {
            if (_waitingForWarningReaction)
                _warningReactionTime = -1f;
            _waitingForWarningReaction = false;
        }
        */
    }

    public void RegisterDriverInputThisFrame(bool countsAsWarningReaction)
    {
        if (!_isLogging)
            return;

        _driverInputTimes.Enqueue(Time.time);
        _driverInputThisFrame = true;

        /*
        if (_warningActive && _waitingForWarningReaction && countsAsWarningReaction)
        {
            _warningReactionTime = Time.time - _warningStartTime;
            _waitingForWarningReaction = false;
        }
        */
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

    /*
    public void TryRegisterWarningReaction()
    {
        if (_isLogging  && _waitingForWarningReaction)
        {
            _warningReactionTime = Time.time - _warningStartTime;
            _waitingForWarningReaction = false; // Reaction wurde erfasst
        }
    }
    */
    
}