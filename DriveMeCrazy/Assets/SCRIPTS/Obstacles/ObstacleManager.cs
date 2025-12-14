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

    [Header("Lane Tracks (separate splines)")]
    [Tooltip("If true, spawns use laneContainers directly. Order MUST be: 0=RIGHT, 1=CENTER, 2=LEFT.")]
    public bool useSeparateLaneSplines = true;

    [Tooltip("Lane spline containers. 0=RIGHT, 1=CENTER, 2=LEFT.")]
    public SplineContainer[] laneContainers = new SplineContainer[3];

    [Header("Lane & Distance Settings (legacy offset mode)")]
    [SerializeField] private LaneLinesVisualizer laneVis;
    [Min(1f)] public float minSeparation = 10f;
    [Range(1, 50)] public int maxPlacementAttempts = 15;

    [Header("Lane Offset Source (legacy offset mode)")]
    [Tooltip("True = laneVis.laneOffsets. False = fixed 3 lanes: -d, 0, +d (d from player.laneOffsetDistance if available).")]
    public bool useLaneVisualizerOffsets = true;

    [Tooltip("Only used if useLaneVisualizerOffsets is false and player is not assigned.")]
    public float fallbackLaneOffsetDistance = 2f;

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

    [Header("Dynamic Difficulty (Director)")]
    public bool useDirector = true;

    public AnimationCurve maxActiveByDiff = AnimationCurve.Linear(0, 6, 1, 32);
    public AnimationCurve intervalByDiff = AnimationCurve.Linear(0, 2.8f, 1, 0.8f);
    public AnimationCurve separationByDiff = AnimationCurve.Linear(0, 14f, 1, 8f);
    public AnimationCurve dangerBiasByDiff = AnimationCurve.Linear(0, 0f, 1, 1f);

    [Header("Strategic Placement (uses CollectableManager metadata)")]
    public CollectableManager collectableMgr;
    public AnimationCurve guardProbByDiff = AnimationCurve.Linear(0, 0.15f, 1, 0.75f);
    public Vector2 guardOffsetRangeMeters = new Vector2(2.5f, 6.0f);
    public AnimationCurve sameLaneProbByDiff = AnimationCurve.Linear(0, 0.45f, 1, 0.8f);
    public float maxGuardAge = 8f;
    public bool fallbackToRandomIfBlocked = true;

    [Header("Fairness")]
    public float playerSafeAheadMeters = 18f;
    public float playerSafeBehindMeters = 6f;
    public float minCrossSeparationMeters = 3.0f;

    public ArcadeVP.ArcadeVehicleController player;

    bool HasSeparateLaneSplinesLocal()
    {
        if (!useSeparateLaneSplines) return false;
        if (laneContainers == null || laneContainers.Length < 3) return false;
        return laneContainers[0] != null && laneContainers[1] != null && laneContainers[2] != null;
    }

    bool HasSeparateLaneSplinesFromCollectables()
    {
        if (!collectableMgr) return false;
        if (!collectableMgr.useSeparateLaneSplines) return false;
        if (collectableMgr.laneContainers == null || collectableMgr.laneContainers.Length < 3) return false;
        return collectableMgr.laneContainers[0] != null && collectableMgr.laneContainers[1] != null && collectableMgr.laneContainers[2] != null;
    }

    bool HasSeparateLaneSplines()
    {
        return HasSeparateLaneSplinesLocal() || HasSeparateLaneSplinesFromCollectables();
    }

    SplineContainer GetLaneContainer(int laneIdx)
    {
        int i = Mathf.Clamp(laneIdx, 0, 2);

        if (HasSeparateLaneSplinesLocal())
            return laneContainers[i];

        if (HasSeparateLaneSplinesFromCollectables())
            return collectableMgr.laneContainers[i];

        return _spline;
    }

    float GetLaneLength(int laneIdx)
    {
        var sc = GetLaneContainer(laneIdx);
        if (!sc) return 0f;
        return sc.Spline.GetLength();
    }

    bool TryGetLaneFrame(int laneIdx, float t, out Vector3 worldPos, out Vector3 worldTangent, out Vector3 worldUp, out Vector3 worldRight)
    {
        worldPos = default;
        worldTangent = default;
        worldUp = Vector3.up;
        worldRight = Vector3.right;

        var sc = GetLaneContainer(laneIdx);
        if (!sc) return false;

        var spline = sc.Spline;
        float len = spline.GetLength();
        if (len < 0.1f) return false;

        t = Mathf.Repeat(t, 1f);

        SplineUtility.Evaluate(spline, t, out float3 lp, out float3 lt, out float3 lu);

        worldPos = sc.transform.TransformPoint(lp);

        Vector3 wt = sc.transform.TransformDirection(math.normalizesafe(lt));
        Vector3 wu = sc.transform.TransformDirection(math.normalizesafe(lu));

        if (wt.sqrMagnitude < 0.0001f) wt = sc.transform.forward;
        if (wu.sqrMagnitude < 0.0001f) wu = Vector3.up;

        worldTangent = wt.normalized;
        worldUp = wu.normalized;

        worldRight = Vector3.Cross(worldTangent, worldUp).normalized;
        if (worldRight.sqrMagnitude < 0.0001f)
            worldRight = Vector3.Cross(Vector3.up, worldTangent).normalized;

        return true;
    }

    float ArcLen()
    {
        if (player && player.splineContainer != null)
        {
            float pl = player.splineContainer.Spline.GetLength();
            if (pl > 0.1f) return pl;
        }

        if (HasSeparateLaneSplines())
        {
            var center = GetLaneContainer(1);
            if (center)
            {
                float cl = center.Spline.GetLength();
                if (cl > 0.1f) return cl;
            }
        }

        if (_spline) return _spline.Spline.GetLength();
        return 0f;
    }

    bool UsingLaneVis()
    {
        if (!useLaneVisualizerOffsets) return false;
        if (!laneVis) return false;
        if (laneVis.laneOffsets == null) return false;
        return laneVis.laneOffsets.Length > 0;
    }

    float GetLaneStep()
    {
        if (player && player.laneOffsetDistance > 0.01f) return player.laneOffsetDistance;
        if (fallbackLaneOffsetDistance > 0.01f) return fallbackLaneOffsetDistance;
        return 2f;
    }

    int SpawnLaneCount
    {
        get
        {
            if (HasSeparateLaneSplines()) return 3;
            if (UsingLaneVis()) return laneVis.laneOffsets.Length;
            return 3;
        }
    }

    float GetLaneOffsetByIndex(int laneIdx)
    {
        // LEGACY offset mode only (ignored in separate lane spline mode)
        if (UsingLaneVis())
        {
            int i = Mathf.Clamp(laneIdx, 0, laneVis.laneOffsets.Length - 1);
            return laneVis.laneOffsets[i];
        }

        float d = GetLaneStep();
        int i3 = Mathf.Clamp(laneIdx, 0, 2);
        return (i3 - 1) * d;
    }

    bool IsClearOfPlayer(float candidateT)
    {
        if (!player) return true;
        float len = ArcLen();
        if (len <= 0.001f) return true;

        float tCar = Mathf.Repeat(player.TrackTNormalized, 1f);
        float wrap(float d) => (d < 0f) ? d + 1f : d;
        float dt = wrap(candidateT - tCar);
        float ahead = dt * len;
        float behind = (1f - dt) * len;
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

    float DtFromMeters(int laneIdx, float meters)
    {
        float len = GetLaneLength(laneIdx);
        if (len <= 0.001f) return 0f;
        return meters / len;
    }

    float[] _liveWeights;
    float _curInterval;
    float _curSeparation;
    float _curMaxActive;

    void Awake()
    {
        if (obstacleTypes == null || obstacleTypes.Length == 0) { Debug.LogError("ObstacleManager: No Obstacle Types defined!"); enabled = false; return; }

        _spline = GetComponent<SplineContainer>();

        if (!laneVis) laneVis = GetComponent<LaneLinesVisualizer>();

        if (useSeparateLaneSplines && !HasSeparateLaneSplines())
        {
            Debug.LogError("ObstacleManager: useSeparateLaneSplines ON, but laneContainers[0..2] not assigned (or CollectableManager laneContainers missing). Order: 0=RIGHT, 1=CENTER, 2=LEFT.");
            enabled = false;
            return;
        }

        if (!HasSeparateLaneSplines() && useLaneVisualizerOffsets && !UsingLaneVis())
        {
            Debug.LogWarning("ObstacleManager: useLaneVisualizerOffsets true, but laneVis or laneOffsets missing. Switching to fixed 3-lane fallback.");
            useLaneVisualizerOffsets = false;
        }

        _currentMaxActive = initialMaxActive;
        _totalBaseWeight = obstacleTypes.Sum(t => t.spawnWeight);
        _liveWeights = obstacleTypes.Select(t => (float)t.spawnWeight).ToArray();

        BuildPool();
        StartCoroutine(SpawnLoop());

        if (!useDirector)
            StartCoroutine(IncreaseDifficultyLoop());
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
            amountToCreate = Mathf.Max(1, amountToCreate);

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

    readonly List<Vector3> _tmp = new();
    public List<Vector3> GetActiveWorldPositions()
    {
        _tmp.Clear();
        for (int i = 0; i < _active.Count; i++)
            if (_active[i]) _tmp.Add(_active[i].transform.position);
        return _tmp;
    }

    GameObject NextPooled(GameObject prefab)
    {
        if (_pool.TryGetValue(prefab, out var subPool))
        {
            foreach (var go in subPool)
                if (!go.activeInHierarchy) return go;
        }
        return null;
    }

    void RefreshLiveWeights(float diff)
    {
        float bias = Mathf.Clamp01(dangerBiasByDiff.Evaluate(diff));
        float maxDamage = Mathf.Max(1, obstacleTypes.Max(t => t.damage));

        for (int i = 0; i < obstacleTypes.Length; i++)
        {
            float baseW = Mathf.Max(1, obstacleTypes[i].spawnWeight);
            float danger01 = obstacleTypes[i].damage / maxDamage;
            _liveWeights[i] = baseW * Mathf.Lerp(1f, Mathf.Lerp(0.6f, 2.0f, danger01), bias);
        }
    }

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

    void ApplyDifficulty(float diff, float dt)
    {
        if (!useDirector) return;

        float targetMax = Mathf.Clamp(maxActiveByDiff.Evaluate(diff), 1f, maxActiveCap);
        float targetInt = Mathf.Max(0.15f, intervalByDiff.Evaluate(diff));
        float targetSep = Mathf.Max(2f, separationByDiff.Evaluate(diff));

        float k = 1f - Mathf.Exp(-5f * dt);

        _curMaxActive = Mathf.Lerp(_curMaxActive <= 0 ? initialMaxActive : _curMaxActive, targetMax, k);
        _curInterval = Mathf.Lerp(_curInterval <= 0 ? spawnInterval : _curInterval, targetInt, k);
        _curSeparation = Mathf.Lerp(_curSeparation <= 0 ? minSeparation : _curSeparation, targetSep, k);

        _currentMaxActive = Mathf.RoundToInt(_curMaxActive);
        spawnInterval = _curInterval;
        minSeparation = _curSeparation;
    }

    bool TrySpawnStrategic(float diff)
    {
        if (!collectableMgr) return false;
        var metas = collectableMgr.ActiveMetas;
        if (metas == null || metas.Count == 0) return false;

        int start = UnityEngine.Random.Range(0, metas.Count);

        int laneCount = SpawnLaneCount;

        for (int step = 0; step < metas.Count; ++step)
        {
            var m = metas[(start + step) % metas.Count];
            if (!m.go || !m.go.activeInHierarchy) continue;
            if (Time.time - m.spawnTime > maxGuardAge) continue;

            float meters = UnityEngine.Random.Range(guardOffsetRangeMeters.x, guardOffsetRangeMeters.y);
            bool before = UnityEngine.Random.value < 0.65f;

            float pSame = Mathf.Clamp01(sameLaneProbByDiff.Evaluate(diff));

            int lane = (UnityEngine.Random.value < pSame) ? m.lane :
                       Mathf.Clamp(m.lane + (UnityEngine.Random.value < 0.5f ? -1 : +1), 0, laneCount - 1);

            float dt = DtFromMeters(lane, meters) * (before ? -1f : +1f);
            float t2 = Mathf.Repeat(m.t + dt, 1f);

            Vector3 wp2, wt2, wu2, right2;
            if (!TryGetLaneFrame(lane, t2, out wp2, out wt2, out wu2, out right2))
                continue;

            Vector3 rayStart;
            if (HasSeparateLaneSplines())
            {
                rayStart = wp2 + Vector3.up * raycastHeight;
            }
            else
            {
                float offset = collectableMgr ? collectableMgr.GetLaneOffsetByIndex(lane) : GetLaneOffsetByIndex(lane);
                rayStart = wp2 + right2 * offset + Vector3.up * raycastHeight;
            }

            if (!Physics.Raycast(rayStart, Vector3.down, out var hit, raycastHeight * 2f, groundMask, QueryTriggerInteraction.Ignore))
                continue;

            Vector3 finalPos = hit.point + Vector3.up * 0.5f;

            if (!IsClearOfPlayer(t2)) continue;
            if (!IsFarEnough(finalPos)) continue;
            if (!IsFarFromCollectibles(finalPos)) continue;

            ObstacleType type = GetRandomObstacleType(diff);
            var go = NextPooled(type.prefab);
            if (!go) return false;

            if (wt2.sqrMagnitude < 0.0001f) wt2 = transform.forward;
            if (wu2.sqrMagnitude < 0.0001f) wu2 = Vector3.up;

            go.transform.SetPositionAndRotation(finalPos, Quaternion.LookRotation(wt2, wu2));
            go.SetActive(true);
            _active.Add(go);

            if (SkillEstimator.Instance) SkillEstimator.Instance.OnObstacleSpawned();
            if (drawAttemptGizmos) DebugDraw(finalPos, Color.yellow);
            return true;
        }

        return false;
    }

    IEnumerator SpawnLoop()
    {
        while (!PlayerJoinManager.IsRaceStarted) yield return null;

        while (enabled)
        {
            float diff = DifficultyDirector.Instance ? DifficultyDirector.Instance.Current.spawnPressure : 0.5f;

            ApplyDifficulty(diff, Time.deltaTime);

            if (_active.Count < _currentMaxActive)
            {
                float guardP = Mathf.Clamp01(guardProbByDiff.Evaluate(diff));
                bool didStrategic = false;

                if (UnityEngine.Random.value < guardP)
                    didStrategic = TrySpawnStrategic(diff);

                if (!didStrategic)
                    TrySpawn(diff);
            }

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

        int laneCount = SpawnLaneCount;
        if (laneCount <= 0) return;

        for (int attempt = 0; attempt < maxPlacementAttempts; ++attempt)
        {
            float t = UnityEngine.Random.value;
            int laneIdx = UnityEngine.Random.Range(0, laneCount);

            Vector3 wp, wt, wu, right;
            if (!TryGetLaneFrame(laneIdx, t, out wp, out wt, out wu, out right))
                continue;

            Vector3 rayStart;
            if (HasSeparateLaneSplines())
            {
                rayStart = wp + Vector3.up * raycastHeight;
            }
            else
            {
                float offset = GetLaneOffsetByIndex(laneIdx);
                rayStart = wp + right * offset + Vector3.up * raycastHeight;
            }

            if (!Physics.Raycast(rayStart, Vector3.down, out var hit, raycastHeight * 2f, groundMask, QueryTriggerInteraction.Ignore))
                continue;

            Vector3 finalPos = hit.point + Vector3.up * 0.5f;

            if (!IsClearOfPlayer(t)) continue;
            if (!IsFarEnough(finalPos)) continue;
            if (!IsFarFromCollectibles(finalPos)) continue;

            if (wt.sqrMagnitude < 0.0001f) wt = transform.forward;
            if (wu.sqrMagnitude < 0.0001f) wu = Vector3.up;

            go.transform.SetPositionAndRotation(finalPos, Quaternion.LookRotation(wt, wu));
            go.SetActive(true);
            _active.Add(go);

            if (SkillEstimator.Instance) SkillEstimator.Instance.OnObstacleSpawned();
            if (drawAttemptGizmos) DebugDraw(finalPos, Color.magenta);
            return;
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
