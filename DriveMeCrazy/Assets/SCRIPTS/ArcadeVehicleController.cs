using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics; // for float3 & math.normalize
using Cinemachine;
using System;
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
        [SerializeField] private int currentLane = 1;
        private float targetOffset, currentOffset;
        private float lastLaneChangeTime = -Mathf.Infinity;
        private float previousSteer = 0f;
        private bool isChangingLane => useSeparateLaneSplines
    ? (laneTrackSwitcher != null && laneTrackSwitcher.IsChanging)
    : Mathf.Abs(currentOffset - targetOffset) > laneSnapThreshold;

        [Header("Lane Tracks (separate splines)")]
        public bool useSeparateLaneSplines = false;
        public SplineLaneTrackSwitcher laneTrackSwitcher;

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


        [Tooltip("If true, lane switching NEVER modifies traveledDistance. Forward motion stays continuous (recommended).")]
        public bool keepForwardDistanceDuringLaneChange = true;

        public event Action LaneChanged;

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

        [Header("Ground Follow Mode")]
        [Tooltip("OFF = spline height/up (NO jitter). ON = probe mesh (can jitter if mesh is noisy).")]
        public bool followGroundMesh = false;

        [Tooltip("Seconds we keep last valid ground hit if probe misses for a frame.")]
        public float groundLostGrace = 0.10f;

        private float _lastGroundHitTime = -999f;
        private RaycastHit _lastGroundHit;
        
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
        [Header("Mini-game � trigger threshold")]
        [Tooltip("Overshoot ratio (0.15 = 15 % past the sign) below which the mini-game never triggers")]
        public float minOvershootRatioToTrigger = 0.15f;

        [Header("Mini-game � difficulty curves")]
        public float shrinkEasy = 0.02f;   // green-zone shrink (frac/s)
        public float shrinkHard = 0.15f;
        public float oscAmpEasy = 0.15f;   // zone oscillation amplitude
        public float oscAmpHard = 0.40f;

        [Header("Mini-game � timing")]
        public float overspeedTriggerTime = 0.40f;  // sustain time before start
        public float extraGraceAfterSwap = 1.0f;   // in addition to driverChangeGrace
                                                   // ---------------------------------------------------------------------
        bool miniGameArmedThisZone = false;   // pending pre-cue armed but not started
        int speedZoneEpoch = 0;              // increments on real zone changes (kills stale coroutines)
        int armedEpoch = -1;
        int armedSignIndex = -1;
        float armedLimitSnapshot = 0f;

        [Header("Mini-game � UX & Pre-cue")]
        [Tooltip("Pre-cue window before the gauge appears (seconds)")]
        public Vector2 preCueRange = new Vector2(0.25f, 0.40f); // NEW
        [Tooltip("Optional UI SFX source for mini-game beeps")]
        public AudioSource uiAudio; // NEW


        [Header("Mini-game � outcomes")]
        [Tooltip("Stay within this NormalizedError to count as centred")]
        [Range(0.05f, 0.5f)] public float perfectCenterThreshold = 0.20f; // NEW
        [Tooltip("Seconds being centred to earn PERFECT")]
        public float perfectHoldTime = 1.25f; // NEW
        [Tooltip("Seconds without going red to PASS")]
        public float passDuration = 2.0f; // NEW
        [Tooltip("Bonus points on PERFECT (uses PlayerManager if present)")]
        public int perfectBonusPoints = 100; // NEW


        [Header("Corner Slow-Down")]
        [Tooltip("Speed kept in a 90-degree hair-pin (0.0-1.0)")]
        [Range(0.2f, 1f)] public float cornerSlowFactor = 0.55f;
        [Tooltip("Rate car scrubs speed in corners (m/s?)")]
        public float cornerDecel = 12f;


        [Header("Ground Smoothing")]
        public float groundSmoothTime = 0.06f;     // 0.04�0.10 tipik
        public float maxSnapY = 0.75f;             // nagy l�pcs�n snapel, ne sim�tson �r�kk�
        public float groundProbeRadius = 0.25f;    // 0.2�0.35 tipik

        private float _ySmoothVel;
        private float _smoothedY;
        private Vector3 _smoothedUp;
        public float normalSmoothSpeed = 12f;

        private float _lastOvershootRatio = 0f;

        // Add this inside the [Header("Crash Effects")] section in the Inspector
        [Tooltip("The maximum damage taken from a crash when speeding at max difficulty.")]
        public int maxCrashDamage = 25;

        [Header("Control Lock")]
        public bool allowBrakeDuringLock = true;
        public float stumbleLockTime = 0.15f; // 0.7 helyett (tweakeld)

        int activeSignIndex = -1;        // <-- keep track of which sign we asked for

        bool balanceActive;
        float balanceVal;
        float balanceFailTimer;
        float frozenSpeed;
        float currentRoundDrift;          // per-round sensitivity
        float insideStreakTimer;   // NEW
        float centreStreakTimer;   // NEW
        bool readyPulseShown;      // NEW
        Coroutine armCR;           // NEW

        [SerializeField] bool debugLogs = true;   // toggle in Inspector

        private float steeringInput, accelerationInput, driftInput, slowInput;
        // --- End Other Headers ---

        public float Speed => speed;   // expose current forward speed
        public bool IsChangingLane => isChangingLane;
        public bool IsDrifting => isDrifting;
        public bool IsGrounded => isGrounded; // Public accessor for grounded state

        
        public int CurrentLane => currentLane; // For Warning Signal & Tracking

        public bool IsBrakePressed => driftInput > 0.1f;   // NEW


        private CinemachineImpulseSource _impulseSource;

        private Vector3 _currentVelocity;
        public Vector3 CurrentVelocity => _currentVelocity;

        bool miniGamePlayedThisZone = false;   // replaces hasTriggered...
        float overspeedTimer = 0f;

        [Header("Driver-change grace (s)")]
       public float driverChangeGrace = 3f;      // inspector-tweakable

       float ignoreOverspeedUntil = 0f;          // runtime timer

        [Header("Mini-game � corner/zone robustness")]
        [Tooltip("Keep the zone logically active this long after leaving the trigger.")]
        public float stickyExitSeconds = 0.75f;

        [Tooltip("Once the round begins, guarantee at least this much visible time.")]
        public float minRoundVisible = 1.0f;

        [Tooltip("If the remaining window is shorter than this, skip pre-cue & pop instantly.")]
        public float instantStartShortWindow = 0.50f;

        [Tooltip("When sticky time ends, auto decide: PASS if inside, FAIL if near edge.")]
        public bool forceOutcomeOnExit = true;

        [Range(0.6f, 1f), Tooltip("Edge threshold for forced FAIL at sticky end.")]
        public float forceFailEdgeThresh = 0.85f;

        // zone timing
        float zoneEnterTime = -1f;
        bool zoneExiting = false;
        float zoneStickyUntil = -1f;

        // round timing
        float roundStartTime = -1f;

        // temp UI tuning for instant pop
        float _origFadeInSpeed = -1f;

        public float TrackTNormalized => (splineLength > 0f) ? (traveledDistance / splineLength) : 0f;

        [Header("Mini-game � AI adaptive mapping")]
        [Tooltip("Blend 0=overspeed-only  1=director-only")]
        [Range(0f, 1f)] public float directorInfluence = 0.6f;

        [Tooltip("Scale the driver�s pedal effect on the pointer.")]
        public Vector2 inputPowerRange = new Vector2(1.10f, 1.60f);   // easy .. hard

        [Tooltip("How �sticky� the pointer�s self-drift is.")]
        public float driftSpeedEasy = 0.15f;     // (already existed above � keep values)
        public float driftSpeedHard = 0.60f;

        [Tooltip("Initial green-zone width as a fraction of the bar.")]
        public float zoneFracEasy = 0.60f;       // (already existed above � keep)
        public float zoneFracHard = 0.18f;

        [Tooltip("Seconds allowed in red before a stumble/crash.")]
        public Vector2 failGraceRange = new Vector2(1.20f, 0.60f);

        [Tooltip("Edge clamp that force-crashes when |val| ? thresh.")]
        public Vector2 failEdgeRange = new Vector2(0.95f, 0.85f);

        [Tooltip("Seconds without red to PASS.")]
        public Vector2 passDurationRange = new Vector2(1.60f, 2.30f);

        [Tooltip("Seconds centred to PERFECT.")]
        public Vector2 perfectHoldRange = new Vector2(1.00f, 1.60f);

        [Tooltip("How close to centre counts as �centred�.")]
        public Vector2 perfectCenterRange = new Vector2(0.28f, 0.16f);

        // ---- Per-round, locked tuning (NEW) ----
        float rt_inputPower;
        float rt_drift;
        float rt_greenWidth;
        float rt_failGrace;
        float rt_failEdge;
        float rt_passDuration;
        float rt_perfectHold;
        float rt_centerThresh;

        public event Action CrashHappened;

        // +1 = jobbra v�lt, -1 = balra v�lt, 0 = nincs v�lt�s

        public int LaneChangeDirection
        {
            get
            {
                if (useSeparateLaneSplines && laneTrackSwitcher != null && laneTrackSwitcher.IsChanging)
                {
                    int dir = (int)Mathf.Sign(laneTrackSwitcher.TargetLane - laneTrackSwitcher.CurrentLane);

                    TelemetryLogger.Instance?.RegisterDriverInputThisFrame();

                    return dir;
                }

                if (!useSeparateLaneSplines)
                {
                    float d = targetOffset - currentOffset;
                    if (Mathf.Abs(d) <= laneSnapThreshold) return 0;

                    int dir = (int)Mathf.Sign(d);

                    TelemetryLogger.Instance?.RegisterDriverInputThisFrame();

                    return dir;
                }

                return 0;
            }
        }

        bool IsOverspeedingNow(float absSpeed)
        {
            if (activeSpeedLimit <= 0f) return false;
            float tolFactor = 1f + speedOvershootTolerance;
            // + tiny epsilon to avoid float jitter edge cases
            return absSpeed > (activeSpeedLimit * tolFactor + 0.0001f);
        }

        float OvershootRatioNow(float absSpeed)
        {
            if (activeSpeedLimit <= 0f) return 0f;
            float over = Mathf.Max(0f, absSpeed - activeSpeedLimit);
            return over / activeSpeedLimit;
        }

        void CancelPendingArm(string reason, bool fromCoroutine = false)
        {
            // if called from inside the coroutine, DON'T StopCoroutine(self)
            if (!fromCoroutine && armCR != null)
                StopCoroutine(armCR);

            armCR = null;
            miniGameArmedThisZone = false;
            armedEpoch = -1;
            armedSignIndex = -1;
            armedLimitSnapshot = 0f;

            // no �zone consumed� on cancel
            overspeedTimer = 0f;

            // stop any pre-cue beep
            if (speedLimitUI && speedLimitUI.sfx) speedLimitUI.sfx.Stop();

            if (debugLogs) Debug.Log($"[SpeedZone] ARM CANCELED: {reason}");
        }

        float ComputeDifficultyBlend(float overshootRatio)
        {
            // base difficulty from overspeed amount (unchanged)
            float baseT = Mathf.Clamp01(overshootRatio / Mathf.Max(0.001f, fullDifficultyAtRatio));

            // precision channel from the multi-channel director
            float dirT = 0.5f;
            if (DifficultyDirector.Instance != null)
                dirT = Mathf.Clamp01(DifficultyDirector.Instance.Current.precision);

            // final blend (your existing directorInfluence 0..1)
            return Mathf.Clamp01(Mathf.Lerp(baseT, dirT, directorInfluence));
        }

        void LockRoundTuning(float t)
        {
            // existing mapping
            rt_drift = Mathf.Lerp(driftSpeedEasy, driftSpeedHard, t);
            float zoneFrac = Mathf.Lerp(zoneFracEasy, zoneFracHard, t);
            rt_greenWidth = Mathf.Clamp(zoneFrac, greenMinWidth, greenMaxWidth);
            rt_inputPower = Mathf.Lerp(inputPowerRange.x, inputPowerRange.y, t);
            rt_failGrace = Mathf.Lerp(failGraceRange.x, failGraceRange.y, t);
            rt_failEdge = Mathf.Lerp(failEdgeRange.x, failEdgeRange.y, t);
            rt_passDuration = Mathf.Lerp(passDurationRange.x, passDurationRange.y, t);
            rt_perfectHold = Mathf.Lerp(perfectHoldRange.x, perfectHoldRange.y, t);
            rt_centerThresh = Mathf.Lerp(perfectCenterRange.x, perfectCenterRange.y, t);

            // NEW: bias by punishment channel (0 easy -> 1 hard)
            float punish = 0.5f;
            if (DifficultyDirector.Instance) punish = Mathf.Clamp01(DifficultyDirector.Instance.Current.punishment);

            // more punishment ? shorter grace, tighter edge, longer pass time
            rt_failGrace = Mathf.Lerp(rt_failGrace, Mathf.Lerp(failGraceRange.x, failGraceRange.y, 1f), punish);
            rt_failEdge = Mathf.Lerp(rt_failEdge, Mathf.Lerp(failEdgeRange.x, failEdgeRange.y, 1f), punish);
            rt_passDuration = Mathf.Lerp(rt_passDuration, Mathf.Lerp(passDurationRange.x, passDurationRange.y, 1f), punish);
        }


        void AbortAllSpeedZoneStuff()
        {
            // stop pending "arm" (pre-cue) so it can�t fire
            if (armCR != null) { StopCoroutine(armCR); armCR = null; }

            // cut any pre-cue beep immediately
            if (speedLimitUI && speedLimitUI.sfx) speedLimitUI.sfx.Stop();

            // hide UIs silently
            if (speedLimitUI) speedLimitUI.Show(-1);
            if (balanceUI && (balanceActive || balanceUI.IsVisible)) balanceUI.End();

            // clear flags so nothing re-triggers
            balanceActive = false;
            miniGamePlayedThisZone = false;
            readyPulseShown = false;
            overspeedTimer = 0f;
        }

        public void EnterSpeedLimit(float limitMps, int signIndex)
        {
            if (!PlayerJoinManager.IsRaceStarted) return;

            // detect �bounce enter� (multiple colliders / tiny re-enter) -> don�t nuke state
            int prevSign = activeSignIndex;
            float prevLimit = activeSpeedLimit;
            bool wasInZone = activeSpeedLimit > 0f;

            // Always update metadata FIRST
            activeSpeedLimit = limitMps;
            activeSignIndex = signIndex;

            bool bounceEnter =
                wasInZone &&
                prevSign == signIndex &&
                Mathf.Abs(prevLimit - limitMps) < 0.01f &&
                (Time.time - zoneEnterTime) < 0.25f;

            // real zone entry -> kill stale arming and reset trackers
            if (!bounceEnter)
            {
                speedZoneEpoch++;               // INVALIDATES any pending coroutine from older zone
                CancelPendingArm("new zone entered");

                miniGamePlayedThisZone = false; // allow a fresh trigger in this zone
                miniGameArmedThisZone = false;
                overspeedTimer = 0f;
                readyPulseShown = false;
            }

            // Mark zone entry and cancel any pending exit
            zoneEnterTime = Time.time;
            zoneExiting = false;
            zoneStickyUntil = -1f;

            // UI sign
            if (speedLimitUI)
            {
                speedLimitUI.gameObject.SetActive(true);
                speedLimitUI.Show(signIndex);
            }

            Debug.Log($"[SpeedZone] ENTER sign={signIndex}  limit={limitMps:0.00}m/s  speed={speed:0.00}");
        }


        public void ExitSpeedLimit(int signIndex)
        {
            if (signIndex != activeSignIndex)
            {
                if (debugLogs)
                    Debug.LogWarning($"[SpeedZone] EXIT IGNORED: got={signIndex} active={activeSignIndex} limit={activeSpeedLimit:0.00}");
                return;
            }

            if (activeSpeedLimit <= 0f && !zoneExiting) return;

            zoneExiting = true;
            zoneStickyUntil = Time.time + stickyExitSeconds;

            Debug.Log($"[SpeedZone] EXIT (sticky) sign={signIndex} for {stickyExitSeconds:0.00}s");
        }


        // Optional legacy wrapper (ha m�shol h�vod param�ter n�lk�l)
        public void ExitSpeedLimit()
        {
            ExitSpeedLimit(activeSignIndex);
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
            // Cancel any pending "arm" coroutine (pre-cue) so it doesn't pop after swap
            if (armCR != null) { StopCoroutine(armCR); armCR = null; }

            // If a round is running, end it as a neutral PASS (no penalty, no crash)
            if (balanceActive)
            {
                EndBalanceMiniGame(MiniGameOutcome.Pass, false);
            }
            else if (balanceUI && balanceUI.IsVisible)
            {
                // If UI was up but not 'active', hide it cleanly
                balanceUI.End();
            }

            // Start grace so the next driver isn't insta-punished
            ignoreOverspeedUntil = Time.time + driverChangeGrace;

            // Clear per-zone trackers
            miniGamePlayedThisZone = false;
            overspeedTimer = 0f;
            readyPulseShown = false;
            isDrifting = false;
        }

        // Start remains the same
        void Start()
        {
            PlayerManager.OnDriverChanged += HandleDriverChanged;
            _smoothedY = transform.position.y;
            _smoothedUp = transform.up;
            _ySmoothVel = 0f;

            if (useSeparateLaneSplines)
            {
                if (laneTrackSwitcher == null)
                    laneTrackSwitcher = GetComponent<SplineLaneTrackSwitcher>();

                if (laneTrackSwitcher == null || laneTrackSwitcher.LaneCount == 0)
                {
                    Debug.LogError("Separate lane splines ON, but laneTrackSwitcher missing or laneTracks empty on " + name);
                    enabled = false;
                    return;
                }

                currentLane = Mathf.Clamp(currentLane, 0, laneTrackSwitcher.LaneCount - 1);

                laneTrackSwitcher.laneChangeCooldown = laneChangeCooldown;
                laneTrackSwitcher.laneChangeDuration = Mathf.Max(0.05f, laneOffsetDistance / Mathf.Max(0.01f, laneChangeSpeed));

                laneTrackSwitcher.Initialise(currentLane);

                splineContainer = laneTrackSwitcher.ActiveLaneContainer;
                spline = laneTrackSwitcher.ActiveSpline;
                splineLength = laneTrackSwitcher.ActiveLength;

                currentOffset = targetOffset = 0f;
            }
            else
            {
                if (splineContainer == null)
                {
                    Debug.LogError("No SplineContainer on " + name);
                    enabled = false;
                    return;
                }

                spline = splineContainer.Spline;
                splineLength = spline.GetLength();

                laneOffsets[0] = -laneOffsetDistance;
                laneOffsets[1] = 0f;
                laneOffsets[2] = +laneOffsetDistance;
                targetOffset = currentOffset = laneOffsets[currentLane];
            }
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
            if (!PlayerJoinManager.IsRaceStarted)
            {
                if (activeSpeedLimit > 0f || balanceActive || armCR != null || (balanceUI && balanceUI.IsVisible))
                    AbortAllSpeedZoneStuff();

                activeSpeedLimit = 0f; // ensures inZone == false below
            }

            bool inZone = (activeSpeedLimit > 0f)
           || (zoneExiting && Time.time < zoneStickyUntil)
           || balanceActive; // keep logic alive during an active round
            if (!inZone) { EnsureGaugeHiddenWhenNotActive(); }

            float dt = Time.deltaTime;
            if (useSeparateLaneSplines && laneTrackSwitcher != null)
            {
                laneTrackSwitcher.laneChangeCooldown = laneChangeCooldown;
                laneTrackSwitcher.laneChangeDuration = Mathf.Max(0.05f, laneOffsetDistance / Mathf.Max(0.01f, laneChangeSpeed));

                float td = traveledDistance; // copy � switcher can do internal math without stealing forward distance
                laneTrackSwitcher.Tick(dt, ref td, out bool laneFinished);

                // Only apply the switcher's distance corrections if you explicitly want the old behavior
                if (!keepForwardDistanceDuringLaneChange)
                    traveledDistance = td;

                if (laneFinished)
                    currentLane = laneTrackSwitcher.CurrentLane;
                if (laneFinished)
                    currentLane = laneTrackSwitcher.CurrentLane;

                splineContainer = laneTrackSwitcher.ActiveLaneContainer;
                spline = laneTrackSwitcher.ActiveSpline;
                splineLength = laneTrackSwitcher.ActiveLength;
            }

            float tolFactor = 1f + speedOvershootTolerance;


            bool overspeedEligible = (activeSpeedLimit > 0f)
                                  && inZone
                                  && !balanceActive
                                  && (Time.time >= (ignoreOverspeedUntil + extraGraceAfterSwap));

            if (overspeedEligible && !miniGamePlayedThisZone)
            {
                float absSpeed = Mathf.Abs(speed);
                bool isOverspeeding = IsOverspeedingNow(absSpeed);
                float ratioNow = OvershootRatioNow(absSpeed);

                // If we were armed but player dropped back under -> cancel pending start
                if (armCR != null && (!isOverspeeding || ratioNow < minOvershootRatioToTrigger))
                    CancelPendingArm("dropped under threshold");

                // Timer ONLY counts if overspeed is real AND ratio is strong enough
                if (!miniGameArmedThisZone)
                {
                    if (isOverspeeding && ratioNow >= minOvershootRatioToTrigger)
                        overspeedTimer += dt;
                    else
                        overspeedTimer = 0f;

                    if (overspeedTimer >= overspeedTriggerTime)
                    {
                        overspeedTimer = 0f;
                        StartBalanceMiniGame(ratioNow);
                    }
                }
            }
            else
            {
                // outside eligibility -> don�t accumulate
                overspeedTimer = 0f;
            }

            // If the mini-game is active, wait for readability then run
            if (balanceActive)
            {
                bool uiReady = (balanceUI == null) || balanceUI.IsFullyVisible;

                if (uiReady)
                {
                    if (!readyPulseShown && balanceUI)
                    {
                        readyPulseShown = true;
                        balanceUI.PlayReadyPulse(); // NEW: one-beat READY
                    }

                    if (!isDrifting)
                    {
                        isDrifting = true;
                        if (skidSound && !skidSound.isPlaying) skidSound.Play();
                    }

                    UpdateBalanceMiniGame(dt);
                    speed = frozenSpeed; // lock straight-line speed while playing
                }
                // else: do nothing gameplay-wise during fade-in
            }

            // --- Robust end-of-zone handling ---
            if (balanceActive)
            {
                // If sticky window is ending, but round hasn�t been visible long enough, extend sticky
                float visibleSoFar = Mathf.Max(0f, Time.time - roundStartTime);
                if (zoneExiting && Time.time >= zoneStickyUntil && visibleSoFar < minRoundVisible)
                {
                    zoneStickyUntil = Time.time + (minRoundVisible - visibleSoFar);
                }

                // If sticky truly ended now, force a clean outcome
                if (zoneExiting && Time.time >= zoneStickyUntil)
                {
                    if (forceOutcomeOnExit)
                    {
                        bool outsideNow = balanceUI ? balanceUI.IsOutside(balanceVal) : false;
                        bool edgeFail = Mathf.Abs(balanceVal) >= (rt_failEdge > 0f ? rt_failEdge : forceFailEdgeThresh);

                        if (edgeFail) { EndBalanceMiniGame(MiniGameOutcome.Fail, fullCrash: true); }
                        else if (outsideNow) { EndBalanceMiniGame(MiniGameOutcome.Fail, fullCrash: false); }
                        else
                        {
                            // reward good control
                            if (centreStreakTimer >= perfectHoldTime)
                                EndBalanceMiniGame(MiniGameOutcome.Perfect, fullCrash: false);
                            else
                                EndBalanceMiniGame(MiniGameOutcome.Pass, fullCrash: false);
                        }
                    }
                    else
                    {
                        EndBalanceMiniGame(MiniGameOutcome.Pass, fullCrash: false);
                    }
                }
            }
            else
            {
                // If no round is active and sticky elapsed, finalize true exit now
                if (zoneExiting && Time.time >= zoneStickyUntil)
                {
                    // Now we truly leave the zone
                    activeSpeedLimit = 0f;
                    activeSignIndex = -1;
                    overspeedTimer = 0f;
                    miniGamePlayedThisZone = false;
                    ignoreOverspeedUntil = 0f;
                    zoneExiting = false;

                    if (speedLimitUI) speedLimitUI.Show(-1);
                    if (balanceUI && balanceUI.IsVisible) balanceUI.End();

                    Debug.Log("[SpeedZone] sticky ended -> EXIT finalized");
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

                if (!allowBrakeDuringLock)
                    slowInput = 0f;   // ha t�nyleg full freeze kell
                                      // ha allowBrakeDuringLock=true: slowInput marad, teh�t azonnal tudsz f�kezni
            }

            // Speed Calculation
            if (balanceActive)
            {
                // Mini-game alatt a speed legyen beton fix.
                speed = frozenSpeed;
            }
            else
            {
                speed += accelerationInput * acceleration * dt;

                if (slowInput > 0f)
                    speed = Mathf.MoveTowards(speed, 0f, slowInput * brakeDeceleration * dt);
                else if (Mathf.Approximately(accelerationInput, 0f))
                    speed = Mathf.MoveTowards(speed, 0f, decelerationRate * dt);

                speed = Mathf.Clamp(speed, -maxSpeed, maxSpeed);

                // Corner drag csak norm�l vezet�sn�l
                ApplyCornerDrag(dt);
            }

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
            if (!useSeparateLaneSplines)
                currentOffset = Mathf.MoveTowards(currentOffset, targetOffset, laneChangeSpeed * dt);


            // --- Movement and physics update ---
            MoveAndHandlePhysics(dt); // New method to handle movement and jumps
            // --- --- --- ---

            UpdateBodyTilt(dt); // Update visual tilt

            if (SkillEstimator.Instance != null)
            {
                float cornerSmoothSample = -1f;      // -1 = ignore
                float laneJitterSample = -1f;      // -1 = ignore
                float overspeedSample = -1f;      // -1 = ignore

                // ----- Corner smoothness (0..1, HIGH = good) -----
                // Csak akkor �rt�kel�nk kanyart, ha t�nyleg kanyarban vagyunk:
                bool inCorner = detectedCornerAngle > cornerAngleThreshold && Mathf.Abs(speed) > 0.1f;
                if (inCorner && splineLength > 0.01f)
                {
                    // Corner intenzit�s: thresholdt�l maxCornerAngleForFullDrift-ig normaliz�lva
                    float cornerIntensity = Mathf.Clamp01(
                        Mathf.InverseLerp(cornerAngleThreshold, maxCornerAngleForFullDrift, detectedCornerAngle));

                    // Speed ar�ny (0..1)
                    float speedFrac = Mathf.Clamp01(Mathf.Abs(speed) / Mathf.Max(0.01f, maxSpeed));

                    // Ide�lis speed: enyhe kanyarban magasabb, �lesben alacsonyabb
                    // pl. intensity=0  -> ~0.9
                    //     intensity=1  -> ~0.4
                    float idealSpeedFrac = Mathf.Lerp(0.9f, 0.4f, cornerIntensity);

                    // Hiba a speed-ben
                    float speedError = Mathf.Clamp01(Mathf.Abs(speedFrac - idealSpeedFrac));

                    // CornerSmooth: 1=perfekt, 0=sz�tcs�szott
                    cornerSmoothSample = 1f - speedError;
                }

                // ----- Lane jitter (0..1, HIGH = bad) -----
                // Ha nagy sebess�gn�l, lane v�lt�s n�lk�l �lland�an piszk�lja a korm�nyt,
                // az jitter �s b�ntetj�k.
                if (Mathf.Abs(speed) > maxSpeed * 0.2f && !isChangingLane && !balanceActive)
                {
                    // steeringInput eleve -1..1
                    laneJitterSample = Mathf.Clamp01(Mathf.Abs(steeringInput));
                }

                // ----- Overspeed control (0..1, HIGH = good) -----
                // Ha van akt�v speed limit, akkor azt n�zz�k mennyire l�pi t�l.
                if (activeSpeedLimit > 0f && PlayerJoinManager.IsRaceStarted)
                {
                    float limit = activeSpeedLimit;
                    float overshoot = Mathf.Max(0f, Mathf.Abs(speed) - limit);
                    float ratio = (limit <= 0.001f) ? 0f : overshoot / limit;

                    // 0 = nincs overspeed, 1 = fullDifficultyAtRatio vagy felette
                    float t = Mathf.Clamp01(ratio / Mathf.Max(0.001f, fullDifficultyAtRatio));
                    overspeedSample = 1f - t;   // 1 = teljesen kontroll�lt, 0 = nagyon t�ll�pi
                }

                SkillEstimator.Instance.OnTrajectorySample(
                    cornerSmoothSample,
                    laneJitterSample,
                    overspeedSample);
            }
            previousSteer = steeringInput;
        }

        // ==== MINI-GAME CONTROL =================================================
        /*?????????????????? MINI-GAME ??????????????????*/
        void StartBalanceMiniGame(float ratioNow)
        {
            if (!PlayerJoinManager.IsRaceStarted) return;
            if (balanceActive) return;
            if (armCR != null) return;
            if (miniGamePlayedThisZone || miniGameArmedThisZone) return;

            // still must meet threshold at ARM time
            if (ratioNow < minOvershootRatioToTrigger) return;

            // arm snapshot
            miniGameArmedThisZone = true;
            armedEpoch = speedZoneEpoch;
            armedSignIndex = activeSignIndex;
            armedLimitSnapshot = activeSpeedLimit;

            // store for crash damage scaling if needed
            _lastOvershootRatio = ratioNow;

            // schedule pre-cue -> but BEGIN will re-check again
            armCR = StartCoroutine(ArmMiniGameThenBegin());
        }

        // keep compatibility if you still call StartBalanceMiniGame() somewhere
        void StartBalanceMiniGame()
        {
            float absSpeed = Mathf.Abs(speed);
            float ratioNow = OvershootRatioNow(absSpeed);
            StartBalanceMiniGame(ratioNow);
        }


        System.Collections.IEnumerator ArmMiniGameThenBegin()
        {
            if (!PlayerJoinManager.IsRaceStarted) yield break;

            // pre-cue wait
            float pre = UnityEngine.Random.Range(preCueRange.x, preCueRange.y);

            // If we�re already exiting and the pre-cue wouldn�t fit, skip it
            if (zoneExiting && (Time.time + pre > zoneStickyUntil))
                pre = 0f;

            // tiny remaining window -> pop instantly
            float remainingWindow = (zoneExiting ? Mathf.Max(0f, zoneStickyUntil - Time.time) : 999f);
            bool instant = remainingWindow <= instantStartShortWindow;
            if (instant) pre = 0f;

            if (speedLimitUI && pre > 0f) speedLimitUI.PlayPreCue(pre);

            // Make sure sticky window can contain the minimum visible time AFTER pre-cue
            if (zoneExiting)
                zoneStickyUntil = Mathf.Max(zoneStickyUntil, Time.time + pre + minRoundVisible);

            // wait
            float t = 0f;
            while (t < pre) { t += Time.deltaTime; yield return null; }

            if (!PlayerJoinManager.IsRaceStarted) yield break;

            // -------------------- HARD RECHECK (THIS IS THE BULLETPROOF PART) --------------------
            // 1) zone/epoch changed? -> cancel
            if (armedEpoch != speedZoneEpoch)
            {
                CancelPendingArm("epoch changed", fromCoroutine: true);
                yield break;
            }

            // 2) sign changed? -> cancel
            if (armedSignIndex != activeSignIndex)
            {
                CancelPendingArm("sign changed", fromCoroutine: true);
                yield break;
            }

            // 3) limit changed? -> cancel
            if (Mathf.Abs(armedLimitSnapshot - activeSpeedLimit) > 0.01f || activeSpeedLimit <= 0f)
            {
                CancelPendingArm("limit changed/invalid", fromCoroutine: true);
                yield break;
            }

            // 4) still overspeeding NOW? -> otherwise cancel (THIS FIXES �30-n�l is bedobja� feeling)
            float absSpeed = Mathf.Abs(speed);
            float ratioNow = OvershootRatioNow(absSpeed);

            if (!IsOverspeedingNow(absSpeed) || ratioNow < minOvershootRatioToTrigger)
            {
                CancelPendingArm("no longer overspeeding at begin", fromCoroutine: true);
                yield break;
            }

            // -------------------- BEGIN ROUND NOW --------------------
            // lock per-round tuning at BEGIN time (not earlier)
            frozenSpeed = speed;
            balanceVal = 0f;
            balanceFailTimer = 0f;
            insideStreakTimer = 0f;
            centreStreakTimer = 0f;
            readyPulseShown = false;

            float diffT = ComputeDifficultyBlend(ratioNow);
            LockRoundTuning(diffT);

            // instant pop tuning
            if (instant && balanceUI)
            {
                if (_origFadeInSpeed < 0f) _origFadeInSpeed = balanceUI.fadeInSpeed;
                balanceUI.fadeInSpeed = 99f;
            }

            // show gauge
            if (balanceUI) balanceUI.Begin(rt_greenWidth);

            roundStartTime = Time.time;
            balanceActive = true;
            currentRoundDrift = rt_drift;
            isDrifting = false;

            // NOW we consume this zone�s mini-game (only once it actually begins)
            miniGamePlayedThisZone = true;
            miniGameArmedThisZone = false;

            armCR = null;
        }



        void UpdateBalanceMiniGame(float dt)
        {
            // pointer movement (ADAPTIVE input power)
            float pressureDelta = accelerationInput - slowInput;
            balanceVal = Mathf.Clamp(
                balanceVal + (pressureDelta * rt_inputPower
                              + currentRoundDrift * Mathf.Sign(balanceVal)) * dt,
                -1f, 1f);

            // UI calls
            balanceUI.Tick(dt);
            balanceUI.SetPointer(balanceVal);

            // Fail checks (ADAPTIVE thresholds)
            bool outside = balanceUI.IsOutside(balanceVal);
            if (outside)
            {
                insideStreakTimer = 0f;
                centreStreakTimer = 0f;
                balanceFailTimer += dt;

                // Edge slam = full crash
                if (Mathf.Abs(balanceVal) >= rt_failEdge)
                {
                    EndBalanceMiniGame(MiniGameOutcome.Fail, fullCrash: true);
                    return;
                }

                if (balanceFailTimer >= rt_failGrace)
                {
                    EndBalanceMiniGame(MiniGameOutcome.Fail, fullCrash: false);
                    return;
                }
            }
            else
            {
                balanceFailTimer = 0f;

                // Track pass / perfect streaks (ADAPTIVE windows)
                insideStreakTimer += dt;

                // If your UI exposes NormalizedError, keep using it
                if (balanceUI.NormalizedError <= rt_centerThresh)
                    centreStreakTimer += dt;
                else
                    centreStreakTimer = 0f;

                if (centreStreakTimer >= rt_perfectHold)
                {
                    EndBalanceMiniGame(MiniGameOutcome.Perfect, fullCrash: false);
                    return;
                }

                if (insideStreakTimer >= rt_passDuration)
                {
                    EndBalanceMiniGame(MiniGameOutcome.Pass, fullCrash: false);
                    return;
                }
            }
        }


        void EndBalanceMiniGame(MiniGameOutcome outcome, bool fullCrash)
        {
            if (!balanceActive) return;

            balanceActive = false;
            //miniGamePlayedThisZone = false;
            isDrifting = false;

            // party feedback
            if (balanceUI) balanceUI.PlayOutcome(outcome);
            balanceUI?.End();

            // scoring / penalties
            switch (outcome)
            {
                case MiniGameOutcome.Perfect:
                    if (SkillEstimator.Instance) SkillEstimator.Instance.OnMiniGamePerfect();
                    // small score plus
                    var pm = PlayerManager.Instance;
                    if (pm != null)
                    {
                        pm.CurrentDriver?.IncrementPoints(perfectBonusPoints);
                    }
                    // feel-good micro impulse (optional)
                    if (_impulseSource != null)
                        _impulseSource.GenerateImpulseWithForce(Mathf.Max(0.2f, crashShakeIntensity * 0.25f));
                    break;

                case MiniGameOutcome.Pass:
                    if (SkillEstimator.Instance) SkillEstimator.Instance.OnMiniGamePass();
                    // no penalty, no crash
                    break;

                case MiniGameOutcome.Fail:
                    if (SkillEstimator.Instance) SkillEstimator.Instance.OnMiniGameFail(fullCrash);
                    if (fullCrash) TriggerSpeedCrash();   // edge slam
                    else TriggerSpeedStumble(); // mild spin/slow
                    break;
            }
            if (_origFadeInSpeed > 0f && balanceUI)
            {
                balanceUI.fadeInSpeed = _origFadeInSpeed;
                _origFadeInSpeed = -1f;
            }
            if (debugLogs) Debug.Log($"[Car] MINI-GAME END ? {outcome}");
        }

        void TriggerSpeedStumble()
        {
            if (!PlayerJoinManager.IsRaceStarted) return; // NEW
            // tiny time loss
            controlLockUntil = Time.time + stumbleLockTime;

            int dir = UnityEngine.Random.value > .5f ? 1 : -1;

            // gentle lane nudge (don�t swap lanes)
            currentDriftYaw = dir * 35f;
            if (bodyMesh) bodyMesh.localRotation = Quaternion.Euler(0, 0, -dir * 15f);

            // speed scrub (much softer than crash)
            speed = frozenSpeed * 0.85f;

            if (_impulseSource != null)
                _impulseSource.GenerateImpulseWithForce(crashShakeIntensity * 0.35f);

            // fun little blink so spectators notice
            speedLimitUI?.FlashRed();
            skidSound?.Play();
        }

        void TriggerSpeedCrash()
        {
            if (!PlayerJoinManager.IsRaceStarted) return; 
            if (TryGetComponent<Damageable>(out Damageable playerDamage))
            {
                // Calculate damage: at least 1, up to maxCrashDamage based on the overshoot ratio.
                int damage = Mathf.Max(1, Mathf.RoundToInt(_lastOvershootRatio * maxCrashDamage));
                playerDamage.InflictDamage(damage);
                Debug.Log($"Crashed while speeding! Dealt {damage} damage.");
            }
            if (Time.time < controlLockUntil) return;
            CrashHappened?.Invoke();
            var fx = FindObjectOfType<SpeedZoneFeedbackFX>();
            if (fx) fx.CrashPulse();
            controlLockUntil = Time.time + overshootLockTime;
            Debug.Log("CRASHED");
            int dir = UnityEngine.Random.value > .5f ? 1 : -1;
            currentLane = dir > 0 ? 2 : 0;

            if (useSeparateLaneSplines && laneTrackSwitcher != null)
            {
                laneTrackSwitcher.ForceSetLane(currentLane, ref traveledDistance);
                splineContainer = laneTrackSwitcher.ActiveLaneContainer;
                spline = laneTrackSwitcher.ActiveSpline;
                splineLength = laneTrackSwitcher.ActiveLength;
                currentOffset = targetOffset = 0f;
            }
            else
            {
                targetOffset = laneOffsets[currentLane];
            }

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
            if (Time.timeScale <= 0.0001f) return;

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
            int maxLaneIndex = useSeparateLaneSplines && laneTrackSwitcher != null
    ? (laneTrackSwitcher.LaneCount - 1)
    : 2;

            // tap logic maradhat, csak a bounds legyen dinamikus
            if (tapL && currentLane < maxLaneIndex) newLane++;
            if (tapR && currentLane > 0) newLane--;

            if (newLane != currentLane)
            {
                if (useSeparateLaneSplines && laneTrackSwitcher != null)
                {
                    if (laneTrackSwitcher.RequestLane(newLane, traveledDistance, out float remapped))
                    {
                        // If we keep forward distance continuous, DO NOT apply remapped
                        // (remapped exists to keep same normalized progress across splines, but it can feel like a slowdown)
                        if (!keepForwardDistanceDuringLaneChange)
                            traveledDistance = remapped;

                        lastLaneChangeTime = Time.time;
                        currentLane = newLane;
                        LaneChanged?.Invoke();
                    }
                }
                else
                {
                    currentLane = newLane;
                    targetOffset = laneOffsets[newLane];
                    lastLaneChangeTime = Time.time;
                    LaneChanged?.Invoke();
                }
            }

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
            // ---- HARD PAUSE SAFETY ----
            // If timescale is 0, dt will be 0 -> DO NOT simulate.
            if (Time.timeScale <= 0f) return;
            if (dt <= 0f) return;

            if (splineContainer == null || spline == null) return;
            if (splineLength <= 0.0001f) splineLength = spline.GetLength();
            if (splineLength <= 0.0001f) return;

            // 1) Advance distance along spline
            traveledDistance = Mathf.Repeat(traveledDistance + speed * dt, splineLength);
            float t = traveledDistance / splineLength;

            // 2) Evaluate spline sample (world pos, tangent, up, right)
            Vector3 worldP_Spline;
            Vector3 worldT;
            Vector3 worldUp_Spline;
            Vector3 worldRight;

            if (useSeparateLaneSplines && laneTrackSwitcher != null)
            {
                laneTrackSwitcher.GetBlendedSample(traveledDistance, out worldP_Spline, out worldT, out worldUp_Spline);

                worldT = worldT.sqrMagnitude > 1e-6f ? worldT.normalized : transform.forward;
                worldUp_Spline = worldUp_Spline.sqrMagnitude > 1e-6f ? worldUp_Spline.normalized : Vector3.up;

                worldRight = Vector3.Cross(worldT, worldUp_Spline);
                if (worldRight.sqrMagnitude < 1e-6f) worldRight = Vector3.Cross(worldT, Vector3.up);
                worldRight = worldRight.normalized;
            }
            else
            {
                SplineUtility.Evaluate(spline, t, out float3 lp, out float3 lt, out float3 lu);

                worldP_Spline = splineContainer.transform.TransformPoint(lp);

                worldT = splineContainer.transform.TransformDirection(lt);
                if (worldT.sqrMagnitude < 1e-6f) worldT = transform.forward;
                worldT = worldT.normalized;

                worldUp_Spline = splineContainer.transform.TransformDirection(lu);
                if (worldUp_Spline.sqrMagnitude < 1e-6f) worldUp_Spline = Vector3.up;
                worldUp_Spline = worldUp_Spline.normalized;

                float3 lr = math.normalizesafe(math.cross(lt, lu));
                worldRight = splineContainer.transform.TransformDirection((Vector3)lr);
                if (worldRight.sqrMagnitude < 1e-6f) worldRight = Vector3.Cross(worldT, worldUp_Spline);
                worldRight = worldRight.normalized;
            }

            // lane offset (Y is still spline Y)
            Vector3 worldPosOnSpline = worldP_Spline + worldRight * currentOffset;

            // 3) Decide height/up source
            bool groundHit = false;
            RaycastHit hit = default;

            if (followGroundMesh)
            {
                Vector3 origin = worldPosOnSpline + Vector3.up * raycastHeight;
                float dist = raycastHeight + groundCheckDistance;

                groundHit = Physics.SphereCast(
                    origin,
                    groundProbeRadius,
                    Vector3.down,
                    out hit,
                    dist,
                    drivableSurface,
                    QueryTriggerInteraction.Ignore
                );

                Debug.DrawRay(origin, Vector3.down * dist, groundHit ? Color.green : Color.red);

                // If probe missed for 1 frame, keep last hit briefly (prevents airborne jitter)
                if (!groundHit && (Time.time - _lastGroundHitTime) <= groundLostGrace)
                {
                    groundHit = true;
                    hit = _lastGroundHit;
                }

                if (groundHit)
                {
                    _lastGroundHit = hit;
                    _lastGroundHitTime = Time.time;
                }
            }
            else
            {
                // spline mode: always "grounded"
                groundHit = false;
            }

            // 4) Compute target Y
            float targetY;

            if (!followGroundMesh)
            {
                // SPLINE MODE (recommended) -> zero jitter
                isGrounded = true;
                verticalVelocity = 0f;
                targetY = worldPosOnSpline.y + heightAboveGround;
            }
            else
            {
                if (groundHit)
                {
                    isGrounded = true;
                    targetY = hit.point.y + heightAboveGround;

                    if (verticalVelocity < 0f) verticalVelocity = 0f;
                }
                else
                {
                    // real airborne only if we truly lost ground beyond grace
                    isGrounded = false;
                    verticalVelocity += Physics.gravity.y * gravityScale * dt;
                    targetY = transform.position.y + verticalVelocity * dt;
                }
            }

            // 5) Smooth Y (but NEVER force dt while paused)
            if (Mathf.Abs(targetY - _smoothedY) > maxSnapY)
            {
                _smoothedY = targetY;
                _ySmoothVel = 0f;
            }
            else
            {
                _smoothedY = Mathf.SmoothDamp(_smoothedY, targetY, ref _ySmoothVel, groundSmoothTime, Mathf.Infinity, dt);
            }

            Vector3 finalPos = new Vector3(worldPosOnSpline.x, _smoothedY, worldPosOnSpline.z);

            _currentVelocity = (finalPos - transform.position) / dt;
            transform.position = finalPos;

            // 6) Rotation up vector
            Vector3 forwardDir = worldT.sqrMagnitude > 1e-6f ? worldT.normalized : transform.forward;

            Vector3 rawUp;
            if (!followGroundMesh)
                rawUp = worldUp_Spline;
            else
                rawUp = (groundHit ? hit.normal : worldUp_Spline);

            if (rawUp.sqrMagnitude < 1e-6f) rawUp = Vector3.up;
            rawUp.Normalize();

            float normalLerp = 1f - Mathf.Exp(-normalSmoothSpeed * dt);
            _smoothedUp = Vector3.Slerp(_smoothedUp == Vector3.zero ? rawUp : _smoothedUp, rawUp, normalLerp).normalized;

            Quaternion baseRot = Quaternion.LookRotation(forwardDir, _smoothedUp);

            // 7) Drift yaw around smoothed up
            float targetDriftYaw = 0f;

            if (balanceActive)
            {
                targetDriftYaw = 100f * driftDirection;
            }
            else if (isDrifting)
            {
                float cornerIntensity = Mathf.Clamp01(Mathf.InverseLerp(cornerAngleThreshold, maxCornerAngleForFullDrift, detectedCornerAngle));
                float cornerBasedYaw = Mathf.Lerp(minDriftYawAngle, maxDriftYawAngle, cornerIntensity);
                float speedScaleFactor = Mathf.Clamp(Mathf.InverseLerp(minSpeedFractionForYawEffect, maxSpeedFractionForYawEffect, smoothedSpeedRatio), 0.4f, 1f);
                targetDriftYaw = (cornerBasedYaw * speedScaleFactor) * driftDirection;
            }

            currentDriftYaw = Mathf.Lerp(currentDriftYaw, targetDriftYaw, (isDrifting ? driftEntrySpeed : driftExitSpeed) * dt);

            Quaternion driftRot = Quaternion.AngleAxis(currentDriftYaw, _smoothedUp);
            transform.rotation = baseRot * driftRot;
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
                float laneDir = 0f;
                if (useSeparateLaneSplines && laneTrackSwitcher != null && laneTrackSwitcher.IsChanging)
                    laneDir = Mathf.Sign(laneTrackSwitcher.TargetLane - laneTrackSwitcher.CurrentLane);
                else if (!useSeparateLaneSplines && isChangingLane)
                    laneDir = Mathf.Sign(targetOffset - currentOffset);
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
            if (Time.timeScale <= 0.0001f) return;

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

        public void TriggerSpeedStumbleExternal() => TriggerSpeedStumble();

    }
}