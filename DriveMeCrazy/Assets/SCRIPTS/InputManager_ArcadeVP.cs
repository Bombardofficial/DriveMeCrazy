using UnityEngine;
using UnityEngine.InputSystem;

namespace ArcadeVP
{
    [RequireComponent(typeof(ArcadeVehicleController))]
    public class InputManager_ArcadeVP : MonoBehaviour
    {
        public InputActionProperty steerAction;
        public InputActionProperty throttleAction;
        public InputActionProperty brakeAction;
        public InputActionProperty slowBrakeAction;
        public bool isDriver = true;
        public static event System.Action<Passenger> OnSteeringSabotageTriggered;
        public static event System.Action<Passenger> OnHandbrakeSabotageTriggered;
        public static event System.Action<Passenger> OnLeftSteerSabotageTriggered;
        public static event System.Action<Passenger> OnRightSteerSabotageTriggered;
        public static event System.Action<Passenger> OnBlockBrakeSabotageTriggered;
        public static event System.Action<Passenger> OnBlockGasSabotageTriggered;
        public static event System.Action<Passenger> OnBlockSteeringSabotageTriggered;
        public static event System.Action<Passenger> OnPressGasSabotageTriggered;

        private ArcadeVehicleController vehicle;
        bool sabotagesBound;

        public static void FireSteeringSabotage(Passenger passenger) => OnSteeringSabotageTriggered?.Invoke(passenger);
        public static void FireHandbrakeSabotage(Passenger passenger) => OnHandbrakeSabotageTriggered?.Invoke(passenger);
        public static void FireLeftSteerSabotage(Passenger passenger) => OnLeftSteerSabotageTriggered?.Invoke(passenger);
        public static void FireRightSteerSabotage(Passenger passenger) => OnRightSteerSabotageTriggered?.Invoke(passenger);
        public static void FireBlockBrakeSabotage(Passenger passenger) => OnBlockBrakeSabotageTriggered?.Invoke(passenger);
        public static void FireBlockGasSabotage(Passenger passenger) => OnBlockGasSabotageTriggered?.Invoke(passenger);
        public static void FireBlockSteeringSabotage(Passenger passenger) => OnBlockSteeringSabotageTriggered?.Invoke(passenger);
        public static void FirePressGasSabotage(Passenger passenger) => OnPressGasSabotageTriggered?.Invoke(passenger);

        void Awake()
        {
            vehicle = GetComponent<ArcadeVehicleController>();
        }
        void OnEnable()
        {
            /* enable all actions as before */
            foreach (var a in new[] { steerAction, throttleAction, brakeAction,
                               slowBrakeAction})
                a.action.Enable();
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
