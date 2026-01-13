using System;
using UnityEngine;

[DisallowMultipleComponent]
public class CarTextureByDriver : MonoBehaviour
{
    [Header("Target Renderers (car body, etc.)")]
    [Tooltip("Ha üres, automatikusan megfogja a gyerek Renderereket is.")]
    [SerializeField] private Renderer[] targetRenderers;

    [Header("Player Textures (index 0 = Player1, 1 = Player2, ...)")]
    [SerializeField] private Texture2D[] playerTextures = new Texture2D[4];

    [Header("Texture Property")]
    [Tooltip("Ha üres, automatikusan próbálja: _BaseMap (URP), _MainTex (Standard), _BaseColorMap, _AlbedoMap")]
    [SerializeField] private string forcedTextureProperty = "";

    [SerializeField]
    private string[] texturePropertyCandidates = new[]
    {
        "_BaseMap",       // URP Lit base map
        "_MainTex",       // Standard
        "_BaseColorMap",  // HDRP / custom
        "_AlbedoMap"      // custom
    };

    [Header("Apply Mode")]
    [Tooltip("Ha true: sharedMaterialt módosít (globális, lobbyra is kihat ha ugyanazt a material assetet használja).")]
    [SerializeField] private bool modifySharedMaterials = true;

    [Tooltip("Ha true: minden materialon próbálja (renderer.sharedMaterials). Ha false: csak a 0. material.")]
    [SerializeField] private bool applyToAllMaterialsOnRenderer = true;

    private int _texPropId = -1;
    private bool _resolved;

    void Awake()
    {
        if (targetRenderers == null || targetRenderers.Length == 0)
            targetRenderers = GetComponentsInChildren<Renderer>(true);

        ResolveTextureProperty();
    }

    void OnEnable()
    {
        PlayerManager.OnDriverChanged += HandleDriverChanged;

        // Ha már van driver (pl. quickStart), akkor azonnal apply
        if (PlayerManager.Instance != null && PlayerManager.Instance.CurrentDriver != null)
            ApplyForDriver(PlayerManager.Instance.CurrentDriver);
    }

    void OnDisable()
    {
        PlayerManager.OnDriverChanged -= HandleDriverChanged;
    }

    private void HandleDriverChanged(Passenger oldDriver, Passenger newDriver)
    {
        ApplyForDriver(newDriver);
    }

    public void ApplyForDriver(Passenger driver)
    {
        if (driver == null) return;
        int idx = driver.PlayerNumber - 1;

        if (playerTextures == null || idx < 0 || idx >= playerTextures.Length)
            return;

        Texture2D tex = playerTextures[idx];
        if (!tex) return;

        if (!_resolved) ResolveTextureProperty();
        if (_texPropId == -1) return;

        ApplyTexture(tex);
    }

    private void ApplyTexture(Texture2D tex)
    {
        if (targetRenderers == null) return;

        for (int i = 0; i < targetRenderers.Length; i++)
        {
            var r = targetRenderers[i];
            if (!r) continue;

            if (modifySharedMaterials)
            {
                // GLOBALIS: sharedMaterials (mindenhol kihat ahol ezt a material assetet használják)
                var mats = r.sharedMaterials;
                if (mats == null || mats.Length == 0) continue;

                if (applyToAllMaterialsOnRenderer)
                {
                    for (int m = 0; m < mats.Length; m++)
                    {
                        var mat = mats[m];
                        if (!mat) continue;
                        if (!mat.HasProperty(_texPropId)) continue;

                        mat.SetTexture(_texPropId, tex);
                    }
                }
                else
                {
                    var mat = mats[0];
                    if (mat && mat.HasProperty(_texPropId))
                        mat.SetTexture(_texPropId, tex);
                }
            }
            else
            {
                // LOKÁLIS: instanced material (csak ezen az 1 objektumon)
                // (Nálad most inkább a shared kell, de meghagytam opcióként.)
                var mats = r.materials;
                if (mats == null || mats.Length == 0) continue;

                if (applyToAllMaterialsOnRenderer)
                {
                    for (int m = 0; m < mats.Length; m++)
                    {
                        var mat = mats[m];
                        if (!mat) continue;
                        if (!mat.HasProperty(_texPropId)) continue;

                        mat.SetTexture(_texPropId, tex);
                    }
                }
                else
                {
                    var mat = mats[0];
                    if (mat && mat.HasProperty(_texPropId))
                        mat.SetTexture(_texPropId, tex);
                }
            }
        }
    }

    private void ResolveTextureProperty()
    {
        _resolved = true;
        _texPropId = -1;

        // Megnézzük a target rendererek materialjait, és találunk egy olyan property-t,
        // ami tényleg létezik.
        if (targetRenderers == null || targetRenderers.Length == 0)
            return;

        // 1) forced
        if (!string.IsNullOrEmpty(forcedTextureProperty))
        {
            int id = Shader.PropertyToID(forcedTextureProperty);
            if (AnyMaterialHasProperty(id))
            {
                _texPropId = id;
                return;
            }
        }

        // 2) candidates
        if (texturePropertyCandidates != null)
        {
            for (int i = 0; i < texturePropertyCandidates.Length; i++)
            {
                string n = texturePropertyCandidates[i];
                if (string.IsNullOrEmpty(n)) continue;

                int id = Shader.PropertyToID(n);
                if (AnyMaterialHasProperty(id))
                {
                    _texPropId = id;
                    return;
                }
            }
        }

        Debug.LogWarning("[CarTextureByDriver] Nem találtam texture property-t. Add meg forcedTextureProperty-t (pl. _BaseMap vagy a custom shader property neve).", this);
    }

    private bool AnyMaterialHasProperty(int propId)
    {
        for (int i = 0; i < targetRenderers.Length; i++)
        {
            var r = targetRenderers[i];
            if (!r) continue;

            var mats = r.sharedMaterials;
            if (mats == null) continue;

            for (int m = 0; m < mats.Length; m++)
            {
                var mat = mats[m];
                if (mat && mat.HasProperty(propId))
                    return true;
            }
        }
        return false;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (playerTextures == null) playerTextures = new Texture2D[4];
        if (playerTextures.Length != 4) Array.Resize(ref playerTextures, 4);
    }
#endif
}
