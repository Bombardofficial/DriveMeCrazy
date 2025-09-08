using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics; // for float3 & math.normalize
using Cinemachine;
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
        public float cornerDetectionLookahead = 2f;
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
        private float currentCurvature = 0f;
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

        [Tooltip("Max volume the skid sound reaches")]
        [Range(0f, 1f)] public float skidMaxVolume = 1f;
        [Tooltip("Seconds it takes to fade in or out")]
        public float skidFadeTime = 0.25f;

        public float maxPitchChangePerSecond = 2.0f;
        private float currentEnginePitch;
        private float enginePitchSmoothVelocity;
        private bool wasSkidding = false; // Combined state for lane change or drift

        [Header("Skid Mark Settings")]
        public float skidWidth = 0.4f;

        private float smoothedSpeedRatio = 0f;
        private float speedRatioSmoothVelocity = 0f;
        public float speedRatioSmoothTime = 0.05f;


        [Header("Speed-Limit Penalty")]
        public float speedOvershootTolerance = 0.05f;   // 5?% leeway
        public float overshootBrakeDecel = 15f;     // how hard we auto?brake
        public float overshootLockTime = 1.5f;    // seconds controls are frozen
        public float overshootCornerAngle = 10f;
        bool overshootRequiresCorner;
        public SpeedLimitUI speedLimitUI;               // optional UI reference
        public float frictionCoefficient = 200f;
        float activeSpeedLimit = 0f;      // 0 ? none
        float controlLockUntil = -999f;   // time until which player input is frozen
        Sprite currentSign;

        [Header("Speed-Zone Mini-Game")]
        public SpeedZoneBalanceUI balanceUI;
        [Range(0.05f, 1f)] public float greenMinWidth = 0.20f;
        [Range(0.05f, 1f)] public float greenMaxWidth = 0.50f;

        [Tooltip("Pointer auto-drift rate (units / sec)")]
        public float balanceDriftSpeed = 0.40f;
        [Tooltip("Player influence (units / sec)")]
        public float balanceInputPower = 1.30f;

        [Tooltip("|value| > thresh = red zone")]
        public float balanceFailThresh = 0.90f;
        [Tooltip("Seconds allowed in red before crash")]
        public float balanceFailGrace = 1.0f;

        [Header("Crash Effects")]
        [Tooltip("The yaw angle (in degrees) the car snaps to when crashing.")]
        public float crashYawAngle = 80f;
        [Tooltip("The roll angle (in degrees) of the car body when crashing.")]
        public float crashRollAngle = 45f;
        [Tooltip("The percentage of speed kept after a crash (0.4 = 40%).")]
        [Range(0f, 1f)] public float crashSpeedPenalty = 0.4f;
        [Tooltip("The intensity value sent to the camera shake event.")]
        public float crashShakeIntensity = 1.2f;

        [Header("Dynamic difficulty")]
        [Tooltip("Overshoot % where the game reaches MAX difficulty")]
        public float fullDifficultyAtRatio = 1.0f;   // 100 % over
                                                     // ??  MINI-GAME TUNING -------------------------------------------------
        [Header("Mini-game – trigger threshold")]
        [Tooltip("Overshoot ratio (0.15 = 15 % past the sign) below which the mini-game never triggers")]
        public float minOvershootRatioToTrigger = 0.15f;

        [Header("Mini-game – difficulty curves")]
        public float driftSpeedEasy = 0.15f;   // pointer auto-drift (units/s)
        public float driftSpeedHard = 0.60f;
        public float shrinkEasy = 0.02f;   // green-zone shrink (frac/s)
        public float shrinkHard = 0.15f;
        public float oscAmpEasy = 0.15f;   // zone oscillation amplitude
        public float oscAmpHard = 0.40f;
        public float zoneFracEasy = 0.60f;   // initial green-zone width (bar-frac)
        public float zoneFracHard = 0.18f;

        [Header("Mini-game – timing")]
        public float overspeedTriggerTime = 0.40f;  // sustain time before start
        public float extraGraceAfterSwap = 1.0f;   // in addition to driverChangeGrace
                                                   // ---------------------------------------------------------------------


        [Header("Corner Slow-Down")]
        [Tooltip("Speed kept in a 90-degree hair-pin (0.0-1.0)")]
        [Range(0.2f, 1f)] public float cornerSlowFactor = 0.55f;
        [Tooltip("Rate car scrubs speed in corners (m/s?)")]
        public float cornerDecel = 12f;

        private float _lastOvershootRatio = 0f;

        // Add this inside the [Header("Crash Effects")] section in the Inspector
        [Tooltip("The maximum damage taken from a crash when speeding at max difficulty.")]
        public int maxCrashDamage = 25;

        int activeSignIndex = -1;        // <-- keep track of which sign we asked for

        bool balanceActive;
        float balanceVal;
        float balanceFailTimer;
        float frozenSpeed;
        float currentRoundDrift;          // per-round sensitivity

        [SerializeField] bool debugLogs = true;   // toggle in Inspector

        private float steeringInput, accelerationInput, driftInput, slowInput;
        // --- End Other Headers ---

        public float Speed => speed;   // expose current forward speed
        public bool IsChangingLane => isChangingLane;
        public bool IsDrifting => isDrifting;
        public bool IsGrounded => isGrounded; // Public accessor for grounded state

        public bool IsBrakePressed => driftInput > 0.1f;   // NEW


        private CinemachineImpulseSource _impulseSource;

        private Vector3 _currentVelocity;
        public Vector3 CurrentVelocity => _currentVelocity;

        bool miniGamePlayedThisZone = false;   // replaces hasTriggered...
        float overspeedTimer = 0f;

        [Header("Driver-change grace (s)")]
       public float driverChangeGrace = 3f;      // inspector-tweakable

       float ignoreOverspeedUntil = 0f;          // runtime timer

        //  ArcadeVehicleController.cs   (inside EnterSpeedLimit)
        public void EnterSpeedLimit(float limitMps, int signIndex)
        {
            activeSpeedLimit = limitMps;
            activeSignIndex = signIndex;

            // Reset per-zone state
            miniGamePlayedThisZone = false;
            overspeedTimer = 0f;

            // Kill any stray gauge if it was left visible for any reason
            if (balanceUI && balanceUI.IsVisible) balanceUI.End();
            balanceActive = false;

            // Speed-limit sign
            if (speedLimitUI)
            {
                speedLimitUI.gameObject.SetActive(true);
                speedLimitUI.Show(signIndex);
            }

            Debug.Log($"ENTER zone  limit={limitMps:0.00}  speed={speed:0.0}");

            // We require a sustained overspeed check inside Update() only.
        }

        void CheckOverspeedImmediate()
        {
            if (activeSpeedLimit <= 0) return;

            float tol = 1f + speedOvershootTolerance;
            bool over = speed > activeSpeedLimit * tol;
            if (debugLogs) Debug.Log($"[Car]  immediate overspeed? {over}");

            if (over && !balanceActive) StartBalanceMiniGame();
        }

        public void ExitSpeedLimit()
        {
            if (activeSpeedLimit <= 0f) return; // guard double-calls

            // Clear zone state
            activeSpeedLimit = 0f;
            activeSignIndex = -1;
            overspeedTimer = 0f;
            // do NOT reset miniGamePlayedThisZone here? we’re leaving the zone anyway
            // but safe to clear:
            miniGamePlayedThisZone = false;
            ignoreOverspeedUntil = 0f;

            // Always hide both UIs
            if (speedLimitUI) speedLimitUI.Show(-1);
            if (balanceUI && balanceUI.IsVisible) balanceUI.End();

            // Ensure flag is off
            balanceActive = false;

            Debug.Log("[SpeedZone]  EXIT (forced UI fade-out)");
        }

        void EnsureGaugeHiddenWhenNotActive()
        {
            if (balanceUI && !balanceActive && balanceUI.IsVisible)
                balanceUI.End();
        }

        void OnDestroy()
        {
            PlayerManager.OnDriverChanged -= HandleDriverChanged;
        }

        void HandleDriverChanged(Passenger oldP, Passenger newP)
        {
            // 1. cancel any running mini-game (that would feel unfair)
            if (balanceActive) EndBalanceMiniGame(true);
            // 2. start a grace timer so the NEXT overspeed isn’t checked too soon
            ignoreOverspeedUntil = Time.time + driverChangeGrace;

            // 3. reset per-zone trackers (otherwise a half-played zone might resume)
            miniGamePlayedThisZone = false;
            overspeedTimer         = 0f;
        }
        // Start remains the same
        void Start()
        {
            if (splineContainer == null)
            {
                Debug.LogError("No SplineContainer on " + name);
                enabled = false;
                return;
            }
            PlayerManager.OnDriverChanged += HandleDriverChanged;
            spline = splineContainer.Spline;
            splineLength = spline.GetLength();

            laneOffsets[0] = -laneOffsetDistance;
            laneOffsets[1] = 0f;
            laneOffsets[2] = +laneOffsetDistance;
            targetOffset = currentOffset = laneOffsets[currentLane];
            _impulseSource = GetComponent<CinemachineImpulseSource>();
            if (engineSound != null)
            {
                engineSound.pitch = minPitch;   // force AudioSource itself
                currentEnginePitch = minPitch;   // internal state in sync
            }
            else
            {
                currentEnginePitch = minPitch;    // still initialise safely
            }

            enginePitchSmoothVelocity = 0f;
            smoothedSpeedRatio = 0f;
            speedRatioSmoothVelocity = 0f;

            if (bodyMesh == null) bodyMesh = transform.GetChild(0);
            if (bodyMesh == null)
                Debug.LogWarning("Body Mesh not assigned for tilt/drift visuals on " + name);

            if (maxCornerAngleForFullDrift <= cornerAngleThreshold)
            {
                Debug.LogWarning("maxCornerAngleForFullDrift > cornerAngleThreshold. Adjusting.");
                maxCornerAngleForFullDrift = cornerAngleThreshold + 10f;
            }
            if (maxDriftYawAngle < minDriftYawAngle)
            {
                Debug.LogWarning("maxDriftYawAngle >= minDriftYawAngle. Adjusting.");
                maxDriftYawAngle = minDriftYawAngle;
            }
            if (maxSpeedFractionForYawEffect < minSpeedFractionForYawEffect)
            {
                Debug.LogWarning("maxSpeedFractionForYawEffect >= minSpeedFractionForYawEffect. Adjusting.");
                maxSpeedFractionForYawEffect = minSpeedFractionForYawEffect + 0.1f;
            }

            // Ensure car starts grounded
            isGrounded = CheckIfGrounded(out _);
        }

        void Update()
        {
            /* 1 – mini-game gate (sustained overspeed only) */
            bool inZone = activeSpeedLimit > 0f;

            // If we’re not in a zone but the gauge somehow shows, kill it.
            if (!inZone) { EnsureGaugeHiddenWhenNotActive(); }
            float dt = Time.deltaTime;

            float tolFactor = 1f + speedOvershootTolerance;
            bool overspeedEligible = inZone && (Time.time >= (ignoreOverspeedUntil + extraGraceAfterSwap));
            bool isOverspeeding = overspeedEligible && (Mathf.Abs(speed) > activeSpeedLimit * tolFactor);

            // Only allow one mini-game per zone
            if (overspeedEligible && !miniGamePlayedThisZone)
            {
                // require sustained overspeed to trigger
                overspeedTimer = isOverspeeding ? overspeedTimer + dt : 0f;

                if (overspeedTimer >= overspeedTriggerTime)
                    StartBalanceMiniGame(); // sets miniGamePlayedThisZone = true
            }

            // If the mini-game is NOT active, make sure the gauge can’t linger
            if (!balanceActive) EnsureGaugeHiddenWhenNotActive();

            // If the mini-game IS active, run it and keep straight-line speed frozen
            if (balanceActive)
            {
                // Fairness: only start “for real” after the UI is fully visible
                bool uiReady = (balanceUI == null) || balanceUI.IsFullyVisible;
                if (uiReady)
                {
                    // enable drift & audio once, right when UI becomes readable
                    if (!isDrifting)
                    {
                        isDrifting = true;
                        if (skidSound && !skidSound.isPlaying) skidSound.Play();
                    }
                    UpdateBalanceMiniGame(dt);
                    speed = frozenSpeed; // freeze during active round
                }
                else
                {
                    // During fade-in: do nothing gameplay-wise (no freeze, no tick)
                    // The UI handles its own fade; we just wait.
                }
            }
            ApplyCornerDrag(dt);
            /*???????????????? 3. normal driving logic continues ???????*/
            bool controlsLocked = Time.time < controlLockUntil;

            /* -------------------------------------------------------------------
               2. zero?out inputs while locked
            ----------------------------------------------------------------------*/
            if (controlsLocked)
            {
                steeringInput = 0f;
                accelerationInput = 0f;
                driftInput = 0f;
                slowInput = 0f;

                // heavy braking
                //speed = Mathf.MoveTowards(speed, 0f, overshootBrakeDecel * dt);
            }

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
            UpdateSkidSound(shouldSkid, dt);
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

        // ==== MINI-GAME CONTROL =================================================
        /*?????????????????? MINI-GAME ??????????????????*/
        void StartBalanceMiniGame()
        {
            /* --------  HOW MUCH DID WE SPEED?  -------- */
            float overshoot = Mathf.Max(0f, Mathf.Abs(speed) - activeSpeedLimit);
            float ratio = (activeSpeedLimit <= 0f) ? 0f : overshoot / activeSpeedLimit;
            _lastOvershootRatio = ratio; // Store the current ratio

            // Now, check if the ratio is high enough to trigger the minigame
            if (ratio < minOvershootRatioToTrigger)
            {
                miniGamePlayedThisZone = true;  // Mark as played so it doesn't try again
                return;                         // Exit the function
            }

            miniGamePlayedThisZone = true;
            overspeedTimer = 0f;
            balanceActive = true;
            frozenSpeed = speed;
            balanceVal = 0f;
            balanceFailTimer = 0f;

            /* --------  DIFFICULTY-SCALED SETTINGS  -------- */
            float diffT = Mathf.Clamp01(ratio / Mathf.Max(0.001f, fullDifficultyAtRatio)); // 0-1


            _lastOvershootRatio = ratio;
            /* --------  DIFFICULTY-SCALED SETTINGS  -------- */
            // pointer auto-drift
            currentRoundDrift = Mathf.Lerp(driftSpeedEasy, driftSpeedHard, diffT);

            // green-zone initial width
            float zoneFrac = Mathf.Lerp(zoneFracEasy, zoneFracHard, diffT);
            float green = Mathf.Clamp(zoneFrac, greenMinWidth, greenMaxWidth);

            /* push params into the gauge */
            if (balanceUI)
            {
                balanceUI.oscillationAmplitude = Mathf.Lerp(oscAmpEasy, oscAmpHard, diffT);
                balanceUI.shrinkRate = Mathf.Lerp(shrinkEasy, shrinkHard, diffT);
                balanceUI.Begin(green);
            }

            isDrifting = false;                     // not yet
            driftDirection = (steeringInput < 0f ? -1 : 1);

            if (debugLogs)
                Debug.Log($"[MiniGame] overshoot {overshoot:0.0} m/s  ratio {ratio:P0}  diff {diffT:0.00}");
        }


        void UpdateBalanceMiniGame(float dt)
        {
            /* ---------- pointer movement ---------- */
            float pressureDelta = accelerationInput - slowInput;

            balanceVal = Mathf.Clamp(
                balanceVal + (pressureDelta * balanceInputPower + // player input
                              currentRoundDrift * Mathf.Sign(balanceVal)) * dt,
                -1f, 1f);

            /* ---------- UI calls ---------- */
            balanceUI.Tick(dt);            // move zone & shrink
            balanceUI.SetPointer(balanceVal);

            /* ---------- fail logic ---------- */
            bool outside = balanceUI.IsOutside(balanceVal);   //  reliable

            if (outside)
            {
                balanceFailTimer += dt;

                // INSTANT CRASH if the pointer hits the absolute edge of the bar
                if (Mathf.Abs(balanceVal) >= balanceFailThresh)
                {
                    EndBalanceMiniGame(false); // CRASH!
                    return; // Exit to avoid running the next check
                }

                // Normal crash after the grace period
                if (balanceFailTimer >= balanceFailGrace)
                {
                    EndBalanceMiniGame(false); // CRASH!
                }
            }
            else
            {
                balanceFailTimer = 0f; // Reset timer when back in the safe zone
            }
        }



        void EndBalanceMiniGame(bool success)
        {
            if (!balanceActive) return;
            miniGamePlayedThisZone = false;
            balanceActive = false;
            balanceUI?.End();
            isDrifting = false;
            if (debugLogs) Debug.Log($"[Car]  MINI-GAME END  success={success}");
            
            if (!success) TriggerSpeedCrash();
        }


        void TriggerSpeedCrash()
        {
            if (TryGetComponent<Damageable>(out Damageable playerDamage))
            {
                // Calculate damage: at least 1, up to maxCrashDamage based on the overshoot ratio.
                int damage = Mathf.Max(1, Mathf.RoundToInt(_lastOvershootRatio * maxCrashDamage));
                playerDamage.InflictDamage(damage);
                Debug.Log($"Crashed while speeding! Dealt {damage} damage.");
            }
            if (Time.time < controlLockUntil) return;
            var fx = FindObjectOfType<SpeedZoneFeedbackFX>();
            if (fx) fx.CrashPulse();
            controlLockUntil = Time.time + overshootLockTime;
            Debug.Log("CRASHED");
            int dir = UnityEngine.Random.value > .5f ? 1 : -1;
            currentLane = dir > 0 ? 2 : 0;
            targetOffset = laneOffsets[currentLane];

            currentDriftYaw = dir * 80;
            if (bodyMesh) bodyMesh.localRotation = Quaternion.Euler(0, 0, -dir * 45);

            speed = frozenSpeed * .4f;
            if (_impulseSource != null)
            {
                _impulseSource.GenerateImpulseWithForce(crashShakeIntensity);
            }
            speedLimitUI?.FlashRed();
            skidSound?.Play();
        }


        // ========================================================================

        private void UpdateSkidSound(bool active, float dt)
        {
            if (skidSound == null) return;

            /* 1. target volume */
            float targetVol = active ? skidMaxVolume : 0f;
            float fadeRate = (skidFadeTime > 0f) ? (skidMaxVolume / skidFadeTime) : 999f;
            skidSound.volume = Mathf.MoveTowards(skidSound.volume, targetVol, fadeRate * dt);

            /* 2. start/stop the clip only when necessary */
            if (!skidSound.isPlaying && skidSound.volume > 0f)
                skidSound.Play();
            else if (skidSound.isPlaying && skidSound.volume <= 0f)
                skidSound.Stop();

            /* 3. gentle pitch variation makes the loop feel alive */
            float desiredPitch = Mathf.Lerp(0.9f, 1.25f, smoothedSpeedRatio);
            skidSound.pitch = desiredPitch;

            wasSkidding = active;   // keep the flag for any other logic that uses it
        }

        public void ProvideInputs(float steer, float accel, float drift, float slow)
        {
            steeringInput = steer; accelerationInput = accel; driftInput = drift; slowInput = slow;
        }

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
            float futureDistance = traveledDistance
                       + Mathf.Sign(speed) * cornerDetectionLookahead
                       //* Mathf.Lerp(1f, 2.5f, Mathf.Abs(speed) / maxSpeed)
                       ;
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
            //Debug.Log("Angle is:" + angle.ToString());
            currentCurvature = Mathf.Abs(angle / (futureDistance - traveledDistance));
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

            if (dt > 0)
            {
                _currentVelocity = (finalPosition - transform.position) / dt;
            }
            // Apply final calculated position
            transform.position = finalPosition;

            // 5. Handle Rotation (Same as before)
            Vector3 forwardDir = worldT.normalized;
            if (forwardDir == Vector3.zero) forwardDir = transform.forward;

            // use road normal when grounded, fall back to spline?up in the air
            Vector3 upDir = (groundHit ? hit.normal : worldUp_Spline).normalized;

            Quaternion baseRotation = Quaternion.LookRotation(forwardDir, upDir);

            /* ------------------ drift yaw stays exactly the same ------------------ */
            float targetDriftYaw = 0f;
            if (balanceActive)
            {
                // Strong forced yaw during mini-game
                targetDriftYaw = 100f * driftDirection;
            }
            else if (isDrifting)
            {
                float cornerIntensity = Mathf.Clamp01(Mathf.InverseLerp(cornerAngleThreshold,
                                                     maxCornerAngleForFullDrift, detectedCornerAngle));
                float cornerBasedYaw = Mathf.Lerp(minDriftYawAngle, maxDriftYawAngle, cornerIntensity);
                float speedScaleFactor = Mathf.Clamp(Mathf.InverseLerp(minSpeedFractionForYawEffect, maxSpeedFractionForYawEffect, smoothedSpeedRatio), 0.4f, 1f);

                float speedScaledYaw = cornerBasedYaw * speedScaleFactor;
                targetDriftYaw = speedScaledYaw * driftDirection;
            }
            currentDriftYaw = Mathf.Lerp(currentDriftYaw, targetDriftYaw,
                             (isDrifting ? driftEntrySpeed : driftExitSpeed) * dt);

            /* NOTE: yaw is applied around the *road normal* so the car keeps hugging the slope */
            Quaternion driftRot = Quaternion.AngleAxis(currentDriftYaw, upDir);
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

        void ApplyCornerDrag(float dt)
        {
            if (detectedCornerAngle < cornerAngleThreshold) return;

            // 0 at threshold --> 1 at maxCornerAngleForFullDrift
            float t = Mathf.Clamp01(Mathf.InverseLerp(
                        cornerAngleThreshold, maxCornerAngleForFullDrift, detectedCornerAngle));

            float allowed = maxSpeed * Mathf.Lerp(1f, cornerSlowFactor, t);

            if (Mathf.Abs(speed) > allowed)
                speed = Mathf.MoveTowards(speed, Mathf.Sign(speed) * allowed, cornerDecel * dt);
        }

    }
}