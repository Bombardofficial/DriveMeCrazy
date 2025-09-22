using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

[System.Serializable]
public class ObstacleType
{
    public GameObject prefab;
    [Range(1, 100)] public int damage = 10;
    [Range(1, 100)] public int spawnWeight = 50;
}

[RequireComponent(typeof(SplineContainer))]
public class ObstacleManager : MonoBehaviour
{
    [Header("Obstacle Configuration")]
    [SerializeField] private ObstacleType[] obstacleTypes;

    [Header("Prefabs & Limits")]
    [Min(1)][SerializeField] int poolSize = 50;
    [Min(.1f)][SerializeField] float spawnInterval = 2f;

    [Header("Lane & Distance Settings")]
    [SerializeField] private LaneLinesVisualizer laneVis;
    [Min(1f)] public float minSeparation = 10f;
    [Range(1, 50)] public int maxPlacementAttempts = 15;

    [Header("Difficulty Scaling (legacy auto)")]
    [SerializeField] private int initialMaxActive = 5;
    [SerializeField] private int maxActiveCap = 40;
    [SerializeField] private float increaseInterval = 20f;
    [SerializeField] private int increaseAmount = 2;

    [Header("Vertical placement")]
    public float raycastHeight = 10f;
    public LayerMask groundMask;

    [Header("Debug • Scene view")]
    public bool drawAttemptGizmos = false;

    private Dictionary<GameObject, List<GameObject>> _pool = new();
    private readonly List<GameObject> _active = new();
    private SplineContainer _spline;
    private int _currentMaxActive;
    private int _totalBaseWeight;

    [Header("Audio")]
    [SerializeField] private AudioSourcePool obstacleAudioPool;

    // !!! DIFFICULTY — live driving knobs
    [Header("Dynamic Difficulty (Director)")]
    public bool useDirector = true;

    [Tooltip("Active obstacles allowed by difficulty. x=0?min, x=1?max")]
    public AnimationCurve maxActiveByDiff = AnimationCurve.Linear(0, 6, 1, 32);

    [Tooltip("Seconds between spawn attempts by difficulty (smaller is harder)")]
    public AnimationCurve intervalByDiff = AnimationCurve.Linear(0, 2.8f, 1, 0.8f);

    [Tooltip("Minimum distance between obstacles by difficulty (smaller is harder)")]
    public AnimationCurve separationByDiff = AnimationCurve.Linear(0, 14f, 1, 8f);

    [Tooltip("Bias toward dangerous obstacles (by damage). 0=no bias, 1=max bias.")]
    public AnimationCurve dangerBiasByDiff = AnimationCurve.Linear(0, 0f, 1, 1f);


    // Strategic placement
    [Header("Strategic Placement (uses CollectableManager metadata)")]
    public CollectableManager collectableMgr;
    [Tooltip("0=never strategic, 1=always strategic spawn when possible")]
    public AnimationCurve guardProbByDiff = AnimationCurve.Linear(0, 0.15f, 1, 0.75f);

    [Tooltip("Meters before (+) / after (-) collectible to place obstacle (random within range)")]
    public Vector2 guardOffsetRangeMeters = new Vector2(2.5f, 6.0f);

    [Range(0f, 1f), Tooltip("Probability to use the SAME lane as the collectible (else neighbor)")]
    public AnimationCurve sameLaneProbByDiff = AnimationCurve.Linear(0, 0.45f, 1, 0.8f);

    [Tooltip("Max collectible age (s) to consider for guarding (prevents guarding fossils).")]
    public float maxGuardAge = 8f;

    [Tooltip("If true, treat strategic spawn failure as a regular random spawn fallback.")]
    public bool fallbackToRandomIfBlocked = true;
    [Header("Fairness")]
    [Tooltip("Meters ahead of the car where obstacles will not spawn.")]
    public float playerSafeAheadMeters = 18f;
    [Tooltip("Meters behind the car where obstacles will not spawn.")]
    public float playerSafeBehindMeters = 6f;
    [Tooltip("Minimum meters between an obstacle and any collectible.")]
    public float minCrossSeparationMeters = 3.0f;

    public ArcadeVP.ArcadeVehicleController player;   // assign in inspector

    // helpers
    float ArcLen() => _spline.Spline.GetLength();
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

    bool IsFarFromCollectibles(Vector3 p)
    {
        if (!collectableMgr) return true;
        float minSq = minCrossSeparationMeters * minCrossSeparationMeters;
        var metas = collectableMgr.ActiveMetas;
        if (metas == null) return true;
        for (int i = 0; i < metas.Count; i++)
        {
            var go = metas[i].go;
            if (!go || !go.activeInHierarchy) continue;
            if ((go.transform.position - p).sqrMagnitude < minSq) return false;
        }
        return true;
    }
    float DtFromMeters(Spline spline, float meters)
    {
        float len = spline.GetLength();
        if (len <= 0.001f) return 0f;
        return meters / len;
    }
    // cache of live weights (per type)
    float[] _liveWeights;

    // smoothers
    float _curInterval;
    float _curSeparation;
    float _curMaxActive;

    void Awake()
    {
        if (obstacleTypes == null || obstacleTypes.Length == 0) { Debug.LogError("ObstacleManager: No Obstacle Types defined!"); enabled = false; return; }
        _spline = GetComponent<SplineContainer>();
        if (!laneVis) laneVis = GetComponent<LaneLinesVisualizer>();
        if (!laneVis) { Debug.LogError("ObstacleManager: LaneLinesVisualizer ref missing"); enabled = false; return; }

        _currentMaxActive = initialMaxActive;
        _totalBaseWeight = obstacleTypes.Sum(t => t.spawnWeight);
        _liveWeights = obstacleTypes.Select(t => (float)t.spawnWeight).ToArray();

        BuildPool();
        StartCoroutine(SpawnLoop());

        // legacy auto-increase can be left ON if !useDirector
        if (!useDirector)
            StartCoroutine(IncreaseDifficultyLoop());
    }


    bool TrySpawnStrategic(float diff)
    {
        if (!collectableMgr) return false;
        var metas = collectableMgr.ActiveMetas;
        if (metas == null || metas.Count == 0) return false;

        // choose a fresh collectible to guard
        int start = UnityEngine.Random.Range(0, metas.Count);
        Spline spline = _spline.Spline;

        for (int step = 0; step < metas.Count; ++step)
        {
            var m = metas[(start + step) % metas.Count];
            if (!m.go || !m.go.activeInHierarchy) continue;
            if (Time.time - m.spawnTime > maxGuardAge) continue;

            // pick offset (before or after)
            float meters = UnityEngine.Random.Range(guardOffsetRangeMeters.x, guardOffsetRangeMeters.y);
            bool before = UnityEngine.Random.value < 0.65f;       // bias to “before” collectible
            float dt = DtFromMeters(spline, meters) * (before ? -1f : +1f);
            float t2 = Mathf.Repeat(m.t + dt, 1f);

            // Evaluate world pos/orientation
            SplineUtility.Evaluate(spline, t2, out float3 lp2, out float3 lt2, out _);
            Vector3 wp2 = transform.TransformPoint(lp2);
            Vector3 wt2 = transform.TransformDirection(math.normalize(lt2));
            Vector3 right2 = Vector3.Cross(Vector3.up, wt2).normalized;

            // choose lane
            float pSame = Mathf.Clamp01(sameLaneProbByDiff.Evaluate(diff));
            int lane = (UnityEngine.Random.value < pSame) ? m.lane :
                       Mathf.Clamp(m.lane + (UnityEngine.Random.value < 0.5f ? -1 : +1), 0, collectableMgr.laneVis.laneOffsets.Length - 1);
            float offset = collectableMgr.laneVis.laneOffsets[lane];

            // vertical ray to ground
            Vector3 rayStart = wp2 + right2 * offset + Vector3.up * raycastHeight;
            if (!Physics.Raycast(rayStart, Vector3.down, out var hit, raycastHeight * 2f, groundMask))
                continue;

            Vector3 finalPos = hit.point + Vector3.up * 0.5f;

            if (!IsClearOfPlayer(t2)) continue;
            if (!IsFarEnough(finalPos)) continue;
            if (!IsFarFromCollectibles(finalPos)) continue;

            // choose type with live weights
            ObstacleType type = GetRandomObstacleType(diff);
            var go = NextPooled(type.prefab);
            if (!go) return false;

            go.transform.SetPositionAndRotation(finalPos, Quaternion.LookRotation(wt2, Vector3.up));
            go.SetActive(true);
            _active.Add(go);
            // telemetry
            if (SkillEstimator.Instance) SkillEstimator.Instance.OnObstacleSpawned();

            if (drawAttemptGizmos) DebugDraw(finalPos, Color.yellow);
            return true;
        }
        return false;
    }

    IEnumerator IncreaseDifficultyLoop()
    {
        yield return new WaitForSeconds(increaseInterval);
        while (PlayerJoinManager.IsRaceStarted && _currentMaxActive < maxActiveCap)
        {
            _currentMaxActive = Mathf.Min(_currentMaxActive + increaseAmount, maxActiveCap);
            yield return new WaitForSeconds(increaseInterval);
        }
    }

    void BuildPool()
    {
        float totalWeight = obstacleTypes.Sum(t => t.spawnWeight);
        foreach (var type in obstacleTypes)
        {
            List<GameObject> subPool = new();
            _pool.Add(type.prefab, subPool);
            int amountToCreate = Mathf.RoundToInt((type.spawnWeight / totalWeight) * poolSize);
            for (int i = 0; i < amountToCreate; ++i)
            {
                var go = Instantiate(type.prefab, Vector3.zero, Quaternion.identity, transform);
                go.SetActive(false);
                if (go.TryGetComponent(out Obstacle obs))
                {
                    obs.manager = this;
                    obs.audioPool = obstacleAudioPool;
                    obs.damageToInflict = type.damage;
                }
                subPool.Add(go);
            }
        }
    }
    public List<Vector3> GetActiveWorldPositions()
    {
        _tmp.Clear();
        for (int i = 0; i < _active.Count; i++)
            if (_active[i]) _tmp.Add(_active[i].transform.position);
        return _tmp;
    }
    readonly List<Vector3> _tmp = new();

    GameObject NextPooled(GameObject prefab)
    {
        if (_pool.TryGetValue(prefab, out var subPool))
        {
            foreach (var go in subPool)
                if (!go.activeInHierarchy) return go;
        }
        return null;
    }

    // !!! DIFFICULTY — compute live weights (bias toward higher-damage prefabs)
    void RefreshLiveWeights(float diff)
    {
        float bias = Mathf.Clamp01(dangerBiasByDiff.Evaluate(diff));
        float maxDamage = Mathf.Max(1, obstacleTypes.Max(t => t.damage));
        for (int i = 0; i < obstacleTypes.Length; i++)
        {
            float baseW = Mathf.Max(1, obstacleTypes[i].spawnWeight);
            float danger01 = obstacleTypes[i].damage / maxDamage;
            // Lerp: base weight ? emphasize more dangerous ones as diff grows
            _liveWeights[i] = baseW * Mathf.Lerp(1f, Mathf.Lerp(0.6f, 2.0f, danger01), bias);
        }
    }

    // !!! DIFFICULTY — weighted pick using _liveWeights
    ObstacleType GetRandomObstacleType(float diff)
    {
        if (useDirector) RefreshLiveWeights(diff);

        float total = useDirector ? _liveWeights.Sum() : _totalBaseWeight;
        float pick = UnityEngine.Random.Range(0f, total);
        float acc = 0f;

        for (int i = 0; i < obstacleTypes.Length; i++)
        {
            float w = useDirector ? _liveWeights[i] : obstacleTypes[i].spawnWeight;
            acc += w;
            if (pick <= acc) return obstacleTypes[i];
        }
        return obstacleTypes[obstacleTypes.Length - 1];
    }

    // !!! DIFFICULTY — apply curves & smoothing
    void ApplyDifficulty(float diff, float dt)
    {
        if (!useDirector) return;

        float targetMax = Mathf.Clamp(maxActiveByDiff.Evaluate(diff), 1f, maxActiveCap);
        float targetInt = Mathf.Max(0.15f, intervalByDiff.Evaluate(diff));
        float targetSep = Mathf.Max(2f, separationByDiff.Evaluate(diff));

        float k = 1f - Mathf.Exp(-5f * dt);   // smoothing factor (0..1)

        _curMaxActive = Mathf.Lerp(_curMaxActive <= 0 ? initialMaxActive : _curMaxActive, targetMax, k);
        _curInterval = Mathf.Lerp(_curInterval <= 0 ? spawnInterval : _curInterval, targetInt, k);
        _curSeparation = Mathf.Lerp(_curSeparation <= 0 ? minSeparation : _curSeparation, targetSep, k);

        _currentMaxActive = Mathf.RoundToInt(_curMaxActive);
        spawnInterval = _curInterval;
        minSeparation = _curSeparation;
    }


    IEnumerator SpawnLoop()
    {
        // Wait for race start
        while (!PlayerJoinManager.IsRaceStarted) yield return null;

        // Main loop
        while (enabled)
        {
            float diff = DifficultyDirector.Instance ? DifficultyDirector.Instance.Current.spawnPressure : 0.5f;

            ApplyDifficulty(diff, Time.deltaTime);

            if (_active.Count < _currentMaxActive)
            {
                // strategic decision
                float guardP = Mathf.Clamp01(guardProbByDiff.Evaluate(diff));
                bool didStrategic = false;
                if (UnityEngine.Random.value < guardP)
                    didStrategic = TrySpawnStrategic(diff);

                if (!didStrategic) TrySpawn(diff);
            }

            // !!! DIFFICULTY — per-iteration wait so runtime changes take effect
            float wait = Mathf.Max(0.05f, spawnInterval);
            float t = 0f;
            while (t < wait) { t += Time.deltaTime; yield return null; }

            if (!PlayerJoinManager.IsRaceStarted) yield break;
        }
    }

    void TrySpawn(float diff)
    {
        ObstacleType typeToSpawn = GetRandomObstacleType(diff);
        var go = NextPooled(typeToSpawn.prefab);
        if (!go) return;

        Spline spline = _spline.Spline;
        if (spline.GetLength() < 0.1f) return;

        for (int attempt = 0; attempt < maxPlacementAttempts; ++attempt)
        {
            float t = UnityEngine.Random.value;
            SplineUtility.Evaluate(spline, t, out float3 lp, out float3 lt, out _);
            Vector3 wp = transform.TransformPoint(lp);
            Vector3 wt = transform.TransformDirection(math.normalize(lt));
            Vector3 right = Vector3.Cross(Vector3.up, wt).normalized;

            int laneIdx = UnityEngine.Random.Range(0, laneVis.laneOffsets.Length);
            float offset = laneVis.laneOffsets[laneIdx];

            Vector3 rayStart = wp + right * offset + Vector3.up * raycastHeight;
            Vector3 finalPos;

            if (Physics.Raycast(rayStart, Vector3.down, out var hit, raycastHeight * 2f, groundMask))
            {
                finalPos = hit.point + Vector3.up * 0.5f;
            }
            else
            {
                continue;
            }

            if (!IsClearOfPlayer(t)) continue;
            if (!IsFarEnough(finalPos)) continue;
            if (!IsFarFromCollectibles(finalPos)) continue;

            if (IsFarEnough(finalPos))
            {
                go.transform.SetPositionAndRotation(finalPos, Quaternion.LookRotation(wt, Vector3.up));
                go.SetActive(true);
                _active.Add(go);
                if (drawAttemptGizmos) DebugDraw(finalPos, Color.magenta);
                return;
            }
            else if (drawAttemptGizmos)
                DebugDraw(finalPos, Color.red);
        }
    }

    bool IsFarEnough(Vector3 p)
    {
        float minSq = minSeparation * minSeparation;
        foreach (var g in _active)
            if ((g.transform.position - p).sqrMagnitude < minSq) return false;
        return true;
    }

    public void ReturnObstacleToPool(GameObject g)
    {
        if (!g) return;
        g.SetActive(false);
        _active.Remove(g);
        if (g.TryGetComponent(out Obstacle obs)) obs.ResetState();
    }

    void DebugDraw(Vector3 p, Color c)
    {
#if UNITY_EDITOR
        Debug.DrawLine(p, p + Vector3.up * 3f, c, 0.9f);
#endif
    }
}
