using UnityEngine;
using UnityEngine.InputSystem;

namespace ArcadeVP
{
    [RequireComponent(typeof(ArcadeVehicleController))]
    public class InputManager_ArcadeVP : MonoBehaviour
    {
        public InputActionProperty steerAction;
        public InputActionProperty throttleAction;
        public InputActionProperty brakeAction;       // drift?brake (Space/Shift)
        public InputActionProperty slowBrakeAction;   // gentle brake (S/Down)

        private ArcadeVehicleController vehicle;

        void Awake()
        {
            vehicle = GetComponent<ArcadeVehicleController>();
        }

        void OnEnable()
        {
            steerAction.action.Enable();
            throttleAction.action.Enable();
            brakeAction.action.Enable();
            slowBrakeAction.action.Enable();
        }

        void OnDisable()
        {
            steerAction.action.Disable();
            throttleAction.action.Disable();
            brakeAction.action.Disable();
            slowBrakeAction.action.Disable();
        }

        void Update()
        {
            float steer = steerAction.action.ReadValue<float>();
            float accel = throttleAction.action.ReadValue<float>();
            float driftB = brakeAction.action.ReadValue<float>();
            float slowB = slowBrakeAction.action.ReadValue<float>();
            vehicle.ProvideInputs(steer, accel, driftB, slowB);
        }
    }
}
