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
