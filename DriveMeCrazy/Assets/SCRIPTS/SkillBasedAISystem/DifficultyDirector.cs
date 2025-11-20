// DifficultyDirector.cs — multi-player aware, per-player difficulty states
using System.Collections.Generic;
using UnityEngine;

// DifficultyState marad: a manager-ek ezt olvassák
[System.Serializable]
public struct DifficultyState
{
    public float overall;        // 0..1 — össz skill
    public float spawnPressure;  // 0..1 — obstacle sûrûség
    public float precision;      // 0..1 — mini-game tightness
    public float punishment;     // 0..1 — bünti/lock/crash keménység
    public float rewardBias;     // 0..1 — 0: obstacle-heavy, 1: reward-heavy
}

public class DifficultyDirector : MonoBehaviour
{
    public static DifficultyDirector Instance { get; private set; }

    [Header("Tick")]
    [Tooltip("How often the director polls skill & updates difficulty (seconds).")]
    public float tickInterval = 0.25f;

    [Tooltip("How fast difficulty can move towards the new target per second.")]
    public float rampPerSecond = 0.75f;

    [Header("Mapping from skill->difficulty")]
    [Tooltip("Gamma for skill (0..1) before mapping. >1 = hangsúly a magas skill tartományra.")]
    [Range(0.2f, 3f)] public float gammaOverall = 1.0f;

    [Tooltip("Spawn pressure (obstacles) as a function of skill.")]
    public AnimationCurve spawnPressureMap = AnimationCurve.Linear(0, 0.3f, 1, 1f);

    [Tooltip("Mini-game precision as a function of skill.")]
    public AnimationCurve precisionMap = AnimationCurve.Linear(0, 0.2f, 1, 1f);

    [Tooltip("Punishment (büntetés keménysége) as a function of skill.")]
    public AnimationCurve punishmentMap = AnimationCurve.Linear(0, 0.2f, 1, 1f);

    [Tooltip("Reward bias as a function of skill (low skill -> több collectible).")]
    public AnimationCurve rewardBiasMap = AnimationCurve.Linear(0, 0.7f, 1, 0.3f);

    // --- Public read access --------------------------------------------------

    /// <summary>Globális, "átlagolt" difficulty (nem driver-specifikus).</summary>
    public DifficultyState GlobalCurrent => _globalPublished;

    /// <summary>Per-player publikált difficulty állapotok.</summary>
    public IReadOnlyDictionary<Passenger, DifficultyState> PerPlayerCurrent => _perPlayerPublished;

    /// <summary>Aktuális driver (PlayerManager-bõl).</summary>
    public Passenger CurrentDriver { get; private set; }

    /// <summary>
    /// Backwards-compatible:
    /// - ha van driver és van rá per-player state -> azt adja vissza,
    /// - különben a globális állapotot.
    /// </summary>
    public DifficultyState Current
    {
        get
        {
            if (CurrentDriver && _perPlayerPublished.TryGetValue(CurrentDriver, out var s))
                return s;
            return _globalPublished;
        }
    }

    // --- Internal state ------------------------------------------------------

    DifficultyState _globalPublished;
    readonly Dictionary<Passenger, DifficultyState> _perPlayerPublished = new();

    float _lastTick;

    void Awake()
    {
        if (Instance && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Induláskor tegyük be "félgõzös" baseline-ra
        float baseSkill = 0.5f;
        _globalPublished = ComputeDesiredFromSkill(baseSkill);

        // Per-player dictionary üresen indul; ahogy a SkillEstimator profilok nõnek, feltöltjük.
    }

    void OnEnable()
    {
        PlayerManager.OnDriverChanged += HandleDriverChanged;
        if (PlayerManager.Instance && PlayerManager.Instance.CurrentDriver)
            CurrentDriver = PlayerManager.Instance.CurrentDriver;
    }

    void OnDisable()
    {
        PlayerManager.OnDriverChanged -= HandleDriverChanged;
    }

    void HandleDriverChanged(Passenger oldP, Passenger newP)
    {
        CurrentDriver = newP;
    }

    void Update()
    {
        if (Time.time - _lastTick < tickInterval)
            return;

        _lastTick = Time.time;

        var est = SkillEstimator.Instance;
        if (est == null)
            return;

        float step = rampPerSecond * tickInterval;

        // --- 1) Globális difficulty a Global profilon ------------------------
        float gSkill = 0.5f;
        float gConf = 0f;

        if (est.Global != null)
        {
            gSkill = est.Global.Skill01;
            gConf = est.Global.Confidence;
        }

        // Confidence-blend: ha még kevés adat van, közelebb 0.5-höz
        float gSkillConf = Mathf.Lerp(0.5f, gSkill, gConf);
        var gDesired = ComputeDesiredFromSkill(gSkillConf);
        _globalPublished = RampTowards(_globalPublished, gDesired, step);

        // --- 2) Per-player difficulty minden Passenger profilból -------------
        var profiles = est.Profiles;
        if (profiles != null)
        {
            // Tisztítsuk a halott/passenger==null kulcsokat
            _toRemove.Clear();
            foreach (var kv in _perPlayerPublished)
            {
                if (!kv.Key) _toRemove.Add(kv.Key);
            }
            foreach (var dead in _toRemove)
                _perPlayerPublished.Remove(dead);
            _toRemove.Clear();

            // Minden létezõ profilhoz számolunk desired state-et
            foreach (var kv in profiles)
            {
                var p = kv.Key;
                var prof = kv.Value;
                if (!p || prof == null) continue;

                float s = prof.Skill01;
                float c = prof.Confidence;
                float sConf = Mathf.Lerp(0.5f, s, c);

                var desired = ComputeDesiredFromSkill(sConf);

                if (_perPlayerPublished.TryGetValue(p, out var current))
                {
                    _perPlayerPublished[p] = RampTowards(current, desired, step);
                }
                else
                {
                    // elsõ frame: azonnal beugrik a desired-re (különben túl lassan indulna)
                    _perPlayerPublished[p] = desired;
                }
            }
        }
    }

    // --- Helpers -------------------------------------------------------------

    // ide jöhetne késõbb skill alapú tuning logika (pl. low skill -> rewardBias clamp), de most a curve-ek döntenek
    DifficultyState ComputeDesiredFromSkill(float skill01)
    {
        float s = Mathf.Clamp01(skill01);
        float sGamma = Mathf.Pow(s, gammaOverall);

        return new DifficultyState
        {
            overall = sGamma,
            spawnPressure = Mathf.Clamp01(spawnPressureMap.Evaluate(sGamma)),
            precision = Mathf.Clamp01(precisionMap.Evaluate(sGamma)),
            punishment = Mathf.Clamp01(punishmentMap.Evaluate(sGamma)),
            rewardBias = Mathf.Clamp01(rewardBiasMap.Evaluate(sGamma)),
        };
    }

    DifficultyState RampTowards(DifficultyState from, DifficultyState to, float step)
    {
        from.overall = Mathf.MoveTowards(from.overall, to.overall, step);
        from.spawnPressure = Mathf.MoveTowards(from.spawnPressure, to.spawnPressure, step);
        from.precision = Mathf.MoveTowards(from.precision, to.precision, step);
        from.punishment = Mathf.MoveTowards(from.punishment, to.punishment, step);
        from.rewardBias = Mathf.MoveTowards(from.rewardBias, to.rewardBias, step);
        return from;
    }

    // ideiglenes lista a "dead" passengerek kiszedésére
    static readonly List<Passenger> _toRemove = new();
}
