using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public class CarDriverInput : MonoBehaviour
{
    [HideInInspector] public ArcadeVP.ArcadeVehicleController vehicle;

    // REMOVE this line: public VehicleControls controls;

    // ADD these private fields for the actions
    private PlayerInput _playerInput;
    private InputAction _steerAction;
    private InputAction _throttleAction;
    private InputAction _brakeAction;
    private InputAction _slowBrakeAction;

    private bool _wasThrottlePressed;
    private bool _wasBrakePressed;
    private float _lastSteer;

    [SerializeField] private float steerThreshold = 0.6f;

    void Awake()
    {
        // REPLACE your old Awake logic with this
        _playerInput = GetComponent<PlayerInput>();
        _steerAction = _playerInput.actions["Steer"];
        _throttleAction = _playerInput.actions["Throttle"];
        _brakeAction = _playerInput.actions["Brake"];
        _slowBrakeAction = _playerInput.actions["SlowBrake"];
    }

    void Update()
    {
        if (!PassengerIsDriver) return;

        // MODIFY these lines to read from the action fields
        float steer = _steerAction.ReadValue<float>();
        float gas = _throttleAction.ReadValue<float>();
        float brake = _brakeAction.ReadValue<float>();
        float slow = _slowBrakeAction.ReadValue<float>();

        bool throttlePressedNow = gas > 0.1f;
        bool brakePressedNow = brake > 0.1f || slow > 0.1f;

        bool throttlePressEvent = throttlePressedNow && !_wasThrottlePressed;
        bool brakePressEvent = brakePressedNow && !_wasBrakePressed;

        // deutlicher Lenkeinschlag als Event
        bool strongSteerEvent = Mathf.Abs(steer) >= steerThreshold && Mathf.Abs(_lastSteer) < steerThreshold;

        // aktuell gültige Reaktionshaltung
        //bool validReactionHeld = brakePressedNow || Mathf.Abs(steer) >= steerThreshold;


        // Logging Input per second (?)
        if (brakePressEvent || strongSteerEvent)
        {
            TelemetryLogger.Instance?.RegisterDriverInputThisFrame(true);
        }
        else if (throttlePressEvent)
        {
            TelemetryLogger.Instance?.RegisterDriverInputThisFrame(false);
        }

        /*
        // 2) warning_reaction_time auch dann setzen, wenn Bremse/Lenkung bereits gehalten wird
        if (validReactionHeld)
        {
            TelemetryLogger.Instance?.TryRegisterWarningReaction();
        }
        */

        _wasThrottlePressed = throttlePressedNow;
        _wasBrakePressed = brakePressedNow;
        _lastSteer = steer;

        if (ArcadeVP.VehicleInputMixer.Instance)
        {
            ArcadeVP.VehicleInputMixer.SetDriverInputs(steer, gas, brake, slow);
        }
        else if (vehicle)
        {
            vehicle.ProvideInputs(steer, gas, brake, slow);
        }
    }

    /* called by PlayerJoinManager after spawn */
    public void SetVehicle(ArcadeVP.ArcadeVehicleController v)
    {
        vehicle = v;

        /* auto-add mixer if designer forgot it */
        if (v && !v.GetComponent<ArcadeVP.VehicleInputMixer>())
            v.gameObject.AddComponent<ArcadeVP.VehicleInputMixer>();
    }

    bool PassengerIsDriver =>
        PlayerManager.Instance &&
        PlayerManager.Instance.CurrentDriver == GetComponent<Passenger>();
}
