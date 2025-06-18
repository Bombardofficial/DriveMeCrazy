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
    [Tooltip("How much damage this obstacle inflicts on the player.")]
    [Range(1, 100)] public int damage = 10;
    [Tooltip("A higher number means a higher chance of spawning. E.g., a weight of 75 is 3x more likely to spawn than a weight of 25.")]
    [Range(1, 100)] public int spawnWeight = 50;
}

/// <summary>
/// Spawns and manages a variety of physics-based obstacles on the spline track.
/// It uses a weighted random system to spawn different types of obstacles and
/// includes logic for progressively increasing difficulty over time.
/// </summary>
[RequireComponent(typeof(SplineContainer))]
public class ObstacleManager : MonoBehaviour
{
    /* ????????????????????????????? Inspector ????????????????????????????? */
    [Header("Obstacle Configuration")]
    [Tooltip("Define the different types of obstacles that can spawn.")]
    [SerializeField] private ObstacleType[] obstacleTypes;

    [Header("Prefabs & Limits")]
    [Tooltip("The total number of obstacles this spawner will ever create for the pool across all types.")]
    [Min(1)][SerializeField] int poolSize = 50;
    [Min(.1f)][SerializeField] float spawnInterval = 2f;

    [Header("Lane & Distance Settings")]
    [Tooltip("Reference to the lane-visualiser so we know the lane offsets.")]
    [SerializeField] private LaneLinesVisualizer laneVis;
    [Tooltip("Minimum world-space distance allowed between two obstacles when spawning.")]
    [Min(1f)] public float minSeparation = 10f;
    [Tooltip("How many random points we’ll try per spawn tick before giving up.")]
    [Range(1, 50)] public int maxPlacementAttempts = 15;

    [Header("Difficulty Scaling")]
    [Tooltip("The number of obstacles allowed on screen at the start.")]
    [SerializeField] private int initialMaxActive = 5;
    [Tooltip("The absolute maximum number of obstacles allowed on screen at peak difficulty.")]
    [SerializeField] private int maxActiveCap = 40;
    [Tooltip("How often (in seconds) to increase the difficulty.")]
    [SerializeField] private float increaseInterval = 20f;
    [Tooltip("How many more obstacles to allow each time the difficulty increases.")]
    [SerializeField] private int increaseAmount = 2;

    [Header("Vertical placement")]
    [Tooltip("How high above the spline we start the raycast to find the ground.")]
    public float raycastHeight = 10f; // Increased for safety
    public LayerMask groundMask;

    [Header("Debug ? Scene view")]
    public bool drawAttemptGizmos = false;

    /* ????????????????????????????? Private ????????????????????????????? */
    private Dictionary<GameObject, List<GameObject>> _pool = new Dictionary<GameObject, List<GameObject>>();
    private readonly List<GameObject> _active = new();
    private SplineContainer _spline;
    private int _currentMaxActive;
    private int _totalSpawnWeight;

    [Header("Audio")]
    [SerializeField] private AudioSourcePool obstacleAudioPool;
    void Awake()
    {
        if (obstacleTypes == null || obstacleTypes.Length == 0) { Debug.LogError("ObstacleManager: No Obstacle Types defined!"); enabled = false; return; }
        _spline = GetComponent<SplineContainer>();
        if (!laneVis) laneVis = GetComponent<LaneLinesVisualizer>();
        if (!laneVis) { Debug.LogError("ObstacleManager: LaneLinesVisualizer ref missing"); enabled = false; return; }
        _currentMaxActive = initialMaxActive;
        _totalSpawnWeight = obstacleTypes.Sum(t => t.spawnWeight);
        BuildPool();
        StartCoroutine(SpawnLoop());
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
            List<GameObject> subPool = new List<GameObject>();
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

    GameObject NextPooled(GameObject prefab)
    {
        if (_pool.TryGetValue(prefab, out var subPool))
        {
            foreach (var go in subPool)
            {
                if (!go.activeInHierarchy) return go;
            }
        }
        return null;
    }

    ObstacleType GetRandomObstacleType()
    {
        int randomWeight = UnityEngine.Random.Range(0, _totalSpawnWeight);
        foreach (var type in obstacleTypes)
        {
            if (randomWeight < type.spawnWeight)
                return type;
            randomWeight -= type.spawnWeight;
        }
        return obstacleTypes[obstacleTypes.Length - 1];
    }

    IEnumerator SpawnLoop()
    {
        var wait = new WaitForSeconds(spawnInterval);
        // 1) Hold fire until the countdown is over
        while (!PlayerJoinManager.IsRaceStarted)
            yield return wait;

        // 2) Main loop – keep running as long as the component is enabled
        while (enabled)
        {
            if (_active.Count < _currentMaxActive)
                TrySpawn();

            yield return wait;      // throttle spawn rate

            // optional: stop automatically when the race ends
            if (!PlayerJoinManager.IsRaceStarted)       // finish line reached
                yield break;
        }
    }

    void TrySpawn()
    {
        ObstacleType typeToSpawn = GetRandomObstacleType();
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

            // ---- MODIFIED SPAWN LOGIC ----
            // 1. Define the starting point of our raycast, high above the track.
            Vector3 raycastStartPoint = wp + right * offset + Vector3.up * raycastHeight;
            Vector3 finalSpawnPosition;

            // 2. Raycast down to find the ground.
            if (Physics.Raycast(raycastStartPoint, Vector3.down, out var hit, raycastHeight * 2f, groundMask))
            {
                // 3. Set the final position 0.5 units ABOVE the point of impact.
                finalSpawnPosition = hit.point + Vector3.up * 0.5f;
            }
            else
            {
                // If we don't hit the ground (unlikely on a closed track), skip this attempt.
                continue;
            }
            // ------------------------------

            if (IsFarEnough(finalSpawnPosition))
            {
                go.transform.SetPositionAndRotation(finalSpawnPosition, Quaternion.LookRotation(wt, Vector3.up));
                go.SetActive(true);
                _active.Add(go);
                if (drawAttemptGizmos) DebugDraw(finalSpawnPosition, Color.magenta);
                return;
            }
            else if (drawAttemptGizmos)
                DebugDraw(finalSpawnPosition, Color.red);
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
        Debug.DrawLine(p, p + Vector3.up * 3f, c, spawnInterval * 0.9f);
#endif
    }
}
