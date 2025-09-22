using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;


[RequireComponent(typeof(SplineContainer))]
public class CollectableManager : MonoBehaviour
{
    [System.Serializable]
    public struct CollectableMeta
    {
        public GameObject go;
        public float t;         // 0..1 position on spline
        public int lane;        // lane index
        public float spawnTime;
    }
    public IReadOnlyList<CollectableMeta> ActiveMetas => _activeMeta;
    readonly List<CollectableMeta> _activeMeta = new();
    [Header("Fairness / expiry")]
    public float autoMissAfterSeconds = 12f; // treat as missed opportunity

    [Header("Prefabs & Limits")]
    [SerializeField] GameObject collectablePrefab;
    [Min(1)][SerializeField] int maxActive = 10;
    [Min(.1f)][SerializeField] float spawnInterval = 5f;

    [Header("Lane & Distance Settings")]
    [SerializeField] public LaneLinesVisualizer laneVis;
    [Min(.1f)] public float minSeparation = 4f;
    [Range(1, 50)] public int maxPlacementAttempts = 15;

    [Header("Vertical placement")]
    public float rayHeight = 5f;
    public float floatHeight = 0.5f;
    public LayerMask groundMask;

    [Header("Debug • Scene view")]
    public bool drawAttemptGizmos = false;

    readonly List<GameObject> _pool = new();
    readonly List<GameObject> _active = new();
    SplineContainer _spline;

    [Header("Audio")]
    [SerializeField] private AudioSourcePool collectableAudioPool;

    // !!! DIFFICULTY — live driving knobs
    [Header("Dynamic Difficulty (Director)")]
    public bool useDirector = true;

    [Tooltip("Max collectibles active by difficulty (easier ? higher)")]
    public AnimationCurve maxActiveByDiff = AnimationCurve.Linear(0, 16, 1, 8);

    [Tooltip("Seconds between spawn attempts (easier ? spawn faster)")]
    public AnimationCurve intervalByDiff = AnimationCurve.Linear(0, 1.2f, 1, 3.5f);

    [Tooltip("Minimum spacing (easier ? spread out more)")]
    public AnimationCurve separationByDiff = AnimationCurve.Linear(0, 6f, 1, 3.5f);

    // smoothers
    float _curInterval, _curSeparation, _curMaxActive;

    [Header("Fairness")]
    [Tooltip("Meters ahead of the car where collectibles will not spawn.")]
    public float playerSafeAheadMeters = 14f;
    [Tooltip("Meters behind the car where collectibles will not spawn.")]
    public float playerSafeBehindMeters = 5f;
    [Tooltip("Minimum meters between a collectible and any obstacle.")]
    public float minCrossSeparationMeters = 3.0f;

    public ArcadeVP.ArcadeVehicleController player;  // assign in inspector
    public ObstacleManager obstacleMgr;               // assign in inspector

    [Header("Fairness • track-relative expiry")]
    [Tooltip("Meters behind the player at which a collectible is considered missed and despawned.")]
    public float despawnBehindMeters = 10f;

    [Tooltip("While the item is within this many meters AHEAD of the player, never expire it due to age.")]
    public float protectAheadMeters = 45f;

    [Tooltip("Hard cleanup: if older than this AND far away (ahead or behind), despawn without counting a miss.")]
    public float hardMaxLifetimeSeconds = 30f;


    float ArcLen() => _spline.Spline.GetLength();

    /// <summary> +meters if b is ahead of a on the spline, -meters if behind </summary>
    float AheadMeters(float aT, float bT)
    {
        float len = ArcLen();
        if (len <= 0.001f) return 0f;
        float dT = bT - aT;
        // wrap shortest forward arc
        if (dT < -0.5f) dT += 1f;
        else if (dT > 0.5f) dT -= 1f;
        return dT * len;
    }

    float DtFromMeters(float meters) => (ArcLen() <= 0.001f) ? 0f : meters / ArcLen();

    bool IsClearOfPlayer(float candidateT)
    {
        if (!player) return true;
        float tCar = Mathf.Repeat(player.TrackTNormalized, 1f);
        float wrap(float d) => (d < 0f) ? d + 1f : d;
        float dt = wrap(candidateT - tCar);
        float ahead = dt * ArcLen();
        float behind = (1f - dt) * ArcLen();
        if (ahead >= 0f && ahead < playerSafeAheadMeters) return false;
        if (behind >= 0f && behind < playerSafeBehindMeters) return false;
        return true;
    }

    bool IsFarFromObstacles(Vector3 p)
    {
        if (!obstacleMgr) return true;
        float minSq = minCrossSeparationMeters * minCrossSeparationMeters;
        var list = obstacleMgr.GetActiveWorldPositions(); // add method below
        for (int i = 0; i < list.Count; i++)
            if ((list[i] - p).sqrMagnitude < minSq) return false;
        return true;
    }

    void Awake()
    {
        if (!collectablePrefab) { Debug.LogError("CollectableManager: Prefab missing"); enabled = false; return; }
        _spline = GetComponent<SplineContainer>();
        if (!laneVis) laneVis = GetComponent<LaneLinesVisualizer>();
        if (!laneVis) { Debug.LogError("CollectableManager: LaneLinesVisualizer ref missing"); enabled = false; return; }

        BuildPool();
        StartCoroutine(SpawnLoop());
    }

    void BuildPool()
    {
        for (int i = 0; i < maxActive; ++i)
        {
            var go = Instantiate(collectablePrefab, Vector3.zero, Quaternion.identity, transform);
            go.SetActive(false);
            if (go.TryGetComponent(out Collectable col))
            {
                col.manager = this;
                col.audioPool = collectableAudioPool;
            }
            _pool.Add(go);
        }
    }

    GameObject NextPooled()
    {
        foreach (var g in _pool) if (!g.activeInHierarchy) return g;
        return null;
    }

    // !!! DIFFICULTY — apply curves & smoothing
    void ApplyDifficulty(float diff, float dt)
    {
        if (!useDirector) return;

        float targetMax = Mathf.Clamp(maxActiveByDiff.Evaluate(diff), 1f, 999f);
        float targetInt = Mathf.Max(0.15f, intervalByDiff.Evaluate(diff));
        float targetSep = Mathf.Max(0.5f, separationByDiff.Evaluate(diff));

        float k = 1f - Mathf.Exp(-5f * dt);   // smoothing factor (0..1)

        _curMaxActive = Mathf.Lerp(_curMaxActive <= 0 ? maxActive : _curMaxActive, targetMax, k);
        _curInterval = Mathf.Lerp(_curInterval <= 0 ? spawnInterval : _curInterval, targetInt, k);
        _curSeparation = Mathf.Lerp(_curSeparation <= 0 ? minSeparation : _curSeparation, targetSep, k);

        maxActive = Mathf.RoundToInt(_curMaxActive);
        spawnInterval = _curInterval;
        minSeparation = _curSeparation;
    }


    IEnumerator SpawnLoop()
    {
        while (!PlayerJoinManager.IsRaceStarted) yield return null;

        while (enabled)
        {
            float diff = 0.5f;
            if (DifficultyDirector.Instance)
            {
                float rewardBias = Mathf.Clamp01(DifficultyDirector.Instance.Current.rewardBias);
                diff = 1f - rewardBias;
            }
            ApplyDifficulty(diff, Time.deltaTime);

            if (_active.Count < maxActive)
                TrySpawn();

            for (int i = _activeMeta.Count - 1; i >= 0; --i)
            {
                var meta = _activeMeta[i];
                if (!meta.go || !meta.go.activeInHierarchy) { _activeMeta.RemoveAt(i); continue; }

                float age = Time.time - meta.spawnTime;
                float playerT = player ? Mathf.Repeat(player.TrackTNormalized, 1f) : 0f;
                float aheadMeters = player ? AheadMeters(playerT, meta.t) : float.PositiveInfinity;

                bool isAhead = aheadMeters >= 0f;
                bool withinProtectAhead = isAhead && aheadMeters <= protectAheadMeters;

                // 1) Miss only when clearly behind by threshold
                if (!isAhead && Mathf.Abs(aheadMeters) >= despawnBehindMeters)
                {
                    meta.go.SetActive(false);
                    _active.Remove(meta.go);
                    _activeMeta.RemoveAt(i);
                    if (SkillEstimator.Instance) SkillEstimator.Instance.OnCollectibleMissed();
                    continue;
                }

                // 2) Hard cleanup: very old AND far (don’t count as miss)
                if (hardMaxLifetimeSeconds > 0f && age > hardMaxLifetimeSeconds && !withinProtectAhead)
                {
                    meta.go.SetActive(false);
                    _active.Remove(meta.go);
                    _activeMeta.RemoveAt(i);
                    // no telemetry here — we didn’t fairly pass it
                    continue;
                }

                // Otherwise: keep it
            }
            // per-iteration wait (runtime changes take effect)
            float wait = Mathf.Max(0.05f, spawnInterval);
            float t = 0f;
            while (t < wait) { t += Time.deltaTime; yield return null; }

            if (!PlayerJoinManager.IsRaceStarted)
                yield break;
        }
    }

    void TrySpawn()
    {
        var go = NextPooled();
        if (!go) return;

        Spline spline = _spline.Spline;
        float len = spline.GetLength();
        if (len < 0.1f) return;

        for (int attempt = 0; attempt < maxPlacementAttempts; ++attempt)
        {
            float t = UnityEngine.Random.value;
            SplineUtility.Evaluate(spline, t, out float3 lp, out float3 lt, out _);
            Vector3 wp = transform.TransformPoint(lp);
            Vector3 wt = transform.TransformDirection(math.normalize(lt));
            Vector3 right = Vector3.Cross(Vector3.up, wt).normalized;

            int laneIdx = UnityEngine.Random.Range(0, laneVis.laneOffsets.Length);
            float offset = laneVis.laneOffsets[laneIdx];

            Vector3 candidate = wp + right * offset + Vector3.up * rayHeight;
            if (Physics.Raycast(candidate, Vector3.down, out var hit, rayHeight * 2f, groundMask))
                candidate = hit.point + Vector3.up * floatHeight;
            else
                candidate = wp + right * offset + Vector3.up * floatHeight;

            if (!IsClearOfPlayer(t)) continue;
            if (!IsFarEnough(candidate)) { if (drawAttemptGizmos) DebugDraw(candidate, Color.red); continue; }
            if (!IsFarFromObstacles(candidate)) { if (drawAttemptGizmos) DebugDraw(candidate, Color.red); continue; }

            if (IsFarEnough(candidate))
            {
                go.transform.SetPositionAndRotation(candidate, Quaternion.LookRotation(wt, Vector3.up));
                go.SetActive(true);
                _active.Add(go);
                _activeMeta.Add(new CollectableMeta { go = go, t = t, lane = laneIdx, spawnTime = Time.time });

                // NEW: telemetry
                if (SkillEstimator.Instance) SkillEstimator.Instance.OnCollectibleSpawned();
                if (drawAttemptGizmos) DebugDraw(candidate, Color.green);
                return;
            }
            else if (drawAttemptGizmos) DebugDraw(candidate, Color.red);
        }
    }

    bool IsFarEnough(Vector3 p)
    {
        float minSq = minSeparation * minSeparation;
        foreach (var g in _active)
            if ((g.transform.position - p).sqrMagnitude < minSq)
                return false;
        return true;
    }

    void DebugDraw(Vector3 p, Color c)
    {
#if UNITY_EDITOR
        Debug.DrawLine(p, p + Vector3.up * 3f, c, 0.9f);
#endif
    }

    public void ReturnCollectableToPool(GameObject g)
    {
        if (!g) return;
        g.SetActive(false);
        _active.Remove(g);

        // remove meta
        for (int i = _activeMeta.Count - 1; i >= 0; --i)
            if (_activeMeta[i].go == g) { _activeMeta.RemoveAt(i); break; }

        // telemetry: treat this path as "collected"
        if (SkillEstimator.Instance) SkillEstimator.Instance.OnCollectibleCollected();
    }

}
