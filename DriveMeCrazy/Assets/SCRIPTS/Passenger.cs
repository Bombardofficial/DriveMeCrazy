using UnityEngine;

/// A player avatar that can earn points.
/// *No* seat bookkeeping lives here – that’s the manager’s job.
public class Passenger : MonoBehaviour
{
    public int PlayerNumber { get; internal set; }

    int _points;
    public int Points => _points;

    [Header("Player Color (tint)")]
    [Tooltip("If empty, it will auto-grab all Renderers in children (including inactive).")]
    [SerializeField] private Renderer[] tintRenderers;

    [Tooltip("Tried in order. URP Lit usually uses _BaseColor, Standard uses _Color.")]
    [SerializeField] private string[] colorPropertyCandidates = new[] { "_BaseColor", "_Color", "_TintColor" };

    private MaterialPropertyBlock _mpb;
    private int _colorPropId = -1;

    void Awake()
    {
        if (tintRenderers == null || tintRenderers.Length == 0)
            tintRenderers = GetComponentsInChildren<Renderer>(true);

        _mpb = new MaterialPropertyBlock();
        ResolveColorProperty();
    }

    void Start() => _points = 0;

    public void IncrementPoints(int amount) => _points += amount;

    /// Call this from PlayerManager after PlayerNumber is assigned.
    public void ApplyPlayerColor(Color c, string explicitProperty = null)
    {
        if (_mpb == null) _mpb = new MaterialPropertyBlock();

        int prop = ResolveColorProperty(explicitProperty);
        if (prop == -1) return;

        for (int i = 0; i < tintRenderers.Length; i++)
        {
            var r = tintRenderers[i];
            if (!r) continue;

            if (r.GetComponentInParent<HatSocketMarker>() != null)
                continue;

            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(prop, c);
            r.SetPropertyBlock(_mpb);
        }
    }

    private void ResolveColorProperty()
    {
        ResolveColorProperty(null);
    }

    private int ResolveColorProperty(string explicitProperty)
    {
        // If user explicitly forces a property name, try it first.
        if (!string.IsNullOrEmpty(explicitProperty))
        {
            int id = Shader.PropertyToID(explicitProperty);
            if (HasAnyRendererProperty(id))
            {
                _colorPropId = id;
                return id;
            }
        }

        // If we already resolved earlier, keep it (unless shader changed).
        if (_colorPropId != -1 && HasAnyRendererProperty(_colorPropId))
            return _colorPropId;

        // Try candidates
        for (int i = 0; i < colorPropertyCandidates.Length; i++)
        {
            string name = colorPropertyCandidates[i];
            if (string.IsNullOrEmpty(name)) continue;

            int id = Shader.PropertyToID(name);
            if (HasAnyRendererProperty(id))
            {
                _colorPropId = id;
                return id;
            }
        }

        Debug.LogWarning(
            $"[Passenger] No compatible color property found on '{gameObject.name}'. " +
            $"Add the correct property name to colorPropertyCandidates (shader dependent).", this);

        _colorPropId = -1;
        return -1;
    }

    private bool HasAnyRendererProperty(int propId)
    {
        if (tintRenderers == null) return false;

        for (int i = 0; i < tintRenderers.Length; i++)
        {
            var r = tintRenderers[i];
            if (!r) continue;

            var mat = r.sharedMaterial;
            if (mat && mat.HasProperty(propId))
                return true;
        }
        return false;
    }
}
