using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;  // for float3 & math.normalize

namespace ArcadeVP
{
    [RequireComponent(typeof(Collider))]
    public class ArcadeVehicleController : MonoBehaviour
    {
        [Header("Spline Path")]
        public SplineContainer splineContainer;
        private Spline spline;
        private float splineLength;

        [Header("Speed Settings")]
        public float maxSpeed = 20f;
        public float acceleration = 5f;
        private float speed = 0f;
        private float traveledDistance = 0f;

        [Header("Lane Settings")]
        public float laneOffsetDistance = 2f;
        public float laneChangeSpeed = 10f;
        public float laneSnapThreshold = 0.05f;
        public float laneChangeCooldown = 0.75f;
        [Range(0f, 1f)]
        public float minSpeedFractionForLaneChange = 0.1f;
        private readonly float[] laneOffsets = new float[3];
        private int currentLane = 1;
        private float targetOffset, currentOffset;
        private float lastLaneChangeTime = -Mathf.Infinity;
        private float previousSteer = 0f;
        private bool isChangingLane => Mathf.Abs(currentOffset - targetOffset) > laneSnapThreshold;

        [Header("Ground Settings")]
        public LayerMask drivableSurface;
        public float raycastHeight = 5f;
        public float groundCheckDistance = 10f;
        public float heightAboveGround = 0.5f;

        [Header("Body Tilt")]
        [Tooltip("Max pitch angle (deg) when accel/brake")]
        public float pitchAngle = 5f;
        [Tooltip("Max roll angle (deg) when changing lanes")]
        public float rollAngle = 8f;
        [Tooltip("How quickly to lerp pitch & roll")]
        public float tiltLerpSpeed = 5f;
        public Transform bodyMesh;

        [Header("Deceleration & Braking")]
        [Tooltip("How quickly the car coasts to zero when no throttle/brake")]
        public float decelerationRate = 2f;
        [Tooltip("How strong the brake is when pressing slow/brake input")]
        public float brakeDeceleration = 10f;

        [Header("Audio Settings")]
        public AudioSource engineSound;
        [Range(0f, 1f)] public float minPitch = 1f;
        [Range(1f, 3f)] public float maxPitch = 3f;
        public AudioSource skidSound;
        [Tooltip("How quickly the engine pitch reacts to speed changes")]
        public float enginePitchSmoothTime = 0.1f;
        private float currentSmoothedEnginePitch;
        private float enginePitchSmoothVelocity;
        private bool wasChangingLane = false;

        [Header("Skid Mark Settings")]
        [Tooltip("Width of the skid mark trails")]
        public float skidWidth = 0.4f; // Restore this variable


        [Header("Audio Smoothing")]
        [Tooltip("How quickly the filtered speed ratio catches up to actual speed (higher = snappier)")]
        public float audioSmoothingFactor = 2f;

        [Tooltip("Max pitch change per second (units of pitch)")]
        public float maxPitchDeltaPerSecond = 1f;

        // internal state for smoothing
        private float filteredSpeedRatio = 0f;
        // inputs
        private float steeringInput, accelerationInput, driftInput, slowInput;

        public bool IsChangingLane => isChangingLane;

        void Start()
        {
            if (splineContainer == null)
            {
                Debug.LogError("No SplineContainer on " + name);
                enabled = false;
                return;
            }
            spline = splineContainer.Spline;
            splineLength = spline.GetLength();

            // set up lane offsets
            laneOffsets[0] = -laneOffsetDistance;
            laneOffsets[1] = 0f;
            laneOffsets[2] = +laneOffsetDistance;
            targetOffset = currentOffset = laneOffsets[currentLane];

            // init engine sound
            if (engineSound != null)
                currentSmoothedEnginePitch = Mathf.Max(engineSound.pitch, minPitch);
            else
                currentSmoothedEnginePitch = minPitch;

            enginePitchSmoothVelocity = 0f;
            filteredSpeedRatio = 0f;
        }

        void Update()
        {
            float dt = Time.deltaTime;

            // —— 1) speed integration ——
            speed += accelerationInput * acceleration * dt;

            // —— 2) brake deceleration ——
            if (slowInput > 0f)
                speed = Mathf.MoveTowards(speed, 0f, slowInput * brakeDeceleration * dt);
            // —— 3) natural drag ——
            else if (Mathf.Approximately(accelerationInput, 0f))
                speed = Mathf.MoveTowards(speed, 0f, decelerationRate * dt);

            speed = Mathf.Clamp(speed, -maxSpeed, maxSpeed);

            // —— 4) lane-change input ——
            HandleLaneChangeTap();

            // —— 5) skid sound ——
            bool nowChanging = isChangingLane && grounded();
            if (nowChanging && !wasChangingLane && skidSound != null)
                skidSound.Play();
            if ((!nowChanging && wasChangingLane) && skidSound != null && skidSound.isPlaying)
                skidSound.Stop();
            wasChangingLane = nowChanging;

            float rawRatio = Mathf.Clamp01(Mathf.Abs(speed) / maxSpeed);
            // exponential smoothing: the higher the factor, the faster it follows
            filteredSpeedRatio = Mathf.Lerp(filteredSpeedRatio, rawRatio, dt * audioSmoothingFactor);

            // now use that filtered value to drive pitch
            UpdateEngineSound(dt);

            // —— 7) slide offset ——
            currentOffset = Mathf.MoveTowards(currentOffset, targetOffset, laneChangeSpeed * dt);

            // —— 8) movement & tilt ——
            MoveAlongSplineAndLanes(dt);
            UpdateBodyTilt(dt);

            previousSteer = steeringInput;
        }

        public void ProvideInputs(float steer, float accel, float drift, float slow)
        {
            steeringInput = steer;
            accelerationInput = accel;
            driftInput = drift;
            slowInput = slow;
        }

        private void HandleLaneChangeTap()
        {
            if (Time.time < lastLaneChangeTime + laneChangeCooldown) return;
            if (Mathf.Abs(speed) < maxSpeed * minSpeedFractionForLaneChange) return;

            bool tapR = steeringInput > 0f && previousSteer <= 0f;
            bool tapL = steeringInput < 0f && previousSteer >= 0f;
            int newLane = currentLane;

            if (tapR && currentLane < 2) newLane++;
            if (tapL && currentLane > 0) newLane--;

            if (newLane != currentLane)
            {
                currentLane = newLane;
                targetOffset = laneOffsets[newLane];
                lastLaneChangeTime = Time.time;
            }
        }

        private void MoveAlongSplineAndLanes(float dt)
        {
            // advance
            traveledDistance = Mathf.Repeat(traveledDistance + speed * dt, splineLength);
            float t = traveledDistance / splineLength;

            // sample & world-transform
            float3 lp = spline.EvaluatePosition(t);
            float3 lt = spline.EvaluateTangent(t);
            Vector3 worldP = splineContainer.transform.TransformPoint(lp);
            Vector3 worldT = splineContainer.transform.TransformDirection(lt).normalized;

            // project to XZ and apply offset
            Vector3 centerXZ = new Vector3(worldP.x, 0f, worldP.z);
            Vector3 forwardXZ = new Vector3(worldT.x, 0f, worldT.z).normalized;
            Vector3 rightXZ = Vector3.Cross(Vector3.up, forwardXZ).normalized;
            Vector3 pos = centerXZ + rightXZ * currentOffset;

            // ray down for Y
            Vector3 rayO = new Vector3(pos.x, worldP.y + raycastHeight, pos.z);
            if (Physics.Raycast(rayO, Vector3.down, out var hit, raycastHeight + groundCheckDistance, drivableSurface))
                pos.y = hit.point.y + heightAboveGround;
            else
                pos.y = transform.position.y;

            transform.position = pos;
            transform.rotation = Quaternion.LookRotation(forwardXZ, Vector3.up);
        }

        private void UpdateBodyTilt(float dt)
        {
            if (bodyMesh == null) return;

            // pitch: accel ? nose up; brake ? nose down
            float desiredPitch = -accelerationInput * pitchAngle
                               + slowInput * pitchAngle;
            // roll: lean into lane-change
            float laneDir = isChangingLane ? Mathf.Sign(targetOffset - currentOffset) : 0f;
            float desiredRoll = -laneDir * rollAngle;

            Vector3 e = bodyMesh.localEulerAngles;
            float curX = e.x > 180f ? e.x - 360f : e.x;
            float curZ = e.z > 180f ? e.z - 360f : e.z;
            float smX = Mathf.Lerp(curX, desiredPitch, tiltLerpSpeed * dt);
            float smZ = Mathf.Lerp(curZ, desiredRoll, tiltLerpSpeed * dt);

            bodyMesh.localEulerAngles = new Vector3(smX, e.y, smZ);
        }

        /// <summary>
        /// Updates engineSound.pitch using the already-computed filteredSpeedRatio.
        /// </summary>
        private void UpdateEngineSound(float dt)
        {
            if (engineSound == null || maxSpeed <= 0f) return;

            // 1) compute target pitch from filtered ratio
            float targetPitch = Mathf.Lerp(minPitch, maxPitch, filteredSpeedRatio);

            // 2) clamp how fast the pitch can move per second
            float maxDelta = maxPitchDeltaPerSecond * dt;
            currentSmoothedEnginePitch = Mathf.MoveTowards(
                currentSmoothedEnginePitch,
                targetPitch,
                maxDelta
            );

            // 3) apply
            engineSound.pitch = currentSmoothedEnginePitch;
        }

        public bool grounded()
        {
            Vector3 o = transform.position + Vector3.up * 0.1f;
            return Physics.Raycast(o, Vector3.down, groundCheckDistance, drivableSurface);
        }
    }
}
