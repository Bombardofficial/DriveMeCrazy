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
        public InputActionProperty steeringSabotageAction;
        public InputActionProperty handbrakeSabotageAction;
        public bool isDriver = true;
        public static event System.Action OnSteeringSabotageTriggered;
        public static event System.Action OnHandbrakeSabotageTriggered;

        private ArcadeVehicleController vehicle;
        bool sabotagesBound;

        public static void FireSteeringSabotage() => OnSteeringSabotageTriggered?.Invoke();
        public static void FireHandbrakeSabotage() => OnHandbrakeSabotageTriggered?.Invoke();

        void Awake()
        {
            vehicle = GetComponent<ArcadeVehicleController>();
        }
        void HandleSteeringSabotage(InputAction.CallbackContext ctx)
        {
            if (isDriver) OnSteeringSabotageTriggered?.Invoke();
        }
        void HandleHandbrakeSabotage(InputAction.CallbackContext ctx)
        {
            if (isDriver) OnHandbrakeSabotageTriggered?.Invoke();
        }
        void OnEnable()
        {
            /* enable all actions as before */
            foreach (var a in new[] { steerAction, throttleAction, brakeAction,
                               slowBrakeAction, steeringSabotageAction,
                               handbrakeSabotageAction })
                a.action.Enable();

            steeringSabotageAction.action.performed += HandleSteeringSabotage;
            handbrakeSabotageAction.action.performed += HandleHandbrakeSabotage;

            sabotagesBound = steeringSabotageAction.action != null
                          && handbrakeSabotageAction.action != null;
            if (!sabotagesBound && isDriver)
                Debug.LogWarning("[InputManager_ArcadeVP] Sabotage keys not bound!");
        }

        void OnDisable()
        {
            steerAction.action.Disable();
            throttleAction.action.Disable();
            brakeAction.action.Disable();
            slowBrakeAction.action.Disable();
            steeringSabotageAction.action.Disable();
            handbrakeSabotageAction.action.Disable();

            steeringSabotageAction.action.performed -= HandleSteeringSabotage;
            handbrakeSabotageAction.action.performed -= HandleHandbrakeSabotage;
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
