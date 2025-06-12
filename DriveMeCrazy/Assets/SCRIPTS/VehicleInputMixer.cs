using UnityEngine;

namespace ArcadeVP
{
    /// <summary>
    ///  Collects inputs from the driver plus any active sabotages and
    ///  forwards the final mix to <see cref="ArcadeVehicleController"/>.
    /// </summary>
    [RequireComponent(typeof(ArcadeVehicleController))]
    public class VehicleInputMixer : MonoBehaviour
    {
        public static VehicleInputMixer Instance { get; private set; }

        /* data fed by the real driver --------------------------------------- */
        float _steer, _gas, _brake, _slow;

        /* additive impulses (sabotages) ------------------------------------- */
        float _addSteer, _addSlow;

        ArcadeVehicleController _car;

        void Awake()
        {
            if (Instance && Instance != this)
            {
                Debug.LogError("[VehicleInputMixer] More than one in scene!");
                Destroy(this); return;
            }
            Instance = this;
            _car = GetComponent<ArcadeVehicleController>();
        }
        void OnEnable()
        {
            PlayerManager.OnDriverChanged += HandleDriverChange;
        }

        // Unsubscribe when disabled or destroyed to prevent errors.
        void OnDisable()
        {
            PlayerManager.OnDriverChanged -= HandleDriverChange;
        }
        void LateUpdate()
        {
            // 1. compose
            float steer = Mathf.Clamp(_steer + _addSteer, -1, 1);
            float gas = Mathf.Clamp01(_gas);
            float brake = Mathf.Clamp01(_brake);
            float slow = Mathf.Clamp01(_slow + _addSlow);

            // 2. feed car
            _car.ProvideInputs(steer, gas, brake, slow);

            // 3. reset additive slots – sabotages must set them every frame
            _addSteer = 0;
            _addSlow = 0;
        }

        private void HandleDriverChange(Passenger oldDriver, Passenger newDriver)
        {
            _steer = 0f;
            _gas = 0f;
            _brake = 0f;
            _slow = 0f;
        }

        /* ------------------------------------------------------------------ */
        #region  API  (called by driver or sabotage scripts)

        public static void SetDriverInputs(float steer, float gas, float brake, float slow)
        {
            if (!Instance) return;
            Instance._steer = steer;
            Instance._gas = gas;
            Instance._brake = brake;
            Instance._slow = slow;
        }

        public static void AddSteer(float value)
        {
            if (!Instance) return;
            Instance._addSteer += value;
        }

        public static void AddSlowBrake(float value01)
        {
            if (!Instance) return;
            Instance._addSlow = Mathf.Max(Instance._addSlow, value01);
        }

        #endregion
    }
}