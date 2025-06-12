using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

[ExecuteAlways]
[RequireComponent(typeof(SplineContainer))]
public class LaneLinesVisualizer : MonoBehaviour
{
    [Header("Sampling")]
    public float samplesPerUnit = 1f;

    [Header("Lane Offsets (m)")]
    public float[] laneOffsets = { -2f, 0f, 2f };

    [Header("Draw Boundaries")]
    public bool drawBoundaries = true;

    [Header("Lane Line")]
    public Material laneLineMaterial;
    public float laneLineWidth = .1f;
    public Color laneLineColor = Color.white;

    [Header("Boundary Line")]
    public Material boundaryLineMaterial;
    public float boundaryLineWidth = .2f;
    public Color boundaryLineColor = Color.white;

    [Header("Ground Projection")]
    public LayerMask drivableSurface;
    public float raycastHeight = 5f;
    public float groundCheckDistance = 10f;

    [Header("Line Height Offset")]
    public float lineHeightOffset = .02f;

    /*???????? Internal ????????*/
    LineRenderer[] laneRenderers;
    LineRenderer[] boundaryRenderers;
    int lastSampleCount = -1;
    float[] lastLaneOffsets;
    bool _needsRebuild = false;

    /*???????? Unity events ????????*/
    void OnEnable() => BuildLaneLines();
    void OnValidate()
    {
        _needsRebuild = true;
    }
    void Update()
    {
#if UNITY_EDITOR
        // Don’t run in Prefab Mode (no real scene yet)
        if (UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() != null)
            return;
#endif
        if (_needsRebuild)
        {
            BuildLaneLines();
            _needsRebuild = false;
        }
    }

    void OnDestroy() => DestroyAllChildLineRenderers();

    /*???????? Build helpers ????????*/
    void BuildLaneLines()
    {
        var container = GetComponent<SplineContainer>();
        var spline = container.Spline;
        if (spline.GetLength() <= 0f) return;

        int samples = Mathf.CeilToInt(spline.GetLength() * Mathf.Max(0.1f, samplesPerUnit)) + 1;

        bool topologyChanged = samples != lastSampleCount ||
                               lastLaneOffsets == null ||
                               laneOffsets.Length != lastLaneOffsets.Length;

        if (topologyChanged) DestroyAllChildLineRenderers();

        // If only colours / widths changed we can reuse
        if (laneRenderers == null || laneRenderers.Length != laneOffsets.Length)
            laneRenderers = new LineRenderer[laneOffsets.Length];

        for (int i = 0; i < laneOffsets.Length; i++)
        {
            var lr = EnsureRenderer(ref laneRenderers[i], $"Lane_{i}",
                                    laneLineMaterial, laneLineWidth, laneLineColor, samples);
            SampleSpline(lr, laneOffsets[i], samples, container, spline);
        }

        /* boundaries */
        if (drawBoundaries && laneOffsets.Length >= 2)
        {
            int L = laneOffsets.Length;
            float halfSpacing = Mathf.Abs(laneOffsets[1] - laneOffsets[0]) * .5f;

            float[] bOffsets = new float[L + 1];
            for (int i = 0; i <= L; i++)
                bOffsets[i] = (i == 0) ? laneOffsets[0] - halfSpacing
                           : (i == L) ? laneOffsets[L - 1] + halfSpacing
                                              : (laneOffsets[i - 1] + laneOffsets[i]) * .5f;

            if (boundaryRenderers == null || boundaryRenderers.Length != bOffsets.Length)
                boundaryRenderers = new LineRenderer[bOffsets.Length];

            for (int i = 0; i < bOffsets.Length; i++)
            {
                var lr = EnsureRenderer(ref boundaryRenderers[i], $"Boundary_{i}",
                                        boundaryLineMaterial, boundaryLineWidth, boundaryLineColor, samples);
                SampleSpline(lr, bOffsets[i], samples, container, spline);
            }
        }

        lastSampleCount = samples;
        lastLaneOffsets = (float[])laneOffsets.Clone();
    }

    LineRenderer EnsureRenderer(ref LineRenderer lr,
                                string name, Material mat, float width, Color col, int samples)
    {
        if (!lr)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(transform, false);
            lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.alignment = LineAlignment.View;
            lr.numCapVertices = lr.numCornerVertices = 8;
        }

        lr.positionCount = samples;
        lr.material = mat;
        lr.widthMultiplier = width;
        lr.startColor = lr.endColor = col;
        return lr;
    }

    void SampleSpline(LineRenderer lr, float offset, int samples,
                      SplineContainer container, Spline spline)
    {
        for (int s = 0; s < samples; s++)
        {
            float t = s / (samples - 1f);

            float3 lp = spline.EvaluatePosition(t);
            float3 lt = math.normalize(spline.EvaluateTangent(t));

            Vector3 worldP = container.transform.TransformPoint(lp);
            Vector3 worldT = container.transform.TransformDirection(lt).normalized;

            Vector3 rightXZ = Vector3.Cross(Vector3.up, worldT).normalized;

            Vector3 sample = worldP + rightXZ * offset + Vector3.up * raycastHeight;

            if (Physics.Raycast(sample, Vector3.down, out var hit,
                                raycastHeight + groundCheckDistance, drivableSurface))
                sample.y = hit.point.y + lineHeightOffset;
            else
                sample.y = worldP.y + lineHeightOffset;

            lr.SetPosition(s, sample);
        }
    }

    void DestroyAllChildLineRenderers()
    {
        foreach (var lr in GetComponentsInChildren<LineRenderer>())
        {
            if (Application.isPlaying)
                Destroy(lr.gameObject);          // safe in Play Mode
            else
                DestroyImmediate(lr.gameObject); // required in Edit Mode
        }

        laneRenderers = boundaryRenderers = null;
    }
}
