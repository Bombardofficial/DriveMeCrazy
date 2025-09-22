// DifficultyDirector.cs
using UnityEngine;

// DifficultyState becomes multichannel
[System.Serializable]
public struct DifficultyState
{
    public float overall;        // legacy
    public float spawnPressure;  // obstacles per minute
    public float precision;      // mini-game tightness
    public float punishment;     // stumble->crash severity / fail grace
    public float rewardBias;     // % collectibles vs obstacles
}


public class DifficultyDirector : MonoBehaviour
{
    public static DifficultyDirector Instance { get; private set; }

    [Header("Tick")] public float tickInterval = 0.25f, rampPerSecond = 0.75f;
    [Header("Mapping")][Range(0.2f, 3f)] public float gammaOverall = 1.0f;
    public AnimationCurve spawnPressureMap = AnimationCurve.Linear(0, 0.3f, 1, 1f);
    public AnimationCurve precisionMap = AnimationCurve.Linear(0, 0.2f, 1, 1f);
    public AnimationCurve punishmentMap = AnimationCurve.Linear(0, 0.2f, 1, 1f);
    public AnimationCurve rewardBiasMap = AnimationCurve.Linear(0, 0.7f, 1, 0.3f); // low skill -> more rewards

    public DifficultyState Current { get; private set; }
    float _lastTick;
    DifficultyState _published;

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        _published = new DifficultyState
        {
            overall = 0.5f,
            spawnPressure = spawnPressureMap.Evaluate(0.5f),
            precision = precisionMap.Evaluate(0.5f),
            punishment = punishmentMap.Evaluate(0.5f),
            rewardBias = rewardBiasMap.Evaluate(0.5f),
        };
        Current = _published;
    }


    void Update()
    {
        if (Time.time - _lastTick < tickInterval) return; _lastTick = Time.time;

        // read skill with confidence
        float skill = 0.5f;
        var est = SkillEstimator.Instance;
        if (est != null) skill = (est.Active ?? est.Global)?.Skill01 ?? 0.5f;
        float conf = (est?.Active ?? est?.Global)?.Confidence ?? 0.0f;
        float skillConf = Mathf.Lerp(0.5f, skill, conf);

        float overall = Mathf.Pow(Mathf.Clamp01(skillConf), gammaOverall);
        var desired = new DifficultyState
        {
            overall = overall,
            spawnPressure = Mathf.Clamp01(spawnPressureMap.Evaluate(overall)),
            precision = Mathf.Clamp01(precisionMap.Evaluate(overall)),
            punishment = Mathf.Clamp01(punishmentMap.Evaluate(overall)),
            rewardBias = Mathf.Clamp01(rewardBiasMap.Evaluate(overall)),
        };

        // ramp
        float step = rampPerSecond * tickInterval;
        _published.overall = Mathf.MoveTowards(_published.overall, desired.overall, step);
        _published.spawnPressure = Mathf.MoveTowards(_published.spawnPressure, desired.spawnPressure, step);
        _published.precision = Mathf.MoveTowards(_published.precision, desired.precision, step);
        _published.punishment = Mathf.MoveTowards(_published.punishment, desired.punishment, step);
        _published.rewardBias = Mathf.MoveTowards(_published.rewardBias, desired.rewardBias, step);

        Current = _published;
    }
}
