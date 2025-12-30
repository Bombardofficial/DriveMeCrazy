using UnityEngine;
using ArcadeVP;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Passenger))]
public class PassengerCarAnimator : MonoBehaviour
{
    [Header("Animator Params")]
    public string isDrivingBool = "IsDriving"; // optional (csak ha létezik az Animatorban)
    public string steerLeftTrigger = "SteerLeft";
    public string steerLeftBackTrigger = "SteerLeftBack";
    public string steerRightTrigger = "SteerRight";
    public string steerRightBackTrigger = "SteerRightBack";
    public string crashTrigger = "Crash";

    [Header("State Names (optional hard reset)")]
    public string drivingStateName = "Driving";

    [Header("Rules")]
    public bool onlyDriverSteers = true;

    Animator _anim;
    Passenger _passenger;
    ArcadeVehicleController _car;

    bool _isDriver;
    bool _wasChanging;
    int _lastDir; // a kocsiból jön (nálad: + = BAL, - = JOBB)
    bool _hasIsDrivingParam;
    float _ignoreSteerUntil;

    void Awake()
    {
        _anim = GetComponent<Animator>();
        _passenger = GetComponent<Passenger>();
        _hasIsDrivingParam = HasBoolParam(isDrivingBool);
    }

    void OnEnable()
    {
        PlayerManager.OnDriverChanged += HandleDriverChanged;
        ResolveCarAndSubscribe();
        UpdateDriverFlag();
    }

    void OnDisable()
    {
        PlayerManager.OnDriverChanged -= HandleDriverChanged;
        UnsubscribeCar();
    }

    void Update()
    {
        if (!_anim) return;
        if (!_car) ResolveCarAndSubscribe();

        if (_hasIsDrivingParam)
        {
            bool started = PlayerJoinManager.IsRaceStarted;
            _anim.SetBool(isDrivingBool, started);
        }

        if (Time.time < _ignoreSteerUntil) return;

        bool steerAllowed = !onlyDriverSteers || _isDriver;
        if (!steerAllowed || !_car)
        {
            _wasChanging = false;
            _lastDir = 0;
            return;
        }

        bool changing = _car.IsChangingLane;

        // Lane change START -> Steering trigger
        if (changing && !_wasChanging)
        {
            int dir = _car.LaneChangeDirection; // NÁLAD: +1 = BAL, -1 = JOBB
            _lastDir = dir;

            // >>> ITT VAN A CSERE <<<
            if (dir > 0) _anim.SetTrigger(steerLeftTrigger);       // + => bal
            else if (dir < 0) _anim.SetTrigger(steerRightTrigger); // - => jobb
        }
        // Lane change END -> Back trigger
        else if (!changing && _wasChanging)
        {
            // >>> ÉS ITT IS UGYANAZ A LOGIKA <<<
            if (_lastDir > 0) _anim.SetTrigger(steerLeftBackTrigger);       // + => balról vissza
            else if (_lastDir < 0) _anim.SetTrigger(steerRightBackTrigger); // - => jobbról vissza

            _lastDir = 0;
        }

        _wasChanging = changing;
    }

    void HandleDriverChanged(Passenger oldDriver, Passenger newDriver)
    {
        UpdateDriverFlag();

        if (onlyDriverSteers && !_isDriver)
        {
            _wasChanging = false;
            _lastDir = 0;
            ResetSteerTriggers();

            if (!string.IsNullOrEmpty(drivingStateName))
                _anim.CrossFadeInFixedTime(drivingStateName, 0.05f);
        }
    }

    void UpdateDriverFlag()
    {
        _isDriver = (PlayerManager.Instance && PlayerManager.Instance.CurrentDriver == _passenger);
    }

    void ResolveCarAndSubscribe()
    {
        ArcadeVehicleController candidate = null;

        var driverInput = GetComponent<CarDriverInput>();
        if (driverInput != null && driverInput.vehicle != null)
            candidate = driverInput.vehicle;

        if (candidate == null && VehicleInputMixer.Instance != null && VehicleInputMixer.Instance.Car != null)
            candidate = VehicleInputMixer.Instance.Car;

        if (candidate == null)
            candidate = FindObjectOfType<ArcadeVehicleController>();

        if (candidate == null) return;
        if (_car == candidate) return;

        UnsubscribeCar();
        _car = candidate;
        _car.CrashHappened += OnCarCrash;
    }

    void UnsubscribeCar()
    {
        if (_car != null) _car.CrashHappened -= OnCarCrash;
        _car = null;
    }

    void OnCarCrash()
    {
        if (!_anim) return;

        _ignoreSteerUntil = Time.time + 0.25f;
        _wasChanging = false;
        _lastDir = 0;

        ResetSteerTriggers();
        _anim.SetTrigger(crashTrigger);
    }

    void ResetSteerTriggers()
    {
        _anim.ResetTrigger(steerLeftTrigger);
        _anim.ResetTrigger(steerLeftBackTrigger);
        _anim.ResetTrigger(steerRightTrigger);
        _anim.ResetTrigger(steerRightBackTrigger);
    }

    bool HasBoolParam(string paramName)
    {
        if (_anim == null || string.IsNullOrEmpty(paramName)) return false;
        foreach (var p in _anim.parameters)
            if (p.type == AnimatorControllerParameterType.Bool && p.name == paramName)
                return true;
        return false;
    }
}
