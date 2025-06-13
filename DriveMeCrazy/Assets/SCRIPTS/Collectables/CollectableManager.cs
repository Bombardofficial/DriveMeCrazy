using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

/// <summary>
/// A "pick?up rain" spawner that ensures collectibles are randomly placed along the spline.
/// <list type="bullet">
///    <item>Works with any spline length / lane layout.</item>
///    <item>No pre?allocation, no extra GameObjects, zero GC at runtime (object pool).</item>
///    <item>Insanely simple to reason about and debug – toggle the “drawAttemptGizmos” flag and you
///          can watch every attempted placement in real time.</item>
/// </list>
/// </summary>
[RequireComponent(typeof(SplineContainer))]
public class CollectableManager : MonoBehaviour
{
    /* ????????????????????????????? Inspector ????????????????????????????? */
    [Header("Prefabs & Limits")]
    [SerializeField] GameObject collectablePrefab;
    [Min(1)][SerializeField] int maxActive = 10;
    [Min(.1f)][SerializeField] float spawnInterval = 5f;

    [Header("Lane & Distance Settings")]
    [Tooltip("Reference to the lane?visualiser so we know the lane offsets.")]
    [SerializeField] LaneLinesVisualizer laneVis;
    [Tooltip("Minimum world?space distance allowed between two collectables.")]
    [Min(.1f)] public float minSeparation = 4f;
    [Tooltip("How many random points we’ll try per spawn tick before giving up.")]
    [Range(1, 50)] public int maxPlacementAttempts = 15;

    [Header("Vertical placement")]
    public float rayHeight = 5f;
    public float floatHeight = 0.5f;
    public LayerMask groundMask;

    [Header("Debug ? Scene view")]
    public bool drawAttemptGizmos = false;

    /* ????????????????????????????? Private ????????????????????????????? */
    readonly List<GameObject> _pool = new();
    readonly List<GameObject> _active = new();

    SplineContainer _spline;

    // ---- REMOVED ----
    // No longer needed, we will use UnityEngine.Random
    // System.Random _rng = new System.Random(); 
    // -----------------

    /* ???????????????? Unity lifecycle ???????????????? */
    void Awake()
    {
        if (!collectablePrefab) { Debug.LogError("CollectableManager: Prefab missing"); enabled = false; return; }
        _spline = GetComponent<SplineContainer>();
        if (!laneVis) laneVis = GetComponent<LaneLinesVisualizer>();
        if (!laneVis) { Debug.LogError("CollectableManager: LaneLinesVisualizer ref missing"); enabled = false; return; }

        BuildPool();
        StartCoroutine(SpawnLoop());
    }

    /* ???????????????? Pool ???????????????? */
    void BuildPool()
    {
        for (int i = 0; i < maxActive; ++i)
        {
            var go = Instantiate(collectablePrefab, Vector3.zero, Quaternion.identity, transform);
            go.SetActive(false);
            if (go.TryGetComponent(out Collectable col)) col.manager = this;
            _pool.Add(go);
        }
    }

    GameObject NextPooled()
    {
        foreach (var g in _pool)
            if (!g.activeInHierarchy) return g;
        return null;
    }

    /* ???????????????? Spawn loop ???????????????? */
    IEnumerator SpawnLoop()
    {
        var wait = new WaitForSeconds(spawnInterval);
        while (true)
        {
            yield return wait;
            if (_active.Count < maxActive) TrySpawn();
        }
    }

    void TrySpawn()
    {
        var go = NextPooled();
        if (!go) return; // shouldn’t happen

        Spline spline = _spline.Spline;
        float len = spline.GetLength();
        if (len < 0.1f) return;

        for (int attempt = 0; attempt < maxPlacementAttempts; ++attempt)
        {
            // ---- MODIFIED ----
            // pick random point along spline using Unity's Random class
            float t = UnityEngine.Random.value; // Random.value is a float between 0.0 and 1.0
                                    // ------------------

            SplineUtility.Evaluate(spline, t, out float3 lp, out float3 lt, out _);
            Vector3 wp = transform.TransformPoint(lp);
            Vector3 wt = transform.TransformDirection(math.normalize(lt));
            Vector3 right = Vector3.Cross(Vector3.up, wt).normalized;

            // ---- MODIFIED ----
            // pick random lane using Unity's Random class
            int laneIdx = UnityEngine.Random.Range(0, laneVis.laneOffsets.Length);
            // ------------------
            float offset = laneVis.laneOffsets[laneIdx];

            Vector3 candidate = wp + right * offset + Vector3.up * rayHeight;
            if (Physics.Raycast(candidate, Vector3.down, out var hit, rayHeight * 2f, groundMask))
                candidate = hit.point + Vector3.up * floatHeight;
            else
                candidate = wp + right * offset + Vector3.up * floatHeight;

            if (IsFarEnough(candidate))
            {
                // success !
                go.transform.SetPositionAndRotation(candidate, Quaternion.LookRotation(wt, Vector3.up));
                go.SetActive(true);
                _active.Add(go);

                if (drawAttemptGizmos)
                    DebugDraw(candidate, Color.green);
                return;
            }
            else if (drawAttemptGizmos)
                DebugDraw(candidate, Color.red);
        }
        // failed after N attempts – just wait for next frame.
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
        UnityEngine.Debug.DrawLine(p, p + Vector3.up * 3f, c, spawnInterval * 0.9f);
#endif
    }

    /* ???????????????? Called by Collectable.cs ???????????????? */
    public void ReturnCollectableToPool(GameObject g)
    {
        if (!g) return;
        g.SetActive(false);
        _active.Remove(g);
    }
}