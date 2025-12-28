using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

namespace ArcadeVP
{
    [DisallowMultipleComponent]
    public class SplineLaneTrackSwitcher : MonoBehaviour
    {
        [Header("Lane spline tracks (0..N-1)")]
        public SplineContainer[] laneTracks;

        [Header("Switch settings")]
        [Min(0.01f)] public float laneChangeDuration = 0.18f;
        [Min(0f)] public float laneChangeCooldown = 0.75f;
        public AnimationCurve blendCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Arc-length sampling (anti time-warp)")]
        [Range(32, 1024)]
        public int arcSamples = 256;

        [Tooltip("Reference lane index used as 'master distance' length. (1 = middle lane if you have 3)")]
        public int referenceLaneIndex = 1;

        [Header("Runtime (read-only)")]
        [SerializeField] int currentLane = 0;
        [SerializeField] int targetLane = 0;
        [SerializeField] float blendT = 1f;

        float lastSwitchTime = -999f;

        struct LaneCache
        {
            public SplineContainer c;
            public Spline s;
            public float lengthWorld;     // arc-length approx in world space
            public float[] cumDist;       // cumulative distance at each sample
            public float[] tAt;           // corresponding spline t for each sample
            public bool valid;
        }

        LaneCache[] _cache;
        float _referenceLength = 1f;

        public int LaneCount => laneTracks != null ? laneTracks.Length : 0;
        public int CurrentLane => currentLane;
        public int TargetLane => targetLane;
        public bool IsChanging => blendT < 1f;

        public float ReferenceLength => Mathf.Max(0.0001f, _referenceLength);

        // IMPORTANT: return the reference length so the controller's traveledDistance wrapping is stable
        public float ActiveLength => ReferenceLength;

        public SplineContainer ActiveLaneContainer =>
            (laneTracks != null && laneTracks.Length > 0) ? laneTracks[currentLane] : null;

        // "active spline" is current lane spline (idle) or from-lane while switching
        public Spline ActiveSpline =>
            (_cache != null && _cache.Length > 0 && _cache[currentLane].valid) ? _cache[currentLane].s : null;

        float Blend01
        {
            get
            {
                float t01 = Mathf.Clamp01(blendT);
                return (blendCurve != null) ? blendCurve.Evaluate(t01) : t01;
            }
        }

        void OnValidate()
        {
            arcSamples = Mathf.Clamp(arcSamples, 32, 1024);
        }

        public void Initialise(int startLane)
        {
            if (laneTracks == null || laneTracks.Length == 0)
                return;

            BuildAllCaches();

            currentLane = Mathf.Clamp(startLane, 0, laneTracks.Length - 1);
            targetLane = currentLane;
            blendT = 1f;

            // Pick a stable reference length (middle lane if possible)
            int refIdx = Mathf.Clamp(referenceLaneIndex, 0, laneTracks.Length - 1);
            if (_cache != null && _cache.Length > refIdx && _cache[refIdx].valid)
                _referenceLength = _cache[refIdx].lengthWorld;
            else if (_cache != null && _cache.Length > currentLane && _cache[currentLane].valid)
                _referenceLength = _cache[currentLane].lengthWorld;
            else
                _referenceLength = 1f;
        }

        void BuildAllCaches()
        {
            int n = LaneCount;
            _cache = new LaneCache[n];

            for (int i = 0; i < n; i++)
            {
                var c = laneTracks[i];
                if (c == null || c.Spline == null)
                {
                    _cache[i] = new LaneCache { valid = false };
                    continue;
                }

                var s = c.Spline;
                int N = Mathf.Max(32, arcSamples);

                float[] cum = new float[N + 1];
                float[] tt = new float[N + 1];

                Vector3 prev = EvalWorldPos(c, s, 0f);
                cum[0] = 0f;
                tt[0] = 0f;

                float total = 0f;
                for (int k = 1; k <= N; k++)
                {
                    float t = k / (float)N;
                    Vector3 p = EvalWorldPos(c, s, t);
                    total += Vector3.Distance(prev, p);
                    prev = p;

                    cum[k] = total;
                    tt[k] = t;
                }

                _cache[i] = new LaneCache
                {
                    c = c,
                    s = s,
                    lengthWorld = Mathf.Max(0.0001f, total),
                    cumDist = cum,
                    tAt = tt,
                    valid = true
                };
            }
        }

        static Vector3 EvalWorldPos(SplineContainer c, Spline s, float t)
        {
            SplineUtility.Evaluate(s, t, out float3 lp, out _, out _);
            return c.transform.TransformPoint(lp);
        }

        float DistanceToT(int laneIdx, float dist)
        {
            var lc = _cache[laneIdx];
            if (!lc.valid || lc.cumDist == null || lc.cumDist.Length < 2)
                return Mathf.Clamp01(dist / Mathf.Max(0.0001f, lc.lengthWorld));

            float len = lc.lengthWorld;
            dist = Mathf.Clamp(dist, 0f, len);

            var cum = lc.cumDist;
            var tt = lc.tAt;

            int lo = 0;
            int hi = cum.Length - 1;

            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (cum[mid] <= dist) lo = mid;
                else hi = mid;
            }

            float d0 = cum[lo];
            float d1 = cum[hi];
            float t0 = tt[lo];
            float t1 = tt[hi];

            float a = (Mathf.Abs(d1 - d0) < 1e-6f) ? 0f : (dist - d0) / (d1 - d0);
            return Mathf.LerpUnclamped(t0, t1, a);
        }

        public bool RequestLane(int newLane, float traveledDistanceMeters, out float remappedTravelDistanceMeters)
        {
            remappedTravelDistanceMeters = traveledDistanceMeters;

            if (laneTracks == null || laneTracks.Length == 0)
                return false;

            newLane = Mathf.Clamp(newLane, 0, laneTracks.Length - 1);

            if (Time.time < lastSwitchTime + laneChangeCooldown)
                return false;

            // If already changing, commit logically (WITHOUT touching traveled distance)
            if (IsChanging)
            {
                currentLane = targetLane;
                blendT = 1f;
            }

            if (newLane == currentLane)
                return false;

            targetLane = newLane;
            blendT = 0f;
            lastSwitchTime = Time.time;
            return true;
        }

        public void Tick(float dt, ref float traveledDistanceMeters, out bool justFinished)
        {
            justFinished = false;
            if (!IsChanging) return;

            float dur = Mathf.Max(0.0001f, laneChangeDuration);
            blendT = Mathf.Clamp01(blendT + dt / dur);

            // Keep traveled distance stable in the controller’s reference-length space
            traveledDistanceMeters = Mathf.Repeat(traveledDistanceMeters, ReferenceLength);

            if (blendT >= 0.99999f)
            {
                currentLane = targetLane;
                blendT = 1f;
                justFinished = true;
            }
        }

        public void ForceSetLane(int laneIndex, ref float traveledDistanceMeters)
        {
            if (laneTracks == null || laneTracks.Length == 0)
                return;

            laneIndex = Mathf.Clamp(laneIndex, 0, laneTracks.Length - 1);

            currentLane = targetLane = laneIndex;
            blendT = 1f;

            traveledDistanceMeters = Mathf.Repeat(traveledDistanceMeters, ReferenceLength);
        }

        public void GetBlendedSample(float traveledDistanceMeters,
                                     out Vector3 worldPos, out Vector3 worldTangent, out Vector3 worldUp)
        {
            if (_cache == null || _cache.Length == 0 || !_cache[currentLane].valid)
            {
                worldPos = transform.position;
                worldTangent = transform.forward;
                worldUp = transform.up;
                return;
            }

            // MASTER progress in 0..1 based on stable ReferenceLength
            float refLen = ReferenceLength;
            float progress01 = Mathf.Repeat(traveledDistanceMeters / refLen, 1f);

            SampleLaneAtProgress(currentLane, progress01, out var pA, out var tA, out var uA);

            if (IsChanging && targetLane >= 0 && targetLane < _cache.Length && _cache[targetLane].valid)
            {
                SampleLaneAtProgress(targetLane, progress01, out var pB, out var tB, out var uB);

                float b = Blend01;
                worldPos = Vector3.Lerp(pA, pB, b);
                worldTangent = Vector3.Slerp(tA, tB, b).normalized;
                worldUp = Vector3.Slerp(uA, uB, b).normalized;
            }
            else
            {
                worldPos = pA;
                worldTangent = tA.normalized;
                worldUp = uA.normalized;
            }
        }

        void SampleLaneAtProgress(int laneIdx, float progress01,
                                  out Vector3 worldPos, out Vector3 worldTangent, out Vector3 worldUp)
        {
            var lc = _cache[laneIdx];
            if (!lc.valid || lc.c == null || lc.s == null)
            {
                worldPos = transform.position;
                worldTangent = transform.forward;
                worldUp = transform.up;
                return;
            }

            float dist = Mathf.Clamp01(progress01) * lc.lengthWorld;
            float t = DistanceToT(laneIdx, dist);

            SplineUtility.Evaluate(lc.s, t, out float3 lp, out float3 lt, out float3 lu);

            worldPos = lc.c.transform.TransformPoint(lp);
            worldTangent = lc.c.transform.TransformDirection(lt).normalized;
            worldUp = lc.c.transform.TransformDirection(lu).normalized;
        }
    }
}
