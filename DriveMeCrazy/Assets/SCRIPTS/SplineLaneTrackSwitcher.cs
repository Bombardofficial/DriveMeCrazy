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

        [Header("Runtime (read-only)")]
        [SerializeField] int currentLane = 0;
        [SerializeField] int targetLane = 0;
        [SerializeField] float blendT = 1f;

        float lastSwitchTime = -999f;

        // Cached transition state
        SplineContainer fromC, toC;
        Spline fromS, toS;
        float fromLen = 1f, toLen = 1f;

        public int LaneCount => laneTracks != null ? laneTracks.Length : 0;
        public int CurrentLane => currentLane;
        public int TargetLane => targetLane;
        public bool IsChanging => blendT < 1f;

        public SplineContainer ActiveLaneContainer => (laneTracks != null && laneTracks.Length > 0) ? laneTracks[currentLane] : null;
        public Spline ActiveSpline => fromS;     // idle stateben from == active
        public float ActiveLength => fromLen;    // idle stateben fromLen == active length

        float Blend01
        {
            get
            {
                float t01 = Mathf.Clamp01(blendT);
                return (blendCurve != null) ? blendCurve.Evaluate(t01) : t01;
            }
        }

        public void Initialise(int startLane)
        {
            if (laneTracks == null || laneTracks.Length == 0)
                return;

            currentLane = Mathf.Clamp(startLane, 0, laneTracks.Length - 1);
            targetLane = currentLane;
            blendT = 1f;

            CacheCurrentAsFrom();
        }

        void CacheCurrentAsFrom()
        {
            fromC = laneTracks[currentLane];
            fromS = fromC != null ? fromC.Spline : null;
            fromLen = (fromS != null) ? Mathf.Max(0.0001f, fromS.GetLength()) : 0.0001f;

            toC = fromC;
            toS = fromS;
            toLen = fromLen;
        }

        public bool RequestLane(int newLane, float traveledDistanceMeters, out float remappedTravelDistanceMeters)
        {
            remappedTravelDistanceMeters = traveledDistanceMeters;

            if (laneTracks == null || laneTracks.Length == 0)
                return false;

            newLane = Mathf.Clamp(newLane, 0, laneTracks.Length - 1);

            if (Time.time < lastSwitchTime + laneChangeCooldown)
                return false;

            // Ha már épp váltunk, akkor fejezzük be “logikailag” és onnan indítsuk az újat
            if (IsChanging)
            {
                float tNorm = traveledDistanceMeters / Mathf.Max(0.0001f, fromLen);
                remappedTravelDistanceMeters = tNorm * Mathf.Max(0.0001f, toLen);

                currentLane = targetLane;
                CacheCurrentAsFrom();

                traveledDistanceMeters = remappedTravelDistanceMeters;
            }

            if (newLane == currentLane)
                return false;

            fromC = laneTracks[currentLane];
            fromS = fromC != null ? fromC.Spline : null;
            fromLen = (fromS != null) ? Mathf.Max(0.0001f, fromS.GetLength()) : 0.0001f;

            targetLane = newLane;
            toC = laneTracks[targetLane];
            toS = toC != null ? toC.Spline : null;
            toLen = (toS != null) ? Mathf.Max(0.0001f, toS.GetLength()) : fromLen;

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

            if (blendT >= 0.99999f)
            {
                // Commit + distance remap (ugyanaz a normalized t marad, csak az új spline lengthre skálázunk)
                float tNorm = traveledDistanceMeters / Mathf.Max(0.0001f, fromLen);
                traveledDistanceMeters = tNorm * Mathf.Max(0.0001f, toLen);

                currentLane = targetLane;
                CacheCurrentAsFrom();
                blendT = 1f;

                justFinished = true;
            }
        }

        public void ForceSetLane(int laneIndex, ref float traveledDistanceMeters)
        {
            if (laneTracks == null || laneTracks.Length == 0)
                return;

            laneIndex = Mathf.Clamp(laneIndex, 0, laneTracks.Length - 1);

            if (IsChanging)
            {
                float tNorm = traveledDistanceMeters / Mathf.Max(0.0001f, fromLen);
                traveledDistanceMeters = tNorm * Mathf.Max(0.0001f, toLen);
            }

            currentLane = targetLane = laneIndex;
            blendT = 1f;

            CacheCurrentAsFrom();
        }

        public void GetBlendedSample(float traveledDistanceMeters,
                                     out Vector3 worldPos, out Vector3 worldTangent, out Vector3 worldUp)
        {
            if (fromC == null || fromS == null)
            {
                worldPos = transform.position;
                worldTangent = transform.forward;
                worldUp = transform.up;
                return;
            }

            float baseLen = Mathf.Max(0.0001f, fromLen);
            float t = Mathf.Repeat(traveledDistanceMeters / baseLen, 1f);

            Sample(fromC, fromS, t, out var pA, out var tA, out var uA);

            if (IsChanging && toC != null && toS != null)
            {
                Sample(toC, toS, t, out var pB, out var tB, out var uB);

                float b = Blend01;
                worldPos = Vector3.LerpUnclamped(pA, pB, b);
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

        static void Sample(SplineContainer c, Spline s, float t,
                           out Vector3 worldPos, out Vector3 worldTangent, out Vector3 worldUp)
        {
            SplineUtility.Evaluate(s, t, out float3 lp, out float3 lt, out float3 lu);

            worldPos = c.transform.TransformPoint(lp);
            worldTangent = c.transform.TransformDirection(lt).normalized;
            worldUp = c.transform.TransformDirection(lu).normalized;
        }
    }
}
