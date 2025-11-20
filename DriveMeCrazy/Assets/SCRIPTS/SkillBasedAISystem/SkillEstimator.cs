// SkillEstimator.cs — production-ready, event-based EMA + trajectory + smoothed skill
using System.Collections.Generic;
using UnityEngine;

public class SkillEstimator : MonoBehaviour
{
    public static SkillEstimator Instance { get; private set; }

    // ===== Tunables (Inspector) =====

    [Header("EMA memory for DISCRETE events (in events, not seconds!)")]
    [Tooltip("Approx. number of recent COLLECT events that influence the EMA strongly.\n" +
             "E.g. 40 = about the last 40 collectible events matter most.")]
    public float windowCollect = 40f;

    [Tooltip("Approx. number of recent OBSTACLE events that influence the EMA strongly.")]
    public float windowObstacle = 40f;

    [Tooltip("Approx. number of recent MINI-GAME events that influence the EMA strongly.")]
    public float windowMiniGame = 20f;   // mini-game learning window (events)

    [Header("Trajectory smoothing (seconds)")]
    [Tooltip("Smoothing window (seconds) for continuous trajectory samples " +
             "(corner quality, lane jitter, overspeed control).")]
    public float windowTrajectory = 12f;

    [Header("Weights -> score (before logistic)")]
    [Range(0f, 3f)] public float wCollect = 1.0f;   // higher is better
    [Range(0f, 3f)] public float wMiss = 0.7f;    // subtracts
    [Range(0f, 3f)] public float wHit = 1.3f;    // subtracts

    [Header("Mini-game weights")]
    [Range(0f, 3f)] public float wMgPass = 1.0f;   // good
    [Range(0f, 3f)] public float wMgPerfect = 1.5f;   // very good
    [Range(0f, 3f)] public float wMgFail = 1.2f;   // bad

    [Header("Mini-game fail severity multipliers")]
    [Tooltip("How much a full crash counts compared to a simple stumble.")]
    public float mgFailWeightStumble = 0.6f;
    public float mgFailWeightCrash = 1.0f;

    [Header("Trajectory weights")]
    [Tooltip("How strongly overall corner smoothness contributes to skill.")]
    [Range(0f, 3f)] public float wCorner = 1.0f;

    [Tooltip("Penalty for steering jitter when not actually changing lane.")]
    [Range(0f, 3f)] public float wLaneJitter = 0.8f;

    [Tooltip("Reward for staying under or near the speed-limit instead of constantly overspeeding.")]
    [Range(0f, 3f)] public float wOverspeedControl = 1.0f;

    [Header("Logistic squashing to 0..1")]
    [Tooltip("Steepness of the logistic curve. Lower = softer, less \"all or nothing\".")]
    [Range(0.5f, 4f)] public float slope = 1.4f;

    [Tooltip("Bias shift before logistic. 0 = centered; >0 makes same score feel more skilled.")]
    [Range(-1f, 1f)] public float bias = 0.0f;

    [Header("Skill smoothing")]
    [Tooltip("Time (in seconds) for Skill01 to move ~63% toward a new target value.\n" +
             "Smaller = more twitchy, larger = more sluggish but stable.")]
    public float skillSmoothSeconds = 0.5f;

    [Header("Debug / Export")]
    public bool writeTotals = true;

    [Header("Confidence")]
    [Tooltip("Effective sample mass (EMA sum) where confidence ~= 1.0.\n" +
             "A good starting point is windowCollect + windowObstacle + windowMiniGame.")]
    public float confidenceEventsForFull = 100f;

    // ===== Per-player profile =====
    public sealed class Profile
    {
        // EMAs: collectibles & obstacles (event-based mass)
        public float colOppEMA, colGotEMA, colMissEMA;
        public float obsOppEMA, obsHitEMA;

        // EMAs: mini-game (event-based mass)
        public float mgOppEMA, mgPassEMA, mgPerfectEMA, mgFailEMA;

        // EMAs: trajectory (continuous 0..1 signals)
        // cornerSmoothEMA: 0..1, HIGH = good (ideal speed in corners)
        // laneJitterEMA:   0..1, HIGH = bad (much pointless steering), will be a penalty
        // overspeedCtrlEMA:0..1, HIGH = good (keeps speed near/below limit)
        public float cornerSmoothEMA;
        public float laneJitterEMA;
        public float overspeedControlEMA;

        // Totals (optional, for logging)
        public int colOppTotal, colGotTotal, colMissTotal;
        public int obsOppTotal, obsHitTotal;
        public int mgOppTotal, mgPassTotal, mgPerfectTotal, mgFailTotal;
        public int trajSamplesTotal;

        // Derived (existing)
        public float CollectSuccessRate => SafeRatio(colGotEMA, colOppEMA);
        public float CollectMissRate => SafeRatio(colMissEMA, colOppEMA);
        public float ObstacleHitRate => SafeRatio(obsHitEMA, obsOppEMA);

        // Derived (mini-game)
        public float MiniGamePassRate => SafeRatio(mgPassEMA, mgOppEMA);
        public float MiniGamePerfectRate => SafeRatio(mgPerfectEMA, mgOppEMA);
        public float MiniGameFailRate => SafeRatio(mgFailEMA, mgOppEMA);

        // Expose trajectory metrics (for debug/plots later)
        public float CornerSmoothness => Mathf.Clamp01(cornerSmoothEMA);
        public float LaneJitter => Mathf.Clamp01(laneJitterEMA);
        public float OverspeedControl => Mathf.Clamp01(overspeedControlEMA);

        public float Skill01 { get; private set; } = 0.5f;

        readonly SkillEstimator _root;

        public Profile(SkillEstimator root) { _root = root; }

        public float Confidence
        {
            get
            {
                // mass ~ "how many effective events were seen recently"
                float mass = colOppTotal + obsOppTotal + mgOppTotal;
                return Mathf.Clamp01(mass / Mathf.Max(1f, _root.confidenceEventsForFull));
            }
        }

        public void Tick(float dt)
        {
            float score = 0f;

            // === Collectibles & obstacles ===
            score += _root.wCollect * CollectSuccessRate;
            score -= _root.wMiss * CollectMissRate;
            score -= _root.wHit * ObstacleHitRate;

            // === Mini-game ===
            score += _root.wMgPass * MiniGamePassRate;
            score += _root.wMgPerfect * MiniGamePerfectRate;
            score -= _root.wMgFail * MiniGameFailRate;

            // === Trajectory ===
            // CornerSmoothness: HIGH=good -> plusz pont
            score += _root.wCorner * CornerSmoothness;
            // LaneJitter: HIGH=rossz -> mínusz pont
            score -= _root.wLaneJitter * LaneJitter;
            // OverspeedControl: HIGH=jó (jól tartja a limitet) -> plusz pont
            score += _root.wOverspeedControl * OverspeedControl;

            // Logistic squash 0..1-be (raw -> target)
            float x = _root.slope * (score + _root.bias);
            float target = 1f / (1f + Mathf.Exp(-x));

            // Time-based smoothing: exponential toward target
            float alphaSkill = AlphaTime(_root.skillSmoothSeconds, dt);
            Skill01 = Mathf.Lerp(Skill01, target, alphaSkill);
        }

        // ---------- Alpha helpers ----------

        // DISCRETE event EMA: window = events
        float AlphaEvent(float windowEvents)
        {
            float w = Mathf.Max(1f, windowEvents);
            return 1f - Mathf.Exp(-1f / w);
        }

        // CONTINUOUS signal EMA: window = seconds
        float AlphaTime(float windowSeconds, float dt)
        {
            float w = Mathf.Max(0.001f, windowSeconds);
            return 1f - Mathf.Exp(-dt / w);
        }

        // ---------- Collectibles (event-based EMAs) ----------
        public void OnCollectibleSpawned()
        {
            float a = AlphaEvent(_root.windowCollect);
            colOppEMA = Mathf.Lerp(colOppEMA, colOppEMA + 1f, a);
            if (_root.writeTotals) colOppTotal++;
        }

        public void OnCollectibleCollected()
        {
            float a = AlphaEvent(_root.windowCollect);
            colGotEMA = Mathf.Lerp(colGotEMA, colGotEMA + 1f, a);
            if (_root.writeTotals) colGotTotal++;
        }

        public void OnCollectibleMissed()
        {
            float a = AlphaEvent(_root.windowCollect);
            colMissEMA = Mathf.Lerp(colMissEMA, colMissEMA + 1f, a);
            if (_root.writeTotals) colMissTotal++;
        }

        // ---------- Obstacles ----------
        public void OnObstacleSpawned()
        {
            float a = AlphaEvent(_root.windowObstacle);
            obsOppEMA = Mathf.Lerp(obsOppEMA, obsOppEMA + 1f, a);
            if (_root.writeTotals) obsOppTotal++;
        }

        public void OnObstacleHit()
        {
            float a = AlphaEvent(_root.windowObstacle);
            obsHitEMA = Mathf.Lerp(obsHitEMA, obsHitEMA + 1f, a);
            if (_root.writeTotals) obsHitTotal++;
        }

        // ---------- Mini-game ----------
        public void OnMiniGameStarted()
        {
            float a = AlphaEvent(_root.windowMiniGame);
            mgOppEMA = Mathf.Lerp(mgOppEMA, mgOppEMA + 1f, a);
            if (_root.writeTotals) mgOppTotal++;
        }

        public void OnMiniGamePass()
        {
            float a = AlphaEvent(_root.windowMiniGame);
            mgPassEMA = Mathf.Lerp(mgPassEMA, mgPassEMA + 1f, a);
            if (_root.writeTotals) mgPassTotal++;
        }

        public void OnMiniGamePerfect()
        {
            float a = AlphaEvent(_root.windowMiniGame);
            mgPerfectEMA = Mathf.Lerp(mgPerfectEMA, mgPerfectEMA + 1f, a);
            if (_root.writeTotals) mgPerfectTotal++;
        }

        public void OnMiniGameFail(bool fullCrash)
        {
            float a = AlphaEvent(_root.windowMiniGame);
            float w = fullCrash ? _root.mgFailWeightCrash : _root.mgFailWeightStumble;   // severity
            mgFailEMA = Mathf.Lerp(mgFailEMA, mgFailEMA + w, a);
            if (_root.writeTotals) mgFailTotal++;
        }

        // ---------- Trajectory (continuous, time-based EMA) ----------
        /// <param name="cornerSmooth01">0..1, HIGH=good, -1 = ignore</param>
        /// <param name="laneJitter01">0..1, HIGH=bad (több jitter), -1 = ignore</param>
        /// <param name="overspeedControl01">0..1, HIGH=good, -1 = ignore</param>
        public void OnTrajectorySample(
            float cornerSmooth01,
            float laneJitter01,
            float overspeedControl01,
            float windowSeconds, float dt)
        {
            float a = AlphaTime(windowSeconds, dt);

            if (cornerSmooth01 >= 0f)
                cornerSmoothEMA = Mathf.Lerp(cornerSmoothEMA, Mathf.Clamp01(cornerSmooth01), a);

            if (laneJitter01 >= 0f)
                laneJitterEMA = Mathf.Lerp(laneJitterEMA, Mathf.Clamp01(laneJitter01), a);

            if (overspeedControl01 >= 0f)
                overspeedControlEMA = Mathf.Lerp(overspeedControlEMA, Mathf.Clamp01(overspeedControl01), a);

            if (cornerSmooth01 >= 0f || laneJitter01 >= 0f || overspeedControl01 >= 0f)
            {
                if (_root.writeTotals) trajSamplesTotal++;
            }
        }

        static float SafeRatio(float num, float den)
            => (den <= 1e-4f) ? 0f : Mathf.Clamp01(num / den);
    }

    // ===== Runtime state =====
    readonly Dictionary<Passenger, Profile> _profiles = new();
    Profile _active;
    Passenger _activePassenger;

    public Profile Global { get; private set; }
    public Profile Active => _active ?? Global;
    public IReadOnlyDictionary<Passenger, Profile> Profiles => _profiles;
    public Passenger ActivePassenger => _activePassenger;

    void Awake()
    {
        if (Instance && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        Global = new Profile(this);
    }

    void OnEnable()
    {
        PlayerManager.OnDriverChanged += HandleDriverChanged;
        if (PlayerManager.Instance && PlayerManager.Instance.CurrentDriver)
            SetActiveDriver(PlayerManager.Instance.CurrentDriver);
    }

    void OnDisable() => PlayerManager.OnDriverChanged -= HandleDriverChanged;

    void HandleDriverChanged(Passenger oldP, Passenger newP) => SetActiveDriver(newP);

    void SetActiveDriver(Passenger p)
    {
        _activePassenger = p;
        if (p == null) { _active = null; return; }

        if (!_profiles.TryGetValue(p, out var prof))
        {
            prof = new Profile(this);
            _profiles.Add(p, prof);
        }
        _active = prof;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        Global?.Tick(dt);
        foreach (var kv in _profiles)
            kv.Value.Tick(dt);
    }

    // ===== Public API: collectibles =====
    public void OnCollectibleSpawned()
    {
        Global.OnCollectibleSpawned();
        Active?.OnCollectibleSpawned();
    }

    public void OnCollectibleCollected()
    {
        Global.OnCollectibleCollected();
        Active?.OnCollectibleCollected();
    }

    public void OnCollectibleMissed()
    {
        Global.OnCollectibleMissed();
        Active?.OnCollectibleMissed();
    }

    // ===== Public API: obstacles =====
    public void OnObstacleSpawned()
    {
        Global.OnObstacleSpawned();
        Active?.OnObstacleSpawned();
    }

    public void OnObstacleHit()
    {
        Global.OnObstacleHit();
        Active?.OnObstacleHit();
    }

    // ===== Public API: mini-game =====
    public void OnMiniGameStarted()
    {
        Global.OnMiniGameStarted();
        Active?.OnMiniGameStarted();
    }

    public void OnMiniGamePass()
    {
        Global.OnMiniGamePass();
        Active?.OnMiniGamePass();
    }

    public void OnMiniGamePerfect()
    {
        Global.OnMiniGamePerfect();
        Active?.OnMiniGamePerfect();
    }

    public void OnMiniGameFail(bool fullCrash)
    {
        Global.OnMiniGameFail(fullCrash);
        Active?.OnMiniGameFail(fullCrash);
    }

    // ===== Public API: trajectory =====
    /// <summary>
    /// Continuous per-frame trajectory sample from the car controller.
    /// Values -1..1: pass -1 to "ignore" that channel this frame.
    /// </summary>
    public void OnTrajectorySample(float cornerSmooth01, float laneJitter01, float overspeedControl01)
    {
        float dt = Time.deltaTime;
        Global.OnTrajectorySample(cornerSmooth01, laneJitter01, overspeedControl01, windowTrajectory, dt);
        Active?.OnTrajectorySample(cornerSmooth01, laneJitter01, overspeedControl01, windowTrajectory, dt);
    }
}
