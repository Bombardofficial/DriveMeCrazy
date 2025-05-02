using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics; // for float3 & math.normalize

namespace ArcadeVP
{
    [RequireComponent(typeof(Collider))]
    public class ArcadeVehicleController : MonoBehaviour
    {
        // --- Existing Headers (Spline Path, Speed Settings, Lane Settings, Drifting Settings) ---
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

        [Header("Drifting Settings")]
        public float cornerDetectionLookahead = 10f;
        public float cornerAngleThreshold = 15f;
        public float minDriftYawAngle = 20f;
        public float maxDriftYawAngle = 45f;
        public float maxCornerAngleForFullDrift = 60f;
        [Range(0f, 1f)]
        public float minSpeedFractionForYawEffect = 0.2f;
        [Range(0f, 1f)]
        public float maxSpeedFractionForYawEffect = 0.8f;
        public float driftRollAngle = 10f;
        public float driftEntrySpeed = 8f;
        public float driftExitSpeed = 5f;
        private bool isDrifting = false;
        private float currentDriftYaw = 0f;
        private int driftDirection = 0;
        private float detectedCornerAngle = 0f;
        // --- End Existing Headers ---

        [Header("Ground Settings")]
        public LayerMask drivableSurface;
        public float raycastHeight = 5f; // How far above targetPos to start the raycast
        public float groundCheckDistance = 1.0f; // Max distance below targetPos to check for ground (Adjust this!)
        public float heightAboveGround = 0.5f;
        [Tooltip("Multiplier for physics gravity when airborne")]
        public float gravityScale = 2.0f; // Adjust gravity strength
        private bool isGrounded = true; // State variable to know if we are on the ground
        private float verticalVelocity = 0f; // Car's current vertical speed

        [Header("Body Tilt")]
        public float pitchAngle = 5f;
        public float rollAngle = 8f;
        public float tiltLerpSpeed = 5f;
        [Tooltip("Max pitch angle when airborne based on vertical velocity")]
        public float airbornePitchAngle = 30f; // Control how much the nose lifts/drops in air
        [Tooltip("How quickly the body pitches when airborne")]
        public float airbornePitchSpeed = 3f;
        public Transform bodyMesh;


        // --- Other Headers (Deceleration, Audio, Skid Marks) ---
        [Header("Deceleration & Braking")]
        public float decelerationRate = 2f;
        public float brakeDeceleration = 10f;

        [Header("Audio Settings")]
        public AudioSource engineSound;
        [Range(0f, 1f)] public float minPitch = 1f;
        [Range(1f, 3f)] public float maxPitch = 3f;
        public AudioSource skidSound;
        public float maxPitchChangePerSecond = 2.0f;
        private float currentEnginePitch;
        private float enginePitchSmoothVelocity;
        private bool wasSkidding = false; // Combined state for lane change or drift

        [Header("Skid Mark Settings")]
        public float skidWidth = 0.4f;

        private float smoothedSpeedRatio = 0f;
        private float speedRatioSmoothVelocity = 0f;
        public float speedRatioSmoothTime = 0.05f;

        private float steeringInput, accelerationInput, driftInput, slowInput;
        // --- End Other Headers ---

        public float Speed => speed;   // expose current forward speed
        public bool IsChangingLane => isChangingLane;
        public bool IsDrifting => isDrifting;
        public bool IsGrounded => isGrounded; // Public accessor for grounded state


        // Start remains the same
        void Start()
        {
            if (splineContainer == null) { Debug.LogError("No SplineContainer on " + name); enabled = false; return; }
            spline = splineContainer.Spline;
            splineLength = spline.GetLength();
            laneOffsets[0] = -laneOffsetDistance; laneOffsets[1] = 0f; laneOffsets[2] = +laneOffsetDistance;
            targetOffset = currentOffset = laneOffsets[currentLane];
            if (engineSound != null) currentEnginePitch = engineSound.pitch; else currentEnginePitch = minPitch;
            enginePitchSmoothVelocity = 0f; smoothedSpeedRatio = 0f; speedRatioSmoothVelocity = 0f;
            if (bodyMesh == null) bodyMesh = transform.GetChild(0);
            if (bodyMesh == null) Debug.LogWarning("Body Mesh not assigned for tilt/drift visuals on " + name);
            if (maxCornerAngleForFullDrift <= cornerAngleThreshold) { Debug.LogWarning("maxCornerAngleForFullDrift > cornerAngleThreshold. Adjusting."); maxCornerAngleForFullDrift = cornerAngleThreshold + 10f; }
            if (maxDriftYawAngle < minDriftYawAngle) { Debug.LogWarning("maxDriftYawAngle >= minDriftYawAngle. Adjusting."); maxDriftYawAngle = minDriftYawAngle; }
            if (maxSpeedFractionForYawEffect < minSpeedFractionForYawEffect) { Debug.LogWarning("maxSpeedFractionForYawEffect >= minSpeedFractionForYawEffect. Adjusting."); maxSpeedFractionForYawEffect = minSpeedFractionForYawEffect + 0.1f; }
            // Ensure car starts grounded
            isGrounded = CheckIfGrounded(out _); // Perform initial ground check
        }

        void Update()
        {
            float dt = Time.deltaTime;

            // Speed Calculation
            speed += accelerationInput * acceleration * dt;
            if (slowInput > 0f)
                speed = Mathf.MoveTowards(speed, 0f, slowInput * brakeDeceleration * dt);
            else if (Mathf.Approximately(accelerationInput, 0f))
                speed = Mathf.MoveTowards(speed, 0f, decelerationRate * dt);
            speed = Mathf.Clamp(speed, -maxSpeed, maxSpeed);

            // Update smoothed speed ratio
            float rawRatio = Mathf.Clamp01(Mathf.Abs(speed) / maxSpeed);
            smoothedSpeedRatio = Mathf.SmoothDamp(smoothedSpeedRatio, rawRatio, ref speedRatioSmoothVelocity, speedRatioSmoothTime, Mathf.Infinity, dt);

            HandleLaneChangeTap();
            DetectCorner();

            // Skid sound trigger - only if actually grounded
            bool shouldSkid = isGrounded && (isChangingLane || isDrifting); // Use isGrounded state
            if (shouldSkid && !wasSkidding && skidSound != null) skidSound.Play();
            if ((!shouldSkid || !isGrounded) && wasSkidding && skidSound != null && skidSound.isPlaying) skidSound.Stop(); // Stop if not skidding OR not grounded
            wasSkidding = shouldSkid;

            // Engine sound update
            UpdateEngineSound(dt);

            // Lane offset update
            currentOffset = Mathf.MoveTowards(currentOffset, targetOffset, laneChangeSpeed * dt);

            // --- Movement and physics update ---
            MoveAndHandlePhysics(dt); // New method to handle movement and jumps
            // --- --- --- ---

            UpdateBodyTilt(dt); // Update visual tilt

            previousSteer = steeringInput;
        }

        // ProvideInputs remains the same
        public void ProvideInputs(float steer, float accel, float drift, float slow)
        {
            steeringInput = steer; accelerationInput = accel; driftInput = drift; slowInput = slow;
        }

        // HandleLaneChangeTap remains the same
        private void HandleLaneChangeTap()
        {
            if (Time.time < lastLaneChangeTime + laneChangeCooldown) return;
            if (Mathf.Abs(speed) < maxSpeed * minSpeedFractionForLaneChange) return;
            bool tapR = steeringInput > 0f && previousSteer <= 0f;
            bool tapL = steeringInput < 0f && previousSteer >= 0f;
            int newLane = currentLane;
            if (tapL && currentLane < 2) newLane++;
            if (tapR && currentLane > 0) newLane--;
            if (newLane != currentLane) { currentLane = newLane; targetOffset = laneOffsets[newLane]; lastLaneChangeTime = Time.time; }
        }

        // DetectCorner remains the same
        private void DetectCorner()
        {
            if (spline == null || splineLength <= 0f) { isDrifting = false; driftDirection = 0; detectedCornerAngle = 0f; return; }
            float currentT = traveledDistance / splineLength;
            float futureDistance = traveledDistance + Mathf.Sign(speed) * cornerDetectionLookahead;
            if (Mathf.Abs(speed) < 0.1f) futureDistance = traveledDistance;
            float futureT = Mathf.Repeat(futureDistance / splineLength, 1f);
            float3 localTangentCurrent = math.normalizesafe(spline.EvaluateTangent(currentT));
            float3 localTangentFuture = math.normalizesafe(spline.EvaluateTangent(futureT));
            Vector3 worldTangentCurrent = splineContainer.transform.TransformDirection(localTangentCurrent).normalized;
            Vector3 worldTangentFuture = splineContainer.transform.TransformDirection(localTangentFuture).normalized;
            Vector3 tangentCurrentXZ = new Vector3(worldTangentCurrent.x, 0, worldTangentCurrent.z).normalized;
            Vector3 tangentFutureXZ = new Vector3(worldTangentFuture.x, 0, worldTangentFuture.z).normalized;
            if (tangentCurrentXZ == Vector3.zero || tangentFutureXZ == Vector3.zero || tangentCurrentXZ == tangentFutureXZ) { isDrifting = false; driftDirection = 0; detectedCornerAngle = 0f; return; }
            float angle = Vector3.Angle(tangentCurrentXZ, tangentFutureXZ);
            float crossY = Vector3.Cross(tangentCurrentXZ, tangentFutureXZ).y;
            if (angle > cornerAngleThreshold) { isDrifting = true; driftDirection = (int)Mathf.Sign(crossY); detectedCornerAngle = angle; }
            else { isDrifting = false; driftDirection = 0; detectedCornerAngle = 0f; }
        }


        // --- NEW Method for Movement & Physics ---
        private void MoveAndHandlePhysics(float dt)
        {
            // 1. Advance distance along spline
            traveledDistance = Mathf.Repeat(traveledDistance + speed * dt, splineLength);
            float t = traveledDistance / splineLength;

            // 2. Evaluate spline
            SplineUtility.Evaluate(spline, t, out float3 lp, out float3 lt, out float3 lu);
            Vector3 worldP_Spline = splineContainer.transform.TransformPoint(lp);
            Vector3 worldT = splineContainer.transform.TransformDirection(lt).normalized;
            Vector3 worldUp_Spline = splineContainer.transform.TransformDirection(lu).normalized;
            Vector3 worldRight = splineContainer.transform.TransformDirection(math.normalizesafe(math.cross(lt, lu)));

            // Calculate target XZ position
            Vector3 worldPosOnSpline = worldP_Spline + worldRight * currentOffset; // Renamed for clarity

            Vector3 groundCheckOrigin = worldPosOnSpline + Vector3.up * raycastHeight;
            float downwardCheckDistance = raycastHeight + groundCheckDistance;

            bool groundHit = Physics.Raycast(
                    groundCheckOrigin, Vector3.down, out var hit,
                    downwardCheckDistance, drivableSurface, QueryTriggerInteraction.Ignore);

            Debug.DrawRay(groundCheckOrigin, Vector3.down * downwardCheckDistance,
                          groundHit ? Color.green : Color.red);
            Debug.DrawRay(groundCheckOrigin, Vector3.down * downwardCheckDistance, groundHit ? Color.green : Color.red);

            isGrounded = groundHit;

            // 4. Determine Target Y Position & Apply Physics
            float targetY;
            Vector3 finalPosition;

            if (isGrounded)
            {
                // --- DETAILED LOGGING ---
                GameObject hitObject = hit.collider.gameObject;
                float hitY = hit.point.y;
                targetY = hitY + heightAboveGround;


                // --- END LOGGING ---

                // Check for non-physical Y values before applying
                if (float.IsNaN(targetY) || float.IsInfinity(targetY))
                {
                    Debug.LogError($"Invalid TargetY calculated: {targetY}. Hit Point: {hit.point}, Height: {heightAboveGround}. Resetting to current Y.");
                    targetY = transform.position.y; // Fallback to prevent error propagation
                    isGrounded = false; // Treat as not grounded if calculation failed
                    verticalVelocity = 0f; // Prevent accumulating velocity from bad state
                }
                else
                {
                    if (verticalVelocity < 0)
                        verticalVelocity = 0f; // Reset velocity on proper landing
                }

                finalPosition = new Vector3(worldPosOnSpline.x, targetY, worldPosOnSpline.z);
            }
            else // Airborne
            {
                verticalVelocity += Physics.gravity.y * gravityScale * dt;
                targetY = transform.position.y + verticalVelocity * dt;

                finalPosition = new Vector3(worldPosOnSpline.x, targetY, worldPosOnSpline.z);
            }

            // Apply final calculated position
            transform.position = finalPosition;

            // 5. Handle Rotation (Same as before)
            Vector3 forwardXZ = new Vector3(worldT.x, 0f, worldT.z).normalized;
            if (forwardXZ == Vector3.zero) forwardXZ = transform.forward;

            Quaternion baseRotation = Quaternion.LookRotation(forwardXZ, worldUp_Spline);

            float targetDriftYaw = 0f;
            if (isDrifting)
            {
                float cornerIntensity = Mathf.Clamp01(Mathf.InverseLerp(cornerAngleThreshold, maxCornerAngleForFullDrift, detectedCornerAngle));
                float cornerBasedYaw = Mathf.Lerp(minDriftYawAngle, maxDriftYawAngle, cornerIntensity);
                float speedScaleFactor = Mathf.Clamp01(Mathf.InverseLerp(minSpeedFractionForYawEffect, maxSpeedFractionForYawEffect, smoothedSpeedRatio));
                float speedScaledYaw = cornerBasedYaw * speedScaleFactor;
                targetDriftYaw = speedScaledYaw * driftDirection;
            }
            currentDriftYaw = Mathf.Lerp(currentDriftYaw, targetDriftYaw, (isDrifting ? driftEntrySpeed : driftExitSpeed) * dt);
            Quaternion driftRot = Quaternion.Euler(0, currentDriftYaw, 0);
            Quaternion targetRotation = baseRotation * driftRot;

            transform.rotation = targetRotation;
        }


        private void UpdateBodyTilt(float dt)
        {
            if (bodyMesh == null) return;

            float desiredRoll;
            float desiredPitch;

            if (isGrounded)
            {
                // Grounded Tilt Logic (Lane Change / Drift Roll / Accel/Brake Pitch)
                desiredPitch = -accelerationInput * pitchAngle + slowInput * pitchAngle;
                float laneDir = isChangingLane ? Mathf.Sign(targetOffset - currentOffset) : 0f;
                // Scale drift roll effect based on how much yaw is actually applied?
                float driftRollFactor = (minDriftYawAngle > 0) ? Mathf.Clamp01(Mathf.Abs(currentDriftYaw) / minDriftYawAngle) : 0f;
                float driftRoll = isDrifting ? -driftDirection * driftRollAngle * driftRollFactor : 0f;
                desiredRoll = -laneDir * rollAngle + driftRoll;
            }
            else // Airborne Tilt Logic
            {
                // Pitch based on vertical velocity (nose up when going up, nose down when falling)
                float pitchRatio = Mathf.Clamp(verticalVelocity / (maxSpeed * 0.5f), -1f, 1f); // Normalize velocity roughly
                desiredPitch = pitchRatio * airbornePitchAngle;

                // Roll could level out in air, or maintain drift roll? Let's level it out smoothly.
                desiredRoll = 0f; // Lerp towards level roll in air
            }

            // Smoothly apply tilt using Slerp on local rotation
            Quaternion currentLocalRot = bodyMesh.localRotation;
            // Target roll/pitch based on grounded/airborne state
            Quaternion targetLocalRot = Quaternion.Euler(desiredPitch, 0, desiredRoll);

            // Use different lerp speeds? Maybe faster pitch in air? For now, one speed.
            float lerpSpeed = isGrounded ? tiltLerpSpeed : airbornePitchSpeed; // Use different speeds potentially

            bodyMesh.localRotation = Quaternion.Slerp(currentLocalRot, targetLocalRot, lerpSpeed * dt);
        }


        // UpdateEngineSound remains the same
        private void UpdateEngineSound(float dt)
        {
            if (engineSound == null || maxSpeed <= 0f) return;
            float targetPitch = Mathf.Lerp(minPitch, maxPitch, smoothedSpeedRatio);
            float maxDelta = maxPitchChangePerSecond * dt;
            currentEnginePitch = Mathf.MoveTowards(currentEnginePitch, targetPitch, maxDelta);
            engineSound.pitch = currentEnginePitch;
        }


        // Replaced by internal isGrounded check, but keep public accessor
        public bool grounded()
        {
            // This uses the state updated in MoveAndHandlePhysics
            return isGrounded;
        }

        // Optional helper for initial ground check or manual checks
        private bool CheckIfGrounded(out RaycastHit hitInfo)
        {
            // Use current transform position for the check
            Vector3 origin = transform.position + Vector3.up * 0.1f;
            return Physics.Raycast(origin, Vector3.down, out hitInfo, groundCheckDistance, drivableSurface);
        }

    }
}