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
    [System.Serializable]
    public struct ObstacleMeta
    {
        public GameObject go;
        public int id;
        public float t;         // 0..1 position on spline
        public int lane;        // lane index (for debug / future)
        public float spawnTime; // Time.time at spawn
    }

    private int obstacleId = 0;

    public IReadOnlyList<ObstacleMeta> ActiveMetas => _activeMeta;
    readonly List<ObstacleMeta> _activeMeta = new();

    [Header("Debug")]
    public bool debugLogs = false;
    public float debugLogCooldown = 1.0f;
    float _nextLogTime = 0f;

    [Tooltip("Ha a ground raycast nem talál semmit, fallbackból így is lerakja az obstacle-t (debughoz kurva hasznos).")]
    public bool allowSpawnWithoutGroundHit = true;

    [Tooltip("Ennyi obstacle spawn próbálkozás történjen egy tickben (így nem csak 1db lesz a pályán).")]
    [Range(1, 8)] public int spawnsPerTick = 2;

    void DLog(string msg)
    {
        if (!debugLogs) return;
        if (Time.time < _nextLogTime) return;
        _nextLogTime = Time.time + Mathf.Max(0.1f, debugLogCooldown);
        Debug.Log(msg, this);
    }

    [Header("Obstacle Configuration")]
    [SerializeField] private ObstacleType[] obstacleTypes;

    [Header("Prefabs & Limits")]
    [Min(1)][SerializeField] int poolSize = 50;
    [Min(.1f)][SerializeField] float spawnInterval = 2f;

    [Header("Pool growth safety (prevents silent under-spawn)")]
    [Tooltip("Ha elfogy a pool egy prefabhoz, automatikusan növeli (betonstabil gameplay-hez ajánlott).")]
    public bool allowPoolGrowth = true;

    [Tooltip("Ennyi példányt ad hozzá egyszerre, ha elfogyott egy prefab poolja.")]
    [Min(1)] public int poolGrowBatch = 2;

    [Tooltip("Max példány egyetlen prefabhoz (védelem runaway növekedés ellen).")]
    [Min(1)] public int maxPoolPerPrefab = 128;

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
    [SerializeField] private SplineContainer trackSpline;

    [Header("Spawn window (relative to player)")]
    public float spawnAheadMinMeters = 22f;
    public float spawnAheadMaxMeters = 85f;

    [Header("Strategic vs Random mix")]
    public AnimationCurve strategicShareBySpawnPressure = AnimationCurve.Linear(0, 0.15f, 1, 0.55f);
    // rewardBias magas (sok csillag) -> kevesebb guard
    public AnimationCurve guardSuppressionByRewardBias = AnimationCurve.Linear(0, 1.0f, 1, 0.35f);

    [Header("Guard behavior shaping")]
    public AnimationCurve guardBeforeProbBySpawnPressure = AnimationCurve.Linear(0, 0.25f, 1, 0.75f);
    public float guardCooldownSeconds = 2.0f;
    public float guardRadiusMeters = 9f;
    public int maxGuardsNearOneCollectible = 1;

    // ===================== NEW: EXPIRY / CLEANUP (THIS FIXES YOUR “NO SPAWN AFTER HALF” BUG) =====================
    [Header("Fairness • track-relative expiry (OBSTACLES)")]
    [Tooltip("Meters behind the player at which an obstacle is despawned (returned to pool).")]
    public float despawnBehindMeters = 14f;

    [Tooltip("While the obstacle is within this many meters AHEAD of the player, never expire it due to age.")]
    public float protectAheadMeters = 60f;

    [Tooltip("Hard cleanup: if older than this AND far away (ahead or behind), despawn (no skill event).")]
    public float hardMaxLifetimeSeconds = 35f;

    [Header("Density safety (prevents first-corner overfill)")]
    [Tooltip("Clamps effective maxActive to what fits INSIDE the spawn window. Strongly recommended ON.")]
    public bool clampMaxActiveToSpawnWindow = true;

    [Tooltip("0.65 -> ~65% of the theoretical max packing in the spawn window.")]
    [Range(0.1f, 1f)] public float windowCapMultiplier = 0.65f;

    [Tooltip("Minimum effective cap (so it never becomes too empty).")]
    [Min(1)] public int minWindowCap = 4;
    // ============================================================================================================

    readonly Dictionary<int, float> _lastGuardTimeByCollectibleId = new();

    float[] _liveWeights;
    float _curInterval;
    float _curSeparation;
    float _curMaxActive;

    static readonly List<GameObject> _tmpActivePrune = new();

    [Header("WarningTracker")]
    [SerializeField] private WarningTracker warningTracker;

    bool IsSplineLooped()
    {
        // Unity Splines: Spline.Closed == loop track
        if (!_spline || _spline.Spline == null) return true; // safe default: treat as loop
        return _spline.Spline.Closed;
    }

    float ArcLen()
    {
        if (!_spline || _spline.Spline == null) return 0f;
        return _spline.Spline.GetLength();
    }

    bool EnsureSplineRef()
    {
        if (!_spline) _spline = trackSpline ? trackSpline : GetComponent<SplineContainer>();

        if ((!_spline || _spline.Spline == null || _spline.Spline.GetLength() < 0.1f) && laneVis)
        {
            var sc = laneVis.GetComponent<SplineContainer>();
            if (sc) _spline = sc;
        }

        if (!_spline) _spline = GetComponent<SplineContainer>();
        return (_spline && _spline.Spline != null && _spline.Spline.GetLength() >= 0.1f);
    }

    // ---------- KEEP LISTS CONSISTENT ----------
    void PruneLists()
    {
        // meta -> also remove from _active if dead/inactive
        for (int i = _activeMeta.Count - 1; i >= 0; --i)
        {
            var go = _activeMeta[i].go;
            if (!go || !go.activeInHierarchy)
            {
                if (go) _active.Remove(go);
                _activeMeta.RemoveAt(i);
            }
        }

        // active (belt & suspenders)
        for (int i = _active.Count - 1; i >= 0; --i)
        {
            var g = _active[i];
            if (!g || !g.activeInHierarchy)
                _active.RemoveAt(i);
        }
    }

    void DeactivateAndUntrack(GameObject go)
    {
        if (!go) return;

        go.SetActive(false);
        _active.Remove(go);

        for (int i = _activeMeta.Count - 1; i >= 0; --i)
            if (_activeMeta[i].go == go) { _activeMeta.RemoveAt(i); break; }

        if (go.TryGetComponent(out Obstacle obs))
            obs.ResetState();
    }

    void GetAheadBehindMeters(float playerT, float itemT, float len, bool looped, out float aheadMeters, out float behindMeters, out bool isBehind)
    {
        aheadMeters = 999999f;
        behindMeters = 999999f;
        isBehind = false;

        if (len < 0.1f) return;

        if (looped)
        {
            float dtForward = Mathf.Repeat(itemT - playerT, 1f); // 0..1 forward distance from player to item
            isBehind = (dtForward > 0.5f);

            if (isBehind)
                behindMeters = (1f - dtForward) * len;
            else
                aheadMeters = dtForward * len;
        }
        else
        {
            // Open track: no wrap. If itemT < playerT -> behind.
            float d = itemT - playerT;
            if (d < 0f)
            {
                isBehind = true;
                behindMeters = (-d) * len;
            }
            else
            {
                aheadMeters = d * len;
            }
        }
    }

    int ComputeWindowCap(float len)
    {
        float windowLen = Mathf.Max(1f, spawnAheadMaxMeters - spawnAheadMinMeters);
        int lanes = (laneVis && laneVis.laneOffsets != null && laneVis.laneOffsets.Length > 0) ? laneVis.laneOffsets.Length : 1;

        float sep = Mathf.Max(2f, minSeparation);
        int perLane = Mathf.Max(1, Mathf.FloorToInt(windowLen / sep));

        int theoretical = perLane * lanes;
        int capped = Mathf.RoundToInt(theoretical * windowCapMultiplier);

        return Mathf.Max(minWindowCap, capped);
    }

    void ExpireObstacles(float len)
    {
        if (!player) return;
        if (!EnsureSplineRef()) return;

        bool looped = IsSplineLooped();
        float pT = looped ? Mathf.Repeat(player.TrackTNormalized, 1f) : Mathf.Clamp01(player.TrackTNormalized);

        for (int i = _activeMeta.Count - 1; i >= 0; --i)
        {
            var m = _activeMeta[i];

            if (!m.go || !m.go.activeInHierarchy)
            {
                if (m.go) _active.Remove(m.go);
                _activeMeta.RemoveAt(i);
                continue;
            }

            float age = Time.time - m.spawnTime;

            GetAheadBehindMeters(pT, m.t, len, looped, out float ahead, out float behind, out bool isBehind);

            bool withinProtectAhead = (!isBehind && ahead <= protectAheadMeters);

            // (1) Main rule: despawn if clearly behind
            if (isBehind && behind >= despawnBehindMeters)
            {
                DeactivateAndUntrack(m.go);
                continue;
            }

            // (2) Hard cleanup: very old AND far (don’t count as hit/miss)
            if (hardMaxLifetimeSeconds > 0f && age > hardMaxLifetimeSeconds && !withinProtectAhead)
            {
                bool farAhead = (!isBehind && ahead > protectAheadMeters);
                bool farBehind = (isBehind && behind > despawnBehindMeters);

                if (farAhead || farBehind)
                {
                    DeactivateAndUntrack(m.go);
                    continue;
                }
            }
        }
    }

    float PickSpawnT(float len)
    {
        if (!player || len < 0.1f) return UnityEngine.Random.value;

        bool looped = IsSplineLooped();
        float tCar = looped ? Mathf.Repeat(player.TrackTNormalized, 1f) : Mathf.Clamp01(player.TrackTNormalized);

        float minA = Mathf.Max(playerSafeAheadMeters + 0.5f, spawnAheadMinMeters);
        float maxA = Mathf.Max(minA + 1.0f, spawnAheadMaxMeters);

        float dtMin = minA / len;
        float dtMax = maxA / len;

        if (looped)
        {
            // keep “always ahead” within half lap
            dtMin = Mathf.Clamp(dtMin, 0.01f, 0.49f);
            dtMax = Mathf.Clamp(dtMax, 0.02f, 0.49f);

            if (dtMax <= dtMin + 0.0005f)
                return UnityEngine.Random.value;

            float dt = UnityEngine.Random.Range(dtMin, dtMax);
            return Mathf.Repeat(tCar + dt, 1f);
        }
        else
        {
            // Open track: NO WRAP. If not enough room, return -1 => no spawn.
            dtMin = Mathf.Max(0.0005f, dtMin);
            dtMax = Mathf.Max(dtMin + 0.0005f, dtMax);

            float tMin = tCar + dtMin;
            if (tMin >= 1f) return -1f;

            float tMax = Mathf.Min(1f, tCar + dtMax);
            if (tMax <= tMin) tMax = Mathf.Min(1f, tMin + 0.01f);

            return UnityEngine.Random.Range(tMin, tMax);
        }
    }

    bool IsClearOfPlayer(float candidateT)
    {
        if (!player) return true;

        float len = ArcLen();
        if (len < 0.1f) return true;

        bool looped = IsSplineLooped();
        float pT = looped ? Mathf.Repeat(player.TrackTNormalized, 1f) : Mathf.Clamp01(player.TrackTNormalized);

        GetAheadBehindMeters(pT, candidateT, len, looped, out float ahead, out float behind, out bool isBehind);

        // On open track: never allow behind spawns
        if (!looped && isBehind) return false;

        // On loop track: if it wrapped behind, reject (or at least require behind safe)
        if (isBehind) return false;

        if (ahead < playerSafeAheadMeters) return false;
        if (behind < playerSafeBehindMeters) return false;

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

    // ignore the target collectible when placing a guard
    bool IsFarFromCollectiblesExcept(Vector3 p, GameObject ignoreGo)
    {
        if (!collectableMgr) return true;
        float minSq = minCrossSeparationMeters * minCrossSeparationMeters;
        var metas = collectableMgr.ActiveMetas;
        if (metas == null) return true;

        for (int i = 0; i < metas.Count; i++)
        {
            var go = metas[i].go;
            if (!go || !go.activeInHierarchy) continue;
            if (go == ignoreGo) continue;

            if ((go.transform.position - p).sqrMagnitude < minSq) return false;
        }
        return true;
    }

    bool IsFarEnough(Vector3 p)
    {
        float minSq = minSeparation * minSeparation;
        foreach (var g in _active)
            if (g && g.activeInHierarchy && (g.transform.position - p).sqrMagnitude < minSq)
                return false;
        return true;
    }

    float DtFromMeters(Spline spline, float meters)
    {
        float len = spline.GetLength();
        if (len <= 0.001f) return 0f;
        return meters / len;
    }

    void Awake()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[ObstacleManager] Awake on '{gameObject.name}', enabled={enabled}, active={gameObject.activeInHierarchy}", this);
#endif

        if (obstacleTypes == null || obstacleTypes.Length == 0)
        {
            Debug.LogError("ObstacleManager: No Obstacle Types defined!", this);
            enabled = false;
            return;
        }

        if (!laneVis) laneVis = GetComponent<LaneLinesVisualizer>();
        if (!laneVis)
        {
            Debug.LogError("ObstacleManager: LaneLinesVisualizer ref missing", this);
            enabled = false;
            return;
        }

        _spline = trackSpline ? trackSpline : GetComponent<SplineContainer>();

        if (groundMask.value == 0)
            groundMask = laneVis.drivableSurface;

        _currentMaxActive = initialMaxActive;
        _totalBaseWeight = obstacleTypes.Sum(t => t.spawnWeight);
        _liveWeights = obstacleTypes.Select(t => (float)t.spawnWeight).ToArray();

        BuildPool();
        StartCoroutine(SpawnLoop());

        if (!useDirector)
            StartCoroutine(IncreaseDifficultyLoop());
    }

    IEnumerator SpawnLoop()
    {
        while (!PlayerJoinManager.IsRaceStarted)
        {
            DLog("ObstacleManager: waiting for PlayerJoinManager.IsRaceStarted...");
            yield return null;
        }

        while (!EnsureSplineRef())
        {
            DLog("ObstacleManager: spline not ready / length=0. Waiting...");
            yield return null;
        }

        DLog($"ObstacleManager: START. Spline='{_spline.name}' len={ArcLen():F2}, groundMask={groundMask.value}, looped={IsSplineLooped()}");

        while (enabled && PlayerJoinManager.IsRaceStarted)
        {
            PruneLists();

            float spawnPressure = DifficultyDirector.Instance ? DifficultyDirector.Instance.Current.spawnPressure : 0.5f;
            float rewardBias = DifficultyDirector.Instance ? DifficultyDirector.Instance.Current.rewardBias : 0.5f;

            ApplyDifficulty(spawnPressure, Time.deltaTime);

            float len = ArcLen();

            // NEW: expire behind/old obstacles so they can't "eat the cap" forever
            ExpireObstacles(len);

            // NEW: density clamp so the first corner can't get mega-stuffed
            int effectiveCap = _currentMaxActive;
            if (clampMaxActiveToSpawnWindow)
                effectiveCap = Mathf.Min(effectiveCap, ComputeWindowCap(len));

            int missing = Mathf.Max(0, effectiveCap - _active.Count);
            int budget = Mathf.Min(missing, Mathf.Max(1, spawnsPerTick));

            float strategicShare = Mathf.Clamp01(strategicShareBySpawnPressure.Evaluate(spawnPressure));
            float guardSupp = Mathf.Clamp01(guardSuppressionByRewardBias.Evaluate(rewardBias));

            for (int i = 0; i < budget; i++)
            {
                bool didStrategic = false;

                if (collectableMgr && UnityEngine.Random.value < strategicShare)
                {
                    float guardP = Mathf.Clamp01(guardProbByDiff.Evaluate(spawnPressure)) * guardSupp;
                    if (UnityEngine.Random.value < guardP)
                        didStrategic = TrySpawnStrategic(spawnPressure);
                }

                if (!didStrategic)
                    TrySpawn(spawnPressure);
            }

            float wait = Mathf.Max(0.05f, spawnInterval);
            float t = 0f;
            while (t < wait) { t += Time.deltaTime; yield return null; }
        }
    }

    bool TrySpawnStrategic(float diff)
    {
        if (!collectableMgr) return false;

        var metas = collectableMgr.ActiveMetas;
        if (metas == null || metas.Count == 0) return false;
        if (!EnsureSplineRef()) return false;

        if (collectableMgr.laneVis == null || collectableMgr.laneVis.laneOffsets == null || collectableMgr.laneVis.laneOffsets.Length == 0)
            return false;

        PruneLists();

        int start = UnityEngine.Random.Range(0, metas.Count);
        Spline spline = _spline.Spline;

        for (int step = 0; step < metas.Count; ++step)
        {
            var m = metas[(start + step) % metas.Count];
            if (!m.go || !m.go.activeInHierarchy) continue;
            if (Time.time - m.spawnTime > maxGuardAge) continue;

            int cid = m.go.GetInstanceID();
            if (_lastGuardTimeByCollectibleId.TryGetValue(cid, out float lastT))
                if (Time.time - lastT < guardCooldownSeconds)
                    continue;

            int near = 0;
            float r2 = guardRadiusMeters * guardRadiusMeters;

            for (int k = 0; k < _active.Count; k++)
            {
                var a = _active[k];
                if (!a || !a.activeInHierarchy) continue;

                if ((a.transform.position - m.go.transform.position).sqrMagnitude <= r2)
                    near++;
            }

            if (near >= maxGuardsNearOneCollectible)
                continue;

            float meters = UnityEngine.Random.Range(guardOffsetRangeMeters.x, guardOffsetRangeMeters.y);

            float beforeProb = Mathf.Clamp01(guardBeforeProbBySpawnPressure.Evaluate(diff));
            bool before = UnityEngine.Random.value < beforeProb;

            float dt = DtFromMeters(spline, meters) * (before ? -1f : +1f);
            float t2 = Mathf.Repeat(m.t + dt, 1f);

            // If open track, don't allow wrap guards either
            if (!IsSplineLooped())
            {
                float pT = player ? Mathf.Clamp01(player.TrackTNormalized) : 0f;
                if (t2 < pT) continue;
            }

            SplineUtility.Evaluate(spline, t2, out float3 lp2, out float3 lt2, out _);

            Vector3 wp2 = _spline.transform.TransformPoint(lp2);
            Vector3 wt2 = _spline.transform.TransformDirection(math.normalizesafe(lt2, new float3(0, 0, 1)));
            if (wt2.sqrMagnitude < 0.0001f) wt2 = Vector3.forward;
            wt2.Normalize();

            Vector3 right2 = Vector3.Cross(Vector3.up, wt2).normalized;
            if (right2.sqrMagnitude < 0.0001f) right2 = Vector3.right;

            float pSame = Mathf.Clamp01(sameLaneProbByDiff.Evaluate(diff));
            int lane = (UnityEngine.Random.value < pSame) ? m.lane :
                       Mathf.Clamp(m.lane + (UnityEngine.Random.value < 0.5f ? -1 : +1), 0, collectableMgr.laneVis.laneOffsets.Length - 1);

            float offset = collectableMgr.laneVis.laneOffsets[lane];

            Vector3 rayStart = wp2 + right2 * offset + Vector3.up * raycastHeight;
            Vector3 finalPos;

            if (Physics.Raycast(rayStart, Vector3.down, out var hit, raycastHeight * 2f, groundMask))
                finalPos = hit.point + Vector3.up * 0.5f;
            else
            {
                if (!allowSpawnWithoutGroundHit) continue;
                finalPos = (wp2 + right2 * offset) + Vector3.up * 0.5f;
            }

            if (!IsClearOfPlayer(t2)) continue;
            if (!IsFarEnough(finalPos)) continue;

            if (!IsFarFromCollectiblesExcept(finalPos, m.go)) continue;

            ObstacleType type = GetRandomObstacleType(diff);
            var go = NextPooled(type.prefab);
            if (!go) return false;

            go.transform.SetPositionAndRotation(finalPos, Quaternion.LookRotation(wt2, Vector3.up));
            go.SetActive(true);
            _active.Add(go);

            // WarningTracker stuff
            Obstacle obs = go.GetComponent<Obstacle>();
            if (obs != null)
            {
                obs.currentLane = lane;
                // obsComp.id = obstacleId++;
            }

            _activeMeta.Add(new ObstacleMeta { go = go, id = obstacleId, t = t2, lane = lane, spawnTime = Time.time });

            _lastGuardTimeByCollectibleId[cid] = Time.time;

            if (SkillEstimator.Instance) SkillEstimator.Instance.OnObstacleSpawned();
            if (drawAttemptGizmos) DebugDraw(finalPos, Color.yellow);

            DLog($"Obstacle STRATEGIC OK: t={t2:F3}, lane={lane}, pos={finalPos}");
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
        float totalWeight = obstacleTypes.Sum(t => Mathf.Max(0, t.spawnWeight));
        totalWeight = Mathf.Max(1f, totalWeight);

        foreach (var type in obstacleTypes)
        {
            if (!type.prefab)
            {
                Debug.LogError("ObstacleManager: obstacleTypes contains NULL prefab!", this);
                continue;
            }

            if (_pool.ContainsKey(type.prefab))
            {
                Debug.LogError($"ObstacleManager: duplicate prefab in obstacleTypes: {type.prefab.name}", this);
                continue;
            }

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

    bool TryGrowPool(GameObject prefab, int count)
    {
        if (!allowPoolGrowth || !prefab) return false;

        if (!_pool.TryGetValue(prefab, out var subPool))
        {
            subPool = new List<GameObject>();
            _pool.Add(prefab, subPool);
        }

        int canAdd = Mathf.Min(count, Mathf.Max(0, maxPoolPerPrefab - subPool.Count));
        if (canAdd <= 0) return false;

        ObstacleType typeConfig = null;
        for (int i = 0; i < obstacleTypes.Length; i++)
            if (obstacleTypes[i].prefab == prefab) { typeConfig = obstacleTypes[i]; break; }

        for (int i = 0; i < canAdd; i++)
        {
            var go = Instantiate(prefab, Vector3.zero, Quaternion.identity, transform);
            go.SetActive(false);

            if (go.TryGetComponent(out Obstacle obs))
            {
                obs.manager = this;
                obs.audioPool = obstacleAudioPool;
                if (typeConfig != null) obs.damageToInflict = typeConfig.damage;
            }

            subPool.Add(go);
        }

        DLog($"ObstacleManager: grew pool '{prefab.name}' by +{canAdd}. Now={subPool.Count}/{maxPoolPerPrefab}");
        return true;
    }

    readonly List<Vector3> _tmp = new();
    public List<Vector3> GetActiveWorldPositions()
    {
        PruneLists();

        _tmp.Clear();
        for (int i = 0; i < _active.Count; i++)
            if (_active[i] && _active[i].activeInHierarchy)
                _tmp.Add(_active[i].transform.position);
        return _tmp;
    }

    GameObject NextPooled(GameObject prefab)
    {
        if (!prefab) return null;

        if (_pool.TryGetValue(prefab, out var subPool))
        {
            for (int i = 0; i < subPool.Count; i++)
                if (subPool[i] && !subPool[i].activeInHierarchy) return subPool[i];
        }

        if (TryGrowPool(prefab, poolGrowBatch) && _pool.TryGetValue(prefab, out var grown))
        {
            for (int i = 0; i < grown.Count; i++)
                if (grown[i] && !grown[i].activeInHierarchy) return grown[i];
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
        total = Mathf.Max(1f, total);

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

    void TrySpawn(float diff)
    {
        if (!EnsureSplineRef()) return;
        if (laneVis.laneOffsets == null || laneVis.laneOffsets.Length == 0) return;

        PruneLists();

        ObstacleType typeToSpawn = GetRandomObstacleType(diff);
        var go = NextPooled(typeToSpawn.prefab);
        if (!go) return;

        Spline spline = _spline.Spline;
        float len = spline.GetLength();
        if (len < 0.1f) return;

        int failRay = 0, failPlayer = 0, failSep = 0, failColl = 0, failNoRoom = 0;

        for (int attempt = 0; attempt < maxPlacementAttempts; ++attempt)
        {
            float t = PickSpawnT(len);
            if (t < 0f) { failNoRoom++; break; } // open track: no fair room ahead

            SplineUtility.Evaluate(spline, t, out float3 lp, out float3 lt, out _);

            Vector3 wp = _spline.transform.TransformPoint(lp);
            Vector3 wt = _spline.transform.TransformDirection(math.normalizesafe(lt, new float3(0, 0, 1)));
            if (wt.sqrMagnitude < 0.0001f) wt = Vector3.forward;
            wt.Normalize();

            Vector3 right = Vector3.Cross(Vector3.up, wt).normalized;
            if (right.sqrMagnitude < 0.0001f) right = Vector3.right;

            int laneIdx = UnityEngine.Random.Range(0, laneVis.laneOffsets.Length);
            float offset = laneVis.laneOffsets[laneIdx];

            Vector3 rayStart = wp + right * offset + Vector3.up * raycastHeight;

            Vector3 finalPos;
            if (Physics.Raycast(rayStart, Vector3.down, out var hit, raycastHeight * 2f, groundMask))
                finalPos = hit.point + Vector3.up * 0.5f;
            else
            {
                failRay++;
                if (!allowSpawnWithoutGroundHit)
                    continue;

                finalPos = (wp + right * offset) + Vector3.up * 0.5f;
            }

            if (!IsClearOfPlayer(t)) { failPlayer++; continue; }
            if (!IsFarEnough(finalPos)) { failSep++; continue; }
            if (!IsFarFromCollectibles(finalPos)) { failColl++; continue; }

            go.transform.SetPositionAndRotation(finalPos, Quaternion.LookRotation(wt, Vector3.up));
            go.SetActive(true);
            _active.Add(go);
            _activeMeta.Add(new ObstacleMeta { go = go, t = t, lane = laneIdx, spawnTime = Time.time });

            if (SkillEstimator.Instance) SkillEstimator.Instance.OnObstacleSpawned();
            if (drawAttemptGizmos) DebugDraw(finalPos, Color.magenta);
            DLog($"Obstacle SPAWN OK: t={t:F3}, lane={laneIdx}, pos={finalPos}");
            return;
        }

        DLog($"Obstacle SPAWN FAIL: attempts={maxPlacementAttempts}, failNoRoom={failNoRoom}, failRay={failRay}, failPlayer={failPlayer}, failSep={failSep}, failColl={failColl}, active={_active.Count}/{_currentMaxActive}");
    }

    public void ReturnObstacleToPool(GameObject g)
    {
        if (!g) return;

        g.SetActive(false);
        _active.Remove(g);

        for (int i = _activeMeta.Count - 1; i >= 0; --i)
            if (_activeMeta[i].go == g) { _activeMeta.RemoveAt(i); break; }

        if (g.TryGetComponent(out Obstacle obs)) obs.ResetState();
    }

    void DebugDraw(Vector3 p, Color c)
    {
#if UNITY_EDITOR
        Debug.DrawLine(p, p + Vector3.up * 3f, c, 0.9f);
#endif
    }
}