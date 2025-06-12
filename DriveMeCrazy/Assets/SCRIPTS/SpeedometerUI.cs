using UnityEngine;
using TMPro;

namespace ArcadeVP.UI
{
    public class SpeedometerUI : MonoBehaviour
    {
        /* ????? References ????? */
        public ArcadeVP.ArcadeVehicleController vehicle;
        public RectTransform pointer;
        public TextMeshProUGUI readout;

        /* ????? Dial calibration ????? */
        [Tooltip("Z?rotation when speed = 0 km/h")]
        public float angleAtZero = 55f;
        [Tooltip("Z?rotation at full?scale")]
        public float angleAtMax = -180f;

        [Tooltip("km/h per controller speed unit (1?m/s ??3.6)")]
        public float unitsToKmh = 3.6f;

        [Tooltip("Full?scale km/h. 0 = auto?use vehicle.maxSpeed")]
        public float maxDisplayKmh = 0f;

        /* ????? Needle dynamics ????? */
        [Tooltip("Needle slew rate in deg/sec")]
        public float needleSweepSpeed = 300f;

        /* ????? Max?speed flutter ????? */
        [Tooltip("Percent of full?scale where jitter begins (0?1)")]
        [Range(0.7f, 1f)] public float jitterStart = 0.98f;
        [Tooltip("Peak jitter amplitude in degrees")]
        public float jitterAmplitude = 2f;
        [Tooltip("Jitter frequency in Hz")]
        public float jitterFrequency = 20f;

        /* ????? Internals ????? */
        float currentAngle;

        void Reset()
        {
            if (!pointer && transform.childCount > 0)
                pointer = transform.GetChild(0).GetComponent<RectTransform>();
        }

        void Update()
        {
            if (!vehicle || !pointer) return;

            /* 1. speed ? km/h */
            float kmh = Mathf.Abs(vehicle.Speed) * unitsToKmh;

            /* 2. choose effective full?scale */
            float fullScale = (maxDisplayKmh > 1f)
                              ? maxDisplayKmh
                              : Mathf.Max(1f, vehicle.maxSpeed * unitsToKmh);

            /* 3. map to [0..1] then angle */
            float t = Mathf.Clamp01(kmh / fullScale);
            float targetAngle = Mathf.Lerp(angleAtZero, angleAtMax, t);

            /* 4. smooth mechanical motion */
            currentAngle = Mathf.MoveTowards(
                currentAngle, targetAngle, needleSweepSpeed * Time.deltaTime);

            /* 5. flutter near top */
            float jitter = 0f;
            if (t >= jitterStart)
            {
                float strength = Mathf.InverseLerp(jitterStart, 1f, t);   // 0?>1
                float wave = Mathf.Sin(Time.time * jitterFrequency * 2f * Mathf.PI);
                jitter = wave * jitterAmplitude * strength;
            }

            pointer.localRotation = Quaternion.Euler(0f, 0f, currentAngle + jitter);

            /* 6. optional digital read?out */
            if (readout) readout.text = Mathf.RoundToInt(kmh).ToString();
        }
    }
}
