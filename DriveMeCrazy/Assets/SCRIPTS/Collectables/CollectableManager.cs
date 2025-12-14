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
        public float t;         // 0..1 position on spline (lane-relative if separate lanes)
        public int lane;        // lane index (0=RIGHT, 1=CENTER, 2=LEFT when using separate lanes)
        public float spawnTime;
    }

    public IReadOnlyList<CollectableMeta> ActiveMetas => _activeMeta;
    readonly List<CollectableMeta> _activeMeta = new();

    [Header("Fairness / expiry")]
    public float autoMissAfterSeconds = 12f;

    [Header("Prefabs & Limits")]
    [SerializeField] GameObject collectablePrefab;
    [Min(1)][SerializeField] int maxActive = 10;
    [Min(.1f)][SerializeField] float spawnInterval = 5f;

    [Header("Lane Tracks (separate splines)")]
    [Tooltip("If true, spawns use laneContainers directly. Order MUST be: 0=RIGHT, 1=CENTER, 2=LEFT.")]
    public bool useSeparateLaneSplines = true;

    [Tooltip("Lane spline containers. 0=RIGHT, 1=CENTER, 2=LEFT.")]
    public SplineContainer[] laneContainers = new SplineContainer[3];

    [Header("Lane & Distance Settings (legacy offset mode)")]
    [SerializeField] public LaneLinesVisualizer laneVis;
    [Min(.1f)] public float minSeparation = 4f;
    [Range(1, 50)] public int maxPlacementAttempts = 15;

    [Header("Lane Offset Source (legacy offset mode)")]
    [Tooltip("True = laneVis.laneOffsets. False = fixed 3 lanes: -d, 0, +d (d from player.laneOffsetDistance if available).")]
    public bool useLaneVisualizerOffsets = true;

    [Tooltip("Only used if useLaneVisualizerOffsets is false and player is not assigned.")]
    public float fallbackLaneOffsetDistance = 2f;

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

    public AnimationCurve maxActiveByDiff = AnimationCurve.Linear(0, 16, 1, 8);
    public AnimationCurve intervalByDiff = AnimationCurve.Linear(0, 1.2f, 1, 3.5f);
    public AnimationCurve separationByDiff = AnimationCurve.Linear(0, 6f, 1, 3.5f);

    float _curInterval, _curSeparation, _curMaxActive;

    [Header("Fairness")]
    public float playerSafeAheadMeters = 14f;
    public float playerSafeBehindMeters = 5f;
    public float minCrossSeparationMeters = 3.0f;

    public ArcadeVP.ArcadeVehicleController player;
    public ObstacleManager obstacleMgr;

    [Header("Fairness • track-relative expiry")]
    public float despawnBehindMeters = 10f;
    public float protectAheadMeters = 45f;
    public float hardMaxLifetimeSeconds = 30f;

    bool HasSeparateLaneSplines()
    {
        if (!useSeparateLaneSplines) return false;
        if (laneContainers == null || laneContainers.Length < 3) return false;
        return laneContainers[0] != null && laneContainers[1] != null && laneContainers[2] != null;
    }

    SplineContainer GetLaneContainer(int laneIdx)
    {
        if (HasSeparateLaneSplines())
        {
            int i = Mathf.Clamp(laneIdx, 0, 2);
            return laneContainers[i];
        }
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
        // Prefer player's active spline length (most accurate for TrackTNormalized)
        if (player && player.splineContainer != null)
        {
            float pl = player.splineContainer.Spline.GetLength();
            if (pl > 0.1f) return pl;
        }

        // Prefer center lane length if separate lanes are used
        if (HasSeparateLaneSplines() && laneContainers[1] != null)
        {
            float cl = laneContainers[1].Spline.GetLength();
            if (cl > 0.1f) return cl;
        }

        // Fallback to local spline length
        if (_spline) return _spline.Spline.GetLength();
        return 0f;
    }

    float AheadMeters(float aT, float bT)
    {
        float len = ArcLen();
        if (len <= 0.001f) return 0f;
        float dT = bT - aT;
        if (dT < -0.5f) dT += 1f;
        else if (dT > 0.5f) dT -= 1f;
        return dT * len;
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

    public int SpawnLaneCount
    {
        get
        {
            if (HasSeparateLaneSplines()) return 3;
            if (UsingLaneVis()) return laneVis.laneOffsets.Length;
            return 3;
        }
    }

    public float GetLaneOffsetByIndex(int laneIdx)
    {
        // LEGACY offset mode only (ignored in separate lane spline mode)
        if (UsingLaneVis())
        {
            int i = Mathf.Clamp(laneIdx, 0, laneVis.laneOffsets.Length - 1);
            return laneVis.laneOffsets[i];
        }

        float d = GetLaneStep();
        int i3 = Mathf.Clamp(laneIdx, 0, 2);
        return (i3 - 1) * d; // 0=-d, 1=0, 2=+d
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

    bool IsFarFromObstacles(Vector3 p)
    {
        if (!obstacleMgr) return true;
        float minSq = minCrossSeparationMeters * minCrossSeparationMeters;
        var list = obstacleMgr.GetActiveWorldPositions();
        for (int i = 0; i < list.Count; i++)
            if ((list[i] - p).sqrMagnitude < minSq) return false;
        return true;
    }

    void Awake()
    {
        if (!collectablePrefab) { Debug.LogError("CollectableManager: Prefab missing"); enabled = false; return; }

        _spline = GetComponent<SplineContainer>();

        if (!laneVis) laneVis = GetComponent<LaneLinesVisualizer>();

        if (useSeparateLaneSplines && !HasSeparateLaneSplines())
        {
            Debug.LogError("CollectableManager: useSeparateLaneSplines ON, but laneContainers[0..2] are not assigned (0=RIGHT, 1=CENTER, 2=LEFT).");
            enabled = false;
            return;
        }

        if (!HasSeparateLaneSplines() && useLaneVisualizerOffsets && !UsingLaneVis())
        {
            Debug.LogWarning("CollectableManager: useLaneVisualizerOffsets true, but laneVis or laneOffsets missing. Switching to fixed 3-lane fallback.");
            useLaneVisualizerOffsets = false;
        }

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

        if (_pool.Count < maxActive)
        {
            var go = Instantiate(collectablePrefab, Vector3.zero, Quaternion.identity, transform);
            go.SetActive(false);
            if (go.TryGetComponent(out Collectable col))
            {
                col.manager = this;
                col.audioPool = collectableAudioPool;
            }
            _pool.Add(go);
            return go;
        }

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

                if (!isAhead && Mathf.Abs(aheadMeters) >= despawnBehindMeters)
                {
                    meta.go.SetActive(false);
                    _active.Remove(meta.go);
                    _activeMeta.RemoveAt(i);
                    if (SkillEstimator.Instance) SkillEstimator.Instance.OnCollectibleMissed();
                    continue;
                }

                if (hardMaxLifetimeSeconds > 0f && age > hardMaxLifetimeSeconds && !withinProtectAhead)
                {
                    meta.go.SetActive(false);
                    _active.Remove(meta.go);
                    _activeMeta.RemoveAt(i);
                    continue;
                }
            }

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

        int laneCount = SpawnLaneCount;
        if (laneCount <= 0) return;

        for (int attempt = 0; attempt < maxPlacementAttempts; ++attempt)
        {
            float t = UnityEngine.Random.value;
            int laneIdx = UnityEngine.Random.Range(0, laneCount);

            Vector3 wp, wt, wu, right;
            if (!TryGetLaneFrame(laneIdx, t, out wp, out wt, out wu, out right))
                continue;

            Vector3 candidateRayStart;
            Vector3 candidateFallback;

            if (HasSeparateLaneSplines())
            {
                candidateRayStart = wp + Vector3.up * rayHeight;
                candidateFallback = wp + Vector3.up * floatHeight;
            }
            else
            {
                float offset = GetLaneOffsetByIndex(laneIdx);
                candidateRayStart = wp + right * offset + Vector3.up * rayHeight;
                candidateFallback = wp + right * offset + Vector3.up * floatHeight;
            }

            Vector3 finalPos;
            if (Physics.Raycast(candidateRayStart, Vector3.down, out var hit, rayHeight * 2f, groundMask, QueryTriggerInteraction.Ignore))
                finalPos = hit.point + Vector3.up * floatHeight;
            else
                finalPos = candidateFallback;

            if (!IsClearOfPlayer(t)) continue;
            if (!IsFarEnough(finalPos)) { if (drawAttemptGizmos) DebugDraw(finalPos, Color.red); continue; }
            if (!IsFarFromObstacles(finalPos)) { if (drawAttemptGizmos) DebugDraw(finalPos, Color.red); continue; }

            if (wt.sqrMagnitude < 0.0001f) wt = transform.forward;
            if (wu.sqrMagnitude < 0.0001f) wu = Vector3.up;

            go.transform.SetPositionAndRotation(finalPos, Quaternion.LookRotation(wt, wu));
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
            if (drawAttemptGizmos) DebugDraw(finalPos, Color.green);
            return;
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

        for (int i = _activeMeta.Count - 1; i >= 0; --i)
            if (_activeMeta[i].go == g) { _activeMeta.RemoveAt(i); break; }

        if (SkillEstimator.Instance) SkillEstimator.Instance.OnCollectibleCollected();
    }
}
