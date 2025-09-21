// DifficultyDirector.cs
using UnityEngine;

[System.Serializable] public struct DifficultyState { public float target; }

public class DifficultyDirector : MonoBehaviour
{
    public static DifficultyDirector Instance { get; private set; }

    public enum Source { Manual, SkillEstimator }
    public Source source = Source.SkillEstimator;

    [Header("Ticking")]
    public float tickInterval = 0.25f;
    public float rampPerSecond = 0.75f;

    [Header("Manual")]
    [Range(0f, 1f)] public float manualDifficulty = 0.5f;

    [Header("Mapping skill -> difficulty")]
    [Range(0.2f, 3f)] public float gamma = 1.0f;  // >1 amplifies high skill

    public DifficultyState Current { get; private set; }

    float _lastTick, _published;

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        _published = 0.5f;
        Current = new DifficultyState { target = _published };
    }

    void Update()
    {
        if (Time.time - _lastTick < tickInterval) return;
        _lastTick = Time.time;

        float desired = 0.5f;
        if (source == Source.Manual) desired = manualDifficulty;
        else
        {
            var est = SkillEstimator.Instance;
            float s = 0.5f;
            if (est != null)
            {
                // use current driver’s profile if available, otherwise global
                var prof = est.Active ?? est.Global;
                if (prof != null) s = prof.Skill01;
            }
            desired = Mathf.Pow(Mathf.Clamp01(s), gamma);
        }

        float step = rampPerSecond * tickInterval;
        _published = Mathf.MoveTowards(_published, desired, step);
        Current = new DifficultyState { target = _published };
    }
}
