using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace ArcadeVP.UI
{
    public class SpeedometerUI : MonoBehaviour
    {
        public ArcadeVP.ArcadeVehicleController vehicle;
        public RectTransform pointer;
        public TextMeshProUGUI readout;

        [Tooltip("km/h per controller speed unit (1 m/s = 3.6 km/h)")]
        public float unitsToKmh = 3.6f;

        [Tooltip("Full-scale km/h. 0 = use vehicle.maxSpeed")]
        public float maxDisplayKmh = 0f;

        /* ----- Linear fallback that actually matches your face ----- */
        [Header("Legacy linear (fallback)")]
        [Tooltip("Z rotation at 0 km/h")]
        public float angleAtZero = 55f;
        [Tooltip("Z rotation at full scale (your 120 km/h = -81°)")]
        public float angleAtMax = -81f;

        /* ----- Non-linear calibration ----- */
        [System.Serializable]
        public struct CalibrationPoint { public float kmh; public float angle; }

        [Header("Dial calibration (non-linear)")]
        public List<CalibrationPoint> calibration = new List<CalibrationPoint>();
        public bool clampOutsideCalibration = true;

        [Header("Needle dynamics")]
        public float needleSweepSpeed = 300f;

        [Header("Top-end flutter")]
        [Range(0.7f, 1f)] public float jitterStart = 0.98f;
        public float jitterAmplitude = 2f;
        public float jitterFrequency = 20f;

        float currentAngle;

        void Awake()
        {
            // ensure pointer ref when dropped in the scene
            if (!pointer && transform.childCount > 0)
                pointer = transform.GetChild(0).GetComponent<RectTransform>();

            // IMPORTANT: Seed calibration here so it exists at runtime even if you didn't press Reset().
            EnsureCalibrationSeeded();
        }

        void OnValidate()
        {
            // Make sure the list exists and is sorted
            if (calibration == null) calibration = new List<CalibrationPoint>();
            if (calibration.Count == 0) EnsureCalibrationSeeded();
            if (calibration.Count > 1) calibration.Sort((a, b) => a.kmh.CompareTo(b.kmh));
        }

        void EnsureCalibrationSeeded()
        {
            if (calibration.Count > 0) return;

            calibration = new List<CalibrationPoint>
            {
                new CalibrationPoint{ kmh=  0f, angle= 55.00f},
                new CalibrationPoint{ kmh= 20f, angle= 42.72f},
                new CalibrationPoint{ kmh= 30f, angle= 29.70f},
                new CalibrationPoint{ kmh= 40f, angle= 17.80f},
                new CalibrationPoint{ kmh= 50f, angle=  4.90f},
                new CalibrationPoint{ kmh= 60f, angle= -7.40f},
                new CalibrationPoint{ kmh= 70f, angle=-19.03f},
                new CalibrationPoint{ kmh= 80f, angle=-31.30f},
                new CalibrationPoint{ kmh= 90f, angle=-44.10f},
                new CalibrationPoint{ kmh=100f, angle=-56.30f},
                new CalibrationPoint{ kmh=110f, angle=-68.40f},
                new CalibrationPoint{ kmh=120f, angle=-81.00f},
            };
        }

        void Update()
        {
            if (!vehicle || !pointer) return;

            // speed in km/h from your controller (m/s * 3.6)
            float kmh = Mathf.Abs(vehicle.Speed) * unitsToKmh;

            // full scale only used for flutter %
            float fullScale = (maxDisplayKmh > 1f)
                ? maxDisplayKmh
                : Mathf.Max(1f, vehicle.maxSpeed * unitsToKmh);

            // get angle from calibration (fallback to linear if somehow missing)
            float targetAngle = (calibration != null && calibration.Count >= 2)
                ? EvaluateAngleFromCalibration(kmh)
                : Mathf.Lerp(angleAtZero, angleAtMax, Mathf.Clamp01(kmh / fullScale));

            // smooth needle
            currentAngle = Mathf.MoveTowards(currentAngle, targetAngle, needleSweepSpeed * Time.deltaTime);

            // subtle flutter near top end
            float tFS = Mathf.Clamp01(kmh / fullScale);
            float jitter = 0f;
            if (tFS >= jitterStart)
            {
                float strength = Mathf.InverseLerp(jitterStart, 1f, tFS);
                float wave = Mathf.Sin(Time.time * jitterFrequency * 2f * Mathf.PI);
                jitter = wave * jitterAmplitude * strength;
            }

            pointer.localRotation = Quaternion.Euler(0f, 0f, currentAngle + jitter);

            if (readout) readout.text = Mathf.RoundToInt(kmh).ToString();
        }

        float EvaluateAngleFromCalibration(float kmh)
        {
            if (kmh <= calibration[0].kmh)
                return clampOutsideCalibration ? calibration[0].angle : ExtrapolateLeft(kmh);

            int last = calibration.Count - 1;
            if (kmh >= calibration[last].kmh)
                return clampOutsideCalibration ? calibration[last].angle : ExtrapolateRight(kmh);

            for (int i = 0; i < last; i++)
            {
                var a = calibration[i];
                var b = calibration[i + 1];
                if (kmh >= a.kmh && kmh <= b.kmh)
                {
                    float t = Mathf.InverseLerp(a.kmh, b.kmh, kmh);
                    return Mathf.Lerp(a.angle, b.angle, t);
                }
            }
            return calibration[last].angle; // shouldn’t hit
        }

        float ExtrapolateLeft(float kmh)
        {
            var a = calibration[0];
            var b = calibration[Mathf.Min(1, calibration.Count - 1)];
            float slope = (b.angle - a.angle) / Mathf.Max(0.0001f, (b.kmh - a.kmh));
            return a.angle + slope * (kmh - a.kmh);
        }

        float ExtrapolateRight(float kmh)
        {
            int last = calibration.Count - 1;
            var a = calibration[Mathf.Max(0, last - 1)];
            var b = calibration[last];
            float slope = (b.angle - a.angle) / Mathf.Max(0.0001f, (b.kmh - a.kmh));
            return b.angle + slope * (kmh - b.kmh);
        }
    }
}
