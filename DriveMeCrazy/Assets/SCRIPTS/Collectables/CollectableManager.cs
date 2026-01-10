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

    [Header("Debug")]
    public bool debugLogs = false;
    [Tooltip("Seconds between debug log bursts (prevents console spam).")]
    public float debugLogCooldown = 1.0f;
    float _nextLogTime = 0f;

    void DLog(string msg)
    {
        if (!debugLogs) return;
        if (Time.time < _nextLogTime) return;
        _nextLogTime = Time.time + Mathf.Max(0.1f, debugLogCooldown);
        Debug.Log(msg, this);
    }

    [Header("Fairness / expiry")]
    public float autoMissAfterSeconds = 12f; // optional fallback (not primary)

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

    [Header("Fairness (spawn window relative to player)")]
    [Tooltip("Meters ahead of the car where collectibles will not spawn.")]
    public float playerSafeAheadMeters = 14f;

    [Tooltip("Meters behind the car where collectibles will not spawn.")]
    public float playerSafeBehindMeters = 5f;

    [Tooltip("Minimum meters between a collectible and any obstacle.")]
    public float minCrossSeparationMeters = 3.0f;

    [Tooltip("Spawn window start ahead of player (meters). MUST be > playerSafeAheadMeters.")]
    public float spawnAheadMinMeters = 18f;

    [Tooltip("Spawn window end ahead of player (meters). Keep it reasonable (e.g. 35-80).")]
    public float spawnAheadMaxMeters = 45f;

    public ArcadeVP.ArcadeVehicleController player;  // assign in inspector
    public ObstacleManager obstacleMgr;               // assign in inspector

    [Header("Fairness • track-relative expiry")]
    [Tooltip("Meters behind the player at which a collectible is considered missed and despawned.")]
    public float despawnBehindMeters = 10f;

    [Tooltip("While the item is within this many meters AHEAD of the player, never expire it due to age.")]
    public float protectAheadMeters = 45f;

    [Tooltip("Hard cleanup: if older than this AND far away (ahead or behind), despawn without counting a miss.")]
    public float hardMaxLifetimeSeconds = 30f;

    [SerializeField] private SplineContainer trackSpline;

    // ---------- PATCH #1: prune meta + active so counts never desync ----------
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
            var go = _active[i];
            if (!go || !go.activeInHierarchy)
                _active.RemoveAt(i);
        }
    }

    float ArcLen()
    {
        if (!_spline || _spline.Spline == null) return 0f;
        return _spline.Spline.GetLength();
    }

    bool EnsureSplineRef()
    {
        // Prefer explicit trackSpline if set
        if (!_spline)
        {
            _spline = trackSpline ? trackSpline : GetComponent<SplineContainer>();
        }

        // If still bad, try laneVis container (often that's the real track)
        if ((!_spline || _spline.Spline == null || _spline.Spline.GetLength() < 0.1f) && laneVis)
        {
            var sc = laneVis.GetComponent<SplineContainer>();
            if (sc) _spline = sc;
        }

        // Last chance: self container
        if (!_spline) _spline = GetComponent<SplineContainer>();

        return (_spline && _spline.Spline != null && _spline.Spline.GetLength() >= 0.1f);
    }

    void Awake()
    {
        if (!collectablePrefab)
        {
            Debug.LogError("CollectableManager: Prefab missing", this);
            enabled = false;
            return;
        }

        if (!laneVis) laneVis = GetComponent<LaneLinesVisualizer>();
        if (!laneVis)
        {
            Debug.LogError("CollectableManager: LaneLinesVisualizer ref missing", this);
            enabled = false;
            return;
        }

        // pick spline ref early (but SpawnLoop will also wait until ready)
        _spline = trackSpline ? trackSpline : GetComponent<SplineContainer>();

        if (groundMask.value == 0)
            groundMask = laneVis.drivableSurface;

        BuildInitialPool();
        StartCoroutine(SpawnLoop());
    }

    void BuildInitialPool()
    {
        int desired = Mathf.Max(1, maxActive);

        // If director can raise maxActive above inspector value, prebuild enough
        if (useDirector && maxActiveByDiff != null && maxActiveByDiff.length > 0)
        {
            float peak = 0f;
            var keys = maxActiveByDiff.keys;
            for (int i = 0; i < keys.Length; i++)
                peak = Mathf.Max(peak, keys[i].value);
            desired = Mathf.Max(desired, Mathf.CeilToInt(peak));
        }

        EnsurePoolSize(desired);
    }

    void EnsurePoolSize(int desired)
    {
        desired = Mathf.Max(1, desired);
        while (_pool.Count < desired)
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

    GameObject NextPooledOrGrow()
    {
        for (int i = 0; i < _pool.Count; i++)
            if (_pool[i] && !_pool[i].activeInHierarchy) return _pool[i];

        // grow by a little if director raised cap
        EnsurePoolSize(_pool.Count + 4);

        for (int i = 0; i < _pool.Count; i++)
            if (_pool[i] && !_pool[i].activeInHierarchy) return _pool[i];

        return null;
    }

    void ApplyDifficulty(float diff, float dt)
    {
        if (!useDirector) return;

        float targetMax = Mathf.Clamp(maxActiveByDiff.Evaluate(diff), 1f, 999f);
        float targetInt = Mathf.Max(0.15f, intervalByDiff.Evaluate(diff));
        float targetSep = Mathf.Max(0.5f, separationByDiff.Evaluate(diff));

        float k = 1f - Mathf.Exp(-5f * dt);

        _curMaxActive = Mathf.Lerp(_curMaxActive <= 0 ? maxActive : _curMaxActive, targetMax, k);
        _curInterval = Mathf.Lerp(_curInterval <= 0 ? spawnInterval : _curInterval, targetInt, k);
        _curSeparation = Mathf.Lerp(_curSeparation <= 0 ? minSeparation : _curSeparation, targetSep, k);

        maxActive = Mathf.RoundToInt(_curMaxActive);
        spawnInterval = _curInterval;
        minSeparation = _curSeparation;
    }

    bool IsFarFromObstacles(Vector3 p)
    {
        if (!obstacleMgr) return true;

        float minSq = minCrossSeparationMeters * minCrossSeparationMeters;
        var list = obstacleMgr.GetActiveWorldPositions();
        for (int i = 0; i < list.Count; i++)
            if ((list[i] - p).sqrMagnitude < minSq) return false;

        return true;
    }

    bool IsFarEnough(Vector3 p)
    {
        float minSq = minSeparation * minSeparation;
        for (int i = 0; i < _active.Count; i++)
        {
            var g = _active[i];
            if (!g) continue;
            if ((g.transform.position - p).sqrMagnitude < minSq)
                return false;
        }
        return true;
    }

    float PickSpawnT(float len)
    {
        // If no player, fallback
        if (!player || len < 0.1f) return UnityEngine.Random.value;

        float tCar = Mathf.Repeat(player.TrackTNormalized, 1f);

        float minAhead = Mathf.Max(playerSafeAheadMeters + 0.5f, spawnAheadMinMeters);
        float maxAhead = Mathf.Max(minAhead + 1.0f, spawnAheadMaxMeters);

        // convert meters ? dt (normalized), clamp to avoid ambiguous >0.5 lap cases
        float dtMin = minAhead / len;
        float dtMax = maxAhead / len;

        // hard clamp to keep “always ahead” within half lap
        dtMin = Mathf.Clamp(dtMin, 0.01f, 0.49f);
        dtMax = Mathf.Clamp(dtMax, 0.02f, 0.49f);

        if (dtMax <= dtMin + 0.0005f)
        {
            // track too short for your safe window ? nothing will ever be “fair”
            DLog($"CollectableManager: Track len={len:F2} too short for spawn window [{minAhead:F1}..{maxAhead:F1}]m. " +
                 $"Reduce spawnAheadMax/Min or safe distances.");
            return UnityEngine.Random.value;
        }

        float dt = UnityEngine.Random.Range(dtMin, dtMax);
        return Mathf.Repeat(tCar + dt, 1f);
    }

    IEnumerator SpawnLoop()
    {
        // Wait for race start
        while (!PlayerJoinManager.IsRaceStarted)
        {
            DLog("CollectableManager: waiting for PlayerJoinManager.IsRaceStarted...");
            yield return null;
        }

        // Wait until spline is ??????? usable
        while (!EnsureSplineRef())
        {
            DLog("CollectableManager: spline not ready / length=0. Waiting...");
            yield return null;
        }

        DLog($"CollectableManager: START. Spline='{_spline.name}' len={ArcLen():F2}, lanes={laneVis.laneOffsets?.Length ?? 0}, groundMask={groundMask.value}");

        while (enabled && PlayerJoinManager.IsRaceStarted)
        {
            // PATCH #1: keep lists consistent every tick
            PruneLists();

            float diff = 0.5f;
            if (DifficultyDirector.Instance)
            {
                float rewardBias = Mathf.Clamp01(DifficultyDirector.Instance.Current.rewardBias);
                diff = 1f - rewardBias;
            }

            ApplyDifficulty(diff, Time.deltaTime);
            EnsurePoolSize(maxActive);

            // After ensure (pool can grow), prune again (belt)
            PruneLists();

            if (_active.Count < maxActive)
                TrySpawn();

            // Expiry: robust forward/behind check (works because we spawn in-front window)
            float len = ArcLen();
            float playerT = player ? Mathf.Repeat(player.TrackTNormalized, 1f) : 0f;

            for (int i = _activeMeta.Count - 1; i >= 0; --i)
            {
                var meta = _activeMeta[i];

                // PATCH #1: if meta dead/inactive, also remove from _active
                if (!meta.go || !meta.go.activeInHierarchy)
                {
                    if (meta.go) _active.Remove(meta.go);
                    _activeMeta.RemoveAt(i);
                    continue;
                }

                float age = Time.time - meta.spawnTime;

                float dtForward = Mathf.Repeat(meta.t - playerT, 1f); // 0..1 forward distance from player to item
                bool isBehind = (dtForward > 0.5f);

                float aheadMeters = isBehind ? 999999f : dtForward * len;
                float behindMeters = isBehind ? (1f - dtForward) * len : 999999f;

                bool withinProtectAhead = (!isBehind && aheadMeters <= protectAheadMeters);

                // (1) Miss: only when clearly behind by threshold
                if (isBehind && behindMeters >= despawnBehindMeters)
                {
                    meta.go.SetActive(false);
                    _active.Remove(meta.go);
                    _activeMeta.RemoveAt(i);

                    if (SkillEstimator.Instance) SkillEstimator.Instance.OnCollectibleMissed();
                    continue;
                }

                // Optional fallback age-miss if you want it (kept minimal)
                if (autoMissAfterSeconds > 0f && age > autoMissAfterSeconds && isBehind && behindMeters >= 1f)
                {
                    meta.go.SetActive(false);
                    _active.Remove(meta.go);
                    _activeMeta.RemoveAt(i);

                    if (SkillEstimator.Instance) SkillEstimator.Instance.OnCollectibleMissed();
                    continue;
                }

                // (2) Hard cleanup: very old AND far (don’t count as miss)
                if (hardMaxLifetimeSeconds > 0f && age > hardMaxLifetimeSeconds && !withinProtectAhead)
                {
                    bool farAhead = (!isBehind && aheadMeters > protectAheadMeters);
                    bool farBehind = (isBehind && behindMeters > despawnBehindMeters);

                    if (farAhead || farBehind)
                    {
                        meta.go.SetActive(false);
                        _active.Remove(meta.go);
                        _activeMeta.RemoveAt(i);
                        continue;
                    }
                }
            }

            float wait = Mathf.Max(0.05f, spawnInterval);
            float t = 0f;
            while (t < wait)
            {
                t += Time.deltaTime;
                yield return null;
            }
        }
    }

    void TrySpawn()
    {
        if (!EnsureSplineRef()) return;

        // PATCH #1: keep counts truthful at spawn time too
        PruneLists();

        if (laneVis.laneOffsets == null || laneVis.laneOffsets.Length == 0)
        {
            Debug.LogError("CollectableManager: laneOffsets empty/null -> cannot spawn", this);
            return;
        }

        var go = NextPooledOrGrow();
        if (!go) { DLog("CollectableManager: pool exhausted (even after grow)."); return; }

        Spline spline = _spline.Spline;
        float len = spline.GetLength();
        if (len < 0.1f) { DLog("CollectableManager: spline length < 0.1"); return; }

        int failPlayer = 0, failSep = 0, failObs = 0;

        for (int attempt = 0; attempt < maxPlacementAttempts; ++attempt)
        {
            float t = PickSpawnT(len);

            SplineUtility.Evaluate(spline, t, out float3 lp, out float3 lt, out _);

            Vector3 wp = _spline.transform.TransformPoint(lp);
            Vector3 wt = _spline.transform.TransformDirection(math.normalizesafe(lt, new float3(0, 0, 1)));
            if (wt.sqrMagnitude < 0.0001f) wt = Vector3.forward;
            wt.Normalize();

            Vector3 right = Vector3.Cross(Vector3.up, wt).normalized;
            if (right.sqrMagnitude < 0.0001f) right = Vector3.right;

            int laneIdx = UnityEngine.Random.Range(0, laneVis.laneOffsets.Length);
            float offset = laneVis.laneOffsets[laneIdx];

            Vector3 candidateRay = wp + right * offset + Vector3.up * rayHeight;

            // Ground project (ray optional; if miss, still spawn at floatHeight)
            Vector3 candidate;
            if (Physics.Raycast(candidateRay, Vector3.down, out var hit, rayHeight * 2f, groundMask))
                candidate = hit.point + Vector3.up * floatHeight;
            else
                candidate = wp + right * offset + Vector3.up * floatHeight;

            // Fairness checks
            if (player)
            {
                float tCar = Mathf.Repeat(player.TrackTNormalized, 1f);
                float dtForward = Mathf.Repeat(t - tCar, 1f);
                float ahead = dtForward * len;
                float behind = (1f - dtForward) * len;

                if (ahead < playerSafeAheadMeters || behind < playerSafeBehindMeters)
                {
                    failPlayer++;
                    if (drawAttemptGizmos) DebugDraw(candidate, Color.red);
                    continue;
                }
            }

            if (!IsFarEnough(candidate))
            {
                failSep++;
                if (drawAttemptGizmos) DebugDraw(candidate, Color.red);
                continue;
            }

            if (!IsFarFromObstacles(candidate))
            {
                failObs++;
                if (drawAttemptGizmos) DebugDraw(candidate, Color.red);
                continue;
            }

            // Spawn OK
            go.transform.SetPositionAndRotation(candidate, Quaternion.LookRotation(wt, Vector3.up));
            go.SetActive(true);

            _active.Add(go);
            _activeMeta.Add(new CollectableMeta
            {
                go = go,
                t = t,
                lane = laneIdx,
                spawnTime = Time.time
            });

            if (SkillEstimator.Instance) SkillEstimator.Instance.OnCollectibleSpawned();
            if (drawAttemptGizmos) DebugDraw(candidate, Color.green);

            DLog($"Collectable SPAWN OK: t={t:F3}, lane={laneIdx}, pos={candidate}");
            return;
        }

        DLog($"Collectable SPAWN FAIL: attempts={maxPlacementAttempts}, failPlayer={failPlayer}, failSep={failSep}, failObs={failObs}, active={_active.Count}/{maxActive}");
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

        for (int i = _activeMeta.Count - 1; i >= 0; --i)
            if (_activeMeta[i].go == g) { _activeMeta.RemoveAt(i); break; }

        if (SkillEstimator.Instance) SkillEstimator.Instance.OnCollectibleCollected();
    }
}
