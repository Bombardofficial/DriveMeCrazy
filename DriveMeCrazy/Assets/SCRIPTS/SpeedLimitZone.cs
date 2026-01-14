// SpeedLimitZone.cs
using UnityEngine;

namespace ArcadeVP
{
    public enum SpeedUnit { MetresPerSecond, KilometresPerHour }

    [RequireComponent(typeof(Collider))]
    public class SpeedLimitZone : MonoBehaviour
    {
        [Header("Limit value & unit")]
        public float limitValue = 60f;
        public SpeedUnit unit = SpeedUnit.KilometresPerHour;

        [Tooltip("Index (marad, ahogy eddig is)")]
        public int signIndex = 0;

        [Header("UI sprite for THIS zone")]
        public Sprite signSprite;

        public string targetTag = "Driver";

        float LimitMps => unit == SpeedUnit.MetresPerSecond ? limitValue : limitValue / 3.6f;

        void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag(targetTag)) return;

            var car = other.attachedRigidbody
                       ? other.attachedRigidbody.GetComponent<ArcadeVehicleController>()
                       : other.GetComponent<ArcadeVehicleController>();

            if (!car) return;

            // ---- QUICK UI FIX (index marad) ----
            if (SpeedLimitUI.Instance)
                SpeedLimitUI.Instance.ShowSprite(signIndex, signSprite);

            Debug.Log($"[SpeedZone] ENTRY id={signIndex} {limitValue} {unit} => {LimitMps:0.00} m/s");
            car.EnterSpeedLimit(LimitMps, signIndex);
        }
    }
}
