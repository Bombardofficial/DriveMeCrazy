// SkillEstimator.cs  (drop-in replacement)
using System.Collections.Generic;
using UnityEngine;

public class SkillEstimator : MonoBehaviour
{
    public static SkillEstimator Instance { get; private set; }

    // ===== Tunables (Inspector) =====
    [Header("EMA windows (seconds)")]
    public float windowCollect = 12f;
    public float windowObstacle = 12f;

    [Header("Weights -> skill score (before logistic)")]
    [Range(0f, 3f)] public float wCollect = 1.0f; // higher is better
    [Range(0f, 3f)] public float wMiss = 0.7f; // subtracts
    [Range(0f, 3f)] public float wHit = 1.3f; // subtracts

    [Header("Logistic squashing to 0..1")]
    [Range(0.5f, 4f)] public float slope = 2.2f;
    [Range(-1f, 1f)] public float bias = 0.0f;

    [Header("Debug / Export")]
    public bool writeTotals = true; // keep simple totals as well

    // ===== Per-player profile =====
    public sealed class Profile
    {
        // EMAs
        public float colOppEMA, colGotEMA, colMissEMA;
        public float obsOppEMA, obsHitEMA;

        // Totals (optional: nice for sanity checks)
        public int colOppTotal, colGotTotal, colMissTotal;
        public int obsOppTotal, obsHitTotal;

        // Derived
        public float CollectSuccessRate => SafeRatio(colGotEMA, colOppEMA);
        public float CollectMissRate => SafeRatio(colMissEMA, colOppEMA);
        public float ObstacleHitRate => SafeRatio(obsHitEMA, obsOppEMA);

        public float Skill01 { get; private set; } = 0.5f;

        readonly SkillEstimator _root;
        public Profile(SkillEstimator root) { _root = root; }

        public void Tick(float dt)
        {
            // score = +collects - misses - hits
            float score = 0f;
            score += _root.wCollect * CollectSuccessRate;
            score -= _root.wMiss * CollectMissRate;
            score -= _root.wHit * ObstacleHitRate;

            float x = _root.slope * (score + _root.bias);
            Skill01 = 1f / (1f + Mathf.Exp(-x));
        }

        float Alpha(float window, float dt)
            => 1f - Mathf.Exp(-dt / Mathf.Max(0.001f, window));

        // --- event sinks (all attributed to whoever is "active" when they occur) ---
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

        static float SafeRatio(float num, float den)
            => (den <= 1e-4f) ? 0f : Mathf.Clamp01(num / den);
    }

    // ===== Runtime state =====
    // Per-passenger profiles (stable identity per player)
    readonly Dictionary<Passenger, Profile> _profiles = new();
    Profile _active;  // current driver
    Passenger _activePassenger;

    // A convenience "global" profile that aggregates everything (optional)
    public Profile Global { get; private set; }

    // Expose the active profile safely
    public Profile Active => _active ?? Global;

    // For overlay convenience
    public IReadOnlyDictionary<Passenger, Profile> Profiles => _profiles;

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        Global = new Profile(this);
    }

    void OnEnable()
    {
        PlayerManager.OnDriverChanged += HandleDriverChanged;
        // set initial, if driver already exists
        if (PlayerManager.Instance && PlayerManager.Instance.CurrentDriver)
            SetActiveDriver(PlayerManager.Instance.CurrentDriver);
    }

    void OnDisable()
    {
        PlayerManager.OnDriverChanged -= HandleDriverChanged;
    }

    void HandleDriverChanged(Passenger oldP, Passenger newP)
    {
        SetActiveDriver(newP);
    }

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

        // tick all profiles so Skill01 is up to date
        Global?.Tick(dt);
        foreach (var kv in _profiles) kv.Value.Tick(dt);
    }

    // ===== Public API (unchanged call sites) =====
    // These attribute the event to the *current driver* if present,
    // and always also update the Global aggregate.
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

    // Handy accessors for overlay (current driver identity)
    public Passenger ActivePassenger => _activePassenger;
}
