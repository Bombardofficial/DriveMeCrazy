using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

/// <summary>
///   Spawns collectables perfectly centred on each lane?centre line without ever stacking.
///   • Spawn points are baked once and cached.  
///   • Uses an object pool for zero?GC spawns.  
///   • Fixes CS1612 by copying the <c>SpawnPoint</c> struct, mutating, then writing it back.  
/// </summary>
[RequireComponent(typeof(SplineContainer))]
public class CollectableManager : MonoBehaviour
{
    #region Inspector ? Spawning
    [Header("Spawning Settings")]
    [Tooltip("The prefab for the collectable item.")]
    [SerializeField] private GameObject collectablePrefab;

    [Tooltip("The maximum number of collectables allowed on the map at one time.")]
    [SerializeField] private int maxActiveCollectables = 10;

    [Tooltip("Seconds between spawn attempts while we are below the active limit.")]
    [SerializeField] private float spawnInterval = 10f;
    #endregion

    #region Inspector ? Spawn?grid baking
    [Header("Spawn?grid Settings")]
    [Tooltip("How many metres between two candidate spawn points along each lane.")]
    [Min(1f)] public float sampleSpacing = 5f;

    [Tooltip("How high above the candidate point the ground?finding ray starts.")]
    public float raycastHeight = 5f;

    [Tooltip("Collectables float this much above the ground hit.")]
    public float heightAboveGround = 0.5f;

    [Tooltip("Layer(s) considered valid road.")]
    public LayerMask groundLayer;

    [Tooltip("Draw green (free) / red (occupied) spheres in the Scene view.")]
    public bool drawSpawnGizmos = true;
    #endregion

    /* ?????????????????????????? internal ?????????????????????????? */
    struct SpawnPoint
    {
        public Vector3 pos;      // baked world?space final position
        public Vector3 fwd;      // baked forward for nice rotation
        public bool occupied; // runtime flag
    }

    readonly List<SpawnPoint> _spawnGrid = new();
    readonly Dictionary<GameObject, int> _instanceToIdx = new();

    SplineContainer _splineContainer;
    LaneLinesVisualizer _laneVis;

    readonly List<GameObject> _pool = new();
    int _activeCount;

    /* ???????????? Unity lifecycle ???????????? */
    void Awake()
    {
        if (!collectablePrefab)
        {
            Debug.LogError("CollectableManager: missing prefab reference – disabling");
            enabled = false; return;
        }

        _splineContainer = GetComponent<SplineContainer>();
        _laneVis = GetComponent<LaneLinesVisualizer>();
        if (!_laneVis)
        {
            Debug.LogError("CollectableManager: requires LaneLinesVisualizer on the same GameObject.");
            enabled = false; return;
        }

        BuildSpawnPoints();
        BuildPool();
        StartCoroutine(Spawner());
    }

    /* ???????????? Spawn?grid baking ???????????? */
    void BuildSpawnPoints()
    {
        _spawnGrid.Clear();

        Spline spline = _splineContainer.Spline;
        float splineLength = spline.GetLength();
        if (splineLength < math.EPSILON)
        {
            Debug.LogWarning("CollectableManager: spline length ? 0 – no spawn points generated.");
            return;
        }

        int sampleCount = Mathf.CeilToInt(splineLength / Mathf.Max(1f, sampleSpacing));

        for (int s = 0; s <= sampleCount; ++s)
        {
            float ratio = s / (float)sampleCount; // param 0?1
            SplineUtility.Evaluate(spline, ratio, out float3 lp, out float3 lt, out _);

            Vector3 worldP = transform.TransformPoint(lp);
            Vector3 worldF = transform.TransformDirection(math.normalize(lt));
            Vector3 right = Vector3.Cross(Vector3.up, worldF).normalized;

            foreach (float laneOffset in _laneVis.laneOffsets)
            {
                Vector3 candidate = worldP + right * laneOffset + Vector3.up * raycastHeight;

                if (Physics.Raycast(candidate, Vector3.down, out var hit, raycastHeight * 2f, groundLayer))
                    candidate = hit.point + Vector3.up * heightAboveGround;
                else
                    candidate = (worldP + right * laneOffset) + Vector3.up * heightAboveGround;

                _spawnGrid.Add(new SpawnPoint { pos = candidate, fwd = worldF, occupied = false });
            }
        }

        if (_spawnGrid.Count == 0)
            Debug.LogWarning("CollectableManager: no spawn points survived the baking step – check groundLayer.");
    }

    /* ???????????? Object pool ???????????? */
    void BuildPool()
    {
        for (int i = 0; i < maxActiveCollectables; ++i)
        {
            var go = Instantiate(collectablePrefab, Vector3.zero, Quaternion.identity, transform);
            go.SetActive(false);
            var col = go.GetComponent<Collectable>();
            if (col) col.manager = this;
            _pool.Add(go);
        }
    }

    GameObject RequestPooled()
    {
        foreach (var obj in _pool)
            if (!obj.activeInHierarchy) return obj;
        return null; // pool exhausted
    }

    /* ???????????? Runtime spawning ???????????? */
    IEnumerator Spawner()
    {
        var wait = new WaitForSeconds(spawnInterval);
        while (true)
        {
            yield return wait;
            if (_activeCount < maxActiveCollectables)
                TrySpawn();
        }
    }

    void TrySpawn()
    {
        if (_spawnGrid.Count == 0) return;

        GameObject instance = RequestPooled();
        if (!instance) return;

        // pick a random free point – retry a couple of times in case the RNG hits occupied ones
        const int kMaxTries = 15;
        int idx = -1;
        for (int i = 0; i < kMaxTries; ++i)
        {
            int probe = UnityEngine.Random.Range(0, _spawnGrid.Count);
            if (!_spawnGrid[probe].occupied) { idx = probe; break; }
        }
        if (idx < 0) return; // grid fully occupied

        // place & activate --------------------------------------------------
        SpawnPoint pt = _spawnGrid[idx]; // copy struct
        instance.transform.SetPositionAndRotation(pt.pos, Quaternion.LookRotation(pt.fwd, Vector3.up));
        instance.SetActive(true);

        pt.occupied = true;           // mutate copy
        _spawnGrid[idx] = pt;         // write back (struct fix)

        _instanceToIdx[instance] = idx;
        ++_activeCount;
    }

    /* ???????????? API for Collectable.cs ???????????? */
    public void ReturnCollectableToPool(GameObject instance)
    {
        if (instance && _instanceToIdx.TryGetValue(instance, out int idx))
        {
            SpawnPoint pt = _spawnGrid[idx];
            pt.occupied = false;      // free spot
            _spawnGrid[idx] = pt;     // write back

            _instanceToIdx.Remove(instance);
            --_activeCount;
        }

        if (instance) instance.SetActive(false);
    }

    /* ???????????? Debug gizmos ???????????? */
#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!drawSpawnGizmos || _spawnGrid == null) return;

        foreach (var pt in _spawnGrid)
        {
            Gizmos.color = pt.occupied ? Color.red : Color.green;
            Gizmos.DrawSphere(pt.pos, 0.2f);
        }
    }
#endif
}
