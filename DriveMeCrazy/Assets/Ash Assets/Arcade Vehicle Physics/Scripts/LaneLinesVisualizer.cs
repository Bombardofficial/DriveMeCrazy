using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;  // for math.normalize

[ExecuteAlways]
[RequireComponent(typeof(SplineContainer))]
public class LaneLinesVisualizer : MonoBehaviour
{
    [Header("Sampling")]
    [Tooltip("How many points to sample per metre of spline length.")]
    public float samplesPerUnit = 1f;

    [Header("Lane Offsets (m)")]
    [Tooltip("Lateral offsets from centre spline (in XZ-plane) for each lane centre.")]
    public float[] laneOffsets = new float[] { -2f, 0f, +2f };

    [Header("Draw Boundaries")]
    public bool drawBoundaries = true;

    [Header("Lane Line Renderer Settings")]
    public Material laneLineMaterial;
    public float laneLineWidth = 0.1f;
    public Color laneLineColor = Color.white;

    [Header("Boundary Line Renderer Settings")]
    public Material boundaryLineMaterial;
    public float boundaryLineWidth = 0.2f;
    public Color boundaryLineColor = Color.white;

    [Header("Ground Projection")]
    [Tooltip("Which layers count as ground.")]
    public LayerMask drivableSurface;
    [Tooltip("Height above sample point to start the ray.")]
    public float raycastHeight = 5f;
    [Tooltip("Maximum distance the ray will travel downwards.")]
    public float groundCheckDistance = 10f;

    [Header("Line Height Offset")]
    [Tooltip("How far above the ground to place the lines (to avoid Z-fighting)")]
    public float lineHeightOffset = 0.02f;

    private LineRenderer[] laneRenderers;
    private LineRenderer[] boundaryRenderers;

    void OnEnable() => BuildLaneLines();
    void OnValidate() => BuildLaneLines();
    void OnDestroy()
    {
        DestroyRenderers(laneRenderers);
        DestroyRenderers(boundaryRenderers);
    }

    private void DestroyRenderers(LineRenderer[] rrs)
    {
        if (rrs == null) return;
        foreach (var lr in rrs) if (lr) DestroyImmediate(lr.gameObject);
    }

    private void BuildLaneLines()
    {
        var container = GetComponent<SplineContainer>();
        var spline = container.Spline;
        float length = spline.GetLength();
        int samples = Mathf.CeilToInt(length * samplesPerUnit) + 1;

        // clean up old
        DestroyRenderers(laneRenderers);
        DestroyRenderers(boundaryRenderers);

        // --- centre lane lines ---
        laneRenderers = new LineRenderer[laneOffsets.Length];
        for (int i = 0; i < laneOffsets.Length; i++)
        {
            var lr = CreateRenderer($"Lane_{i}", samples,
                                    laneLineMaterial, laneLineWidth, laneLineColor);
            SampleAndPlace(lr, laneOffsets[i], samples, container, spline);
            laneRenderers[i] = lr;
        }

        // --- boundary lines (one more than lane count) ---
        if (drawBoundaries && laneOffsets.Length >= 2)
        {
            int L = laneOffsets.Length;
            // uniform half-spacing
            float halfSpacing = Mathf.Abs(laneOffsets[1] - laneOffsets[0]) * 0.5f;

            // compute L+1 boundary offsets
            float[] bOffsets = new float[L + 1];
            for (int i = 0; i <= L; i++)
            {
                if (i == 0) bOffsets[i] = laneOffsets[0] - halfSpacing;
                else if (i == L) bOffsets[i] = laneOffsets[L - 1] + halfSpacing;
                else bOffsets[i] = (laneOffsets[i - 1] + laneOffsets[i]) * 0.5f;
            }

            boundaryRenderers = new LineRenderer[bOffsets.Length];
            for (int i = 0; i < bOffsets.Length; i++)
            {
                var lr = CreateRenderer($"Boundary_{i}", samples,
                                        boundaryLineMaterial, boundaryLineWidth, boundaryLineColor);
                SampleAndPlace(lr, bOffsets[i], samples, container, spline);
                boundaryRenderers[i] = lr;
            }
        }
    }

    private LineRenderer CreateRenderer(string name, int samples,
                                        Material mat, float width, Color col)
    {
        var go = new GameObject(name);
        go.hideFlags = HideFlags.DontSave;
        go.transform.SetParent(transform, false);

        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.alignment = LineAlignment.View;    // face the camera
        lr.positionCount = samples;
        lr.material = mat;
        lr.widthMultiplier = width;
        lr.startColor = lr.endColor = col;

        // smoother end-caps & corner joins
        lr.numCapVertices = 8;
        lr.numCornerVertices = 8;

        return lr;
    }

    private void SampleAndPlace(LineRenderer lr, float offset, int samples,
                                SplineContainer container, Spline spline)
    {
        for (int s = 0; s < samples; s++)
        {
            float t = s / (float)(samples - 1);

            // local sample
            float3 lp = spline.EvaluatePosition(t);
            float3 lt = spline.EvaluateTangent(t);
            float3 ltN = math.normalize(lt);

            // world conversion
            Vector3 worldP = container.transform.TransformPoint(new Vector3(lp.x, lp.y, lp.z));
            Vector3 worldT = container.transform
                                  .TransformDirection(new Vector3(ltN.x, ltN.y, ltN.z))
                                  .normalized;

            // lateral XZ offset
            Vector3 forwardXZ = Vector3.ProjectOnPlane(worldT, Vector3.up).normalized;
            Vector3 rightXZ = Vector3.Cross(Vector3.up, forwardXZ).normalized;

            // ray origin + offset
            Vector3 samplePos = new Vector3(worldP.x, worldP.y + raycastHeight, worldP.z)
                              + rightXZ * offset;

            // project to ground
            if (Physics.Raycast(samplePos, Vector3.down,
                                out RaycastHit hit,
                                raycastHeight + groundCheckDistance,
                                drivableSurface))
            {
                samplePos.y = hit.point.y + lineHeightOffset;
            }
            else
            {
                samplePos.y = worldP.y + lineHeightOffset;
            }

            lr.SetPosition(s, samplePos);
        }
    }
}
