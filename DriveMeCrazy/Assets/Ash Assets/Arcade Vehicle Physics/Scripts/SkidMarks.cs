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

            skidMark.startWidth = skidMark.endWidth = carController.skidWidth;
            originalColor = skidMark.material ? skidMark.material.color : Color.black;
            originalColor.a = 1f;

            skidMark.emitting = false;
        }

        void OnDisable() => StopSkidImmediately();
        void OnDestroy() => StopSkidImmediately();

        void Update()
        {
            if (!carController || !skidMark) return;

            bool shouldEmit = carController.grounded() &&
                              (carController.IsChangingLane || carController.IsDrifting);

            if (shouldEmit && !skidMark.emitting)
            {
                skidMark.Clear();
                skidMark.material.color = originalColor;
                skidMark.emitting = true;
                if (smoke) smoke.Play();
            }
            else if (!shouldEmit && skidMark.emitting)
            {
                skidMark.emitting = false;
                if (smoke) smoke.Stop();
            }
        }

        void StopSkidImmediately()
        {
            if (skidMark)
            {
                skidMark.emitting = false;
                skidMark.Clear();
            }
            if (smoke) smoke.Stop();
        }
    }
}
