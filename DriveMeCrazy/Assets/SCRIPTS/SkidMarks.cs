using UnityEngine;

namespace ArcadeVP
{
    [RequireComponent(typeof(TrailRenderer))]
    public class SkidMarks : MonoBehaviour
    {
        private TrailRenderer skidMark;
        private ParticleSystem smoke;
        public ArcadeVehicleController carController;

        Color originalColor;

        /*  you can tweak these in the Inspector if desired  */
        [Header("Trail Tuning (optional)")]
        [Tooltip("How long the skid mark stays in the scene (seconds)")]
        public float trailLifeTime = 10f;
        [Tooltip("Minimum distance before a new vertex is added")]
        public float trailMinVertexDistance = 0.02f;

        void Awake()
        {
            skidMark = GetComponent<TrailRenderer>();
            smoke = GetComponent<ParticleSystem>();

            if (!carController)
            {
                Debug.LogError($"{name}: SkidMarks needs a reference to ArcadeVehicleController");
                enabled = false;
                return;
            }
            if (!carController)
                carController = GetComponentInParent<ArcadeVehicleController>();

            /* ---------- one‑time TrailRenderer set‑up ---------------------- */
            //skidMark.worldSpace = true;
            skidMark.time = trailLifeTime;
            skidMark.minVertexDistance = trailMinVertexDistance;
            skidMark.startWidth = skidMark.endWidth = carController.skidWidth;
            originalColor = skidMark.material ? skidMark.material.color : Color.black;
            originalColor.a = 1f;
            skidMark.startColor = skidMark.endColor = originalColor;
            skidMark.material.color = originalColor;
            skidMark.emitting = false;
            /* --------------------------------------------------------------- */

            if (smoke)
            {
                smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        void OnDisable() => StopSkidImmediately();
        void OnDestroy() => StopSkidImmediately();

        void Update()
        {
            if (!carController || !skidMark) return;

            bool skidNow = carController.IsGrounded &&
               (carController.IsChangingLane ||
                carController.IsDrifting ||
                carController.IsBrakePressed);

            if (skidNow)
            {
                if (!skidMark.emitting)
                {
                    skidMark.Clear();
                    skidMark.emitting = true;
                }

                if (smoke)
                {
                    if (!smoke.isPlaying)
                    {
                        smoke.Clear();
                        smoke.Play(true);
                    }
                }
            }
            else
            {
                if (skidMark.emitting) skidMark.emitting = false;
                if (smoke && smoke.isPlaying) smoke.Stop();
            }
        }

        /* -------------------------------------------------------------------- */
        void StopSkidImmediately()
        {
            if (skidMark)
            {
                skidMark.emitting = false;
                skidMark.Clear();
            }
            if (smoke)
            {
                smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }
}
