//  SpeedLimitZone.cs
using UnityEngine;

namespace ArcadeVP
{
    public enum SpeedUnit { MetresPerSecond, KilometresPerHour }

    [RequireComponent(typeof(Collider))]
    public class SpeedLimitZone : MonoBehaviour
    {
        [Header("Limit value & unit")]
        public float limitValue = 60f;                     // default 60 km/h
        public SpeedUnit unit = SpeedUnit.KilometresPerHour;

        [Tooltip("Index in SpeedLimitUI.signObjects")]
        public int signIndex = 0;

        public string targetTag = "Driver";

        /* ---------- helper ---------- */
        float LimitMps =>
            unit == SpeedUnit.MetresPerSecond ? limitValue : limitValue / 3.6f;

        void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag(targetTag)) return;

            var car = other.attachedRigidbody
                       ? other.attachedRigidbody.GetComponent<ArcadeVehicleController>()
                       : other.GetComponent<ArcadeVehicleController>();

            if (!car) return;

            Debug.Log($"[SpeedZone]  ENTRY id={signIndex}  "
                    + $"{limitValue} {unit} ⇒ {LimitMps:0.00} m/s");

            car.EnterSpeedLimit(LimitMps, signIndex);
        }
    }
}
