// SkillEstimator.cs  — REPLACE WHOLE FILE WITH THIS
using System.Collections.Generic;
using UnityEngine;

public class SkillEstimator : MonoBehaviour
{
    public static SkillEstimator Instance { get; private set; }

    // ===== Tunables (Inspector) =====
    [Header("EMA windows (seconds)")]
    public float windowCollect = 12f;
    public float windowObstacle = 12f;
    public float windowMiniGame = 16f;   // NEW: mini-game learning window

    [Header("Weights -> skill score (before logistic)")]
    [Range(0f, 3f)] public float wCollect = 1.0f;   // higher is better
    [Range(0f, 3f)] public float wMiss = 0.7f;   // subtracts
    [Range(0f, 3f)] public float wHit = 1.3f;   // subtracts

    [Header("Mini-game weights")]
    [Range(0f, 3f)] public float wMgPass = 1.0f; // good
    [Range(0f, 3f)] public float wMgPerfect = 1.5f; // very good
    [Range(0f, 3f)] public float wMgFail = 1.2f; // bad

    [Header("Mini-game fail severity multipliers")]
    [Tooltip("How much a full crash counts compared to a simple stumble.")]
    public float mgFailWeightStumble = 0.6f;
    public float mgFailWeightCrash = 1.0f;

    [Header("Logistic squashing to 0..1")]
    [Range(0.5f, 4f)] public float slope = 2.2f;
    [Range(-1f, 1f)] public float bias = 0.0f;

    [Header("Debug / Export")]
    public bool writeTotals = true;

    [Header("Confidence")]
    [Tooltip("Effective sample mass (EMA) where confidence ~= 1.0")]
    public float confidenceMassForFull = 40f;
    // ===== Per-player profile =====
    public sealed class Profile
    {
        // EMAs: collectibles & obstacles
        public float colOppEMA, colGotEMA, colMissEMA;
        public float obsOppEMA, obsHitEMA;

        // EMAs: mini-game (NEW)
        public float mgOppEMA, mgPassEMA, mgPerfectEMA, mgFailEMA;

        // Totals (optional)
        public int colOppTotal, colGotTotal, colMissTotal;
        public int obsOppTotal, obsHitTotal;
        public int mgOppTotal, mgPassTotal, mgPerfectTotal, mgFailTotal;

        // Derived (existing)
        public float CollectSuccessRate => SafeRatio(colGotEMA, colOppEMA);
        public float CollectMissRate => SafeRatio(colMissEMA, colOppEMA);
        public float ObstacleHitRate => SafeRatio(obsHitEMA, obsOppEMA);

        // Derived (mini-game, NEW)
        public float MiniGamePassRate => SafeRatio(mgPassEMA, mgOppEMA);
        public float MiniGamePerfectRate => SafeRatio(mgPerfectEMA, mgOppEMA);
        public float MiniGameFailRate => SafeRatio(mgFailEMA, mgOppEMA);

        public float Skill01 { get; private set; } = 0.5f;

        readonly SkillEstimator _root;


        public Profile(SkillEstimator root) { _root = root; }
        public float Confidence
        {
            get
            {
                float mass = colOppEMA + obsOppEMA + mgOppEMA;
                return Mathf.Clamp01(mass / Mathf.Max(1f, _root.confidenceMassForFull));
            }
        }
        public void Tick(float dt)
        {
            float score = 0f;
            // collectibles & obstacles
            score += _root.wCollect * CollectSuccessRate;
            score -= _root.wMiss * CollectMissRate;
            score -= _root.wHit * ObstacleHitRate;

            // mini-game (NEW)
            score += _root.wMgPass * MiniGamePassRate;
            score += _root.wMgPerfect * MiniGamePerfectRate;
            score -= _root.wMgFail * MiniGameFailRate;

            float x = _root.slope * (score + _root.bias);
            Skill01 = 1f / (1f + Mathf.Exp(-x));
        }

        float Alpha(float window, float dt)
            => 1f - Mathf.Exp(-dt / Mathf.Max(0.001f, window));

        // ---------- Collectibles ----------
        public void OnCollectibleSpawned(float window, float dt)
        {
            float a = Alpha(window, dt);
            colOppEMA = Mathf.Lerp(colOppEMA, colOppEMA + 1f, a);
            if (_root.writeTotals) colOppTotal++;
        }
        public void OnCollectibleCollected(float window, float dt)
        {
            float a = Alpha(window, dt);
            colGotEMA = Mathf.Lerp(colGotEMA, colGotEMA + 1f, a);
            if (_root.writeTotals) colGotTotal++;
        }
        public void OnCollectibleMissed(float window, float dt)
        {
            float a = Alpha(window, dt);
            colMissEMA = Mathf.Lerp(colMissEMA, colMissEMA + 1f, a);
            if (_root.writeTotals) colMissTotal++;
        }

        // ---------- Obstacles ----------
        public void OnObstacleSpawned(float window, float dt)
        {
            float a = Alpha(window, dt);
            obsOppEMA = Mathf.Lerp(obsOppEMA, obsOppEMA + 1f, a);
            if (_root.writeTotals) obsOppTotal++;
        }
        public void OnObstacleHit(float window, float dt)
        {
            float a = Alpha(window, dt);
            obsHitEMA = Mathf.Lerp(obsHitEMA, obsHitEMA + 1f, a);
            if (_root.writeTotals) obsHitTotal++;
        }

        // ---------- Mini-game (NEW) ----------
        public void OnMiniGameStarted(float window, float dt)
        {
            float a = Alpha(window, dt);
            mgOppEMA = Mathf.Lerp(mgOppEMA, mgOppEMA + 1f, a);
            if (_root.writeTotals) mgOppTotal++;
        }
        public void OnMiniGamePass(float window, float dt)
        {
            float a = Alpha(window, dt);
            mgPassEMA = Mathf.Lerp(mgPassEMA, mgPassEMA + 1f, a);
            if (_root.writeTotals) mgPassTotal++;
        }
        public void OnMiniGamePerfect(float window, float dt)
        {
            float a = Alpha(window, dt);
            mgPerfectEMA = Mathf.Lerp(mgPerfectEMA, mgPerfectEMA + 1f, a);
            if (_root.writeTotals) mgPerfectTotal++;
        }
        public void OnMiniGameFail(float window, float dt, bool fullCrash, float stumbleMult, float crashMult)
        {
            float a = Alpha(window, dt);
            float w = fullCrash ? crashMult : stumbleMult;   // severity
            mgFailEMA = Mathf.Lerp(mgFailEMA, mgFailEMA + w, a);
            if (_root.writeTotals) mgFailTotal++;
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
        if (Instance && Instance != this) { Destroy(gameObject); return; }
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
        foreach (var kv in _profiles) kv.Value.Tick(dt);
    }

    // ===== Public API (existing) =====
    public void OnCollectibleSpawned()
    {
        float dt = Time.deltaTime;
        Global.OnCollectibleSpawned(windowCollect, dt);
        Active?.OnCollectibleSpawned(windowCollect, dt);
    }
    public void OnCollectibleCollected()
    {
        float dt = Time.deltaTime;
        Global.OnCollectibleCollected(windowCollect, dt);
        Active?.OnCollectibleCollected(windowCollect, dt);
    }
    public void OnCollectibleMissed()
    {
        float dt = Time.deltaTime;
        Global.OnCollectibleMissed(windowCollect, dt);
        Active?.OnCollectibleMissed(windowCollect, dt);
    }
    public void OnObstacleSpawned()
    {
        float dt = Time.deltaTime;
        Global.OnObstacleSpawned(windowObstacle, dt);
        Active?.OnObstacleSpawned(windowObstacle, dt);
    }
    public void OnObstacleHit()
    {
        float dt = Time.deltaTime;
        Global.OnObstacleHit(windowObstacle, dt);
        Active?.OnObstacleHit(windowObstacle, dt);
    }

    // ===== Public API (NEW: mini-game) =====
    public void OnMiniGameStarted()
    {
        float dt = Time.deltaTime;
        Global.OnMiniGameStarted(windowMiniGame, dt);
        Active?.OnMiniGameStarted(windowMiniGame, dt);
    }
    public void OnMiniGamePass()
    {
        float dt = Time.deltaTime;
        Global.OnMiniGamePass(windowMiniGame, dt);
        Active?.OnMiniGamePass(windowMiniGame, dt);
    }
    public void OnMiniGamePerfect()
    {
        float dt = Time.deltaTime;
        Global.OnMiniGamePerfect(windowMiniGame, dt);
        Active?.OnMiniGamePerfect(windowMiniGame, dt);
    }
    public void OnMiniGameFail(bool fullCrash)
    {
        float dt = Time.deltaTime;
        Global.OnMiniGameFail(windowMiniGame, dt, fullCrash, mgFailWeightStumble, mgFailWeightCrash);
        Active?.OnMiniGameFail(windowMiniGame, dt, fullCrash, mgFailWeightStumble, mgFailWeightCrash);
    }
}
