using UnityEngine;
using UnityEngine.UI;
using System.Collections;

namespace ArcadeVP
{
    public class SpeedZoneBalanceUI : MonoBehaviour
    {
        [Header("References")]
        public RectTransform bar;
        public RectTransform greenZone;
        public RectTransform pointer;

        public bool IsVisible => cg && cg.alpha > 0.01f;
        public bool IsFullyVisible => cg && gameObject.activeInHierarchy && cg.alpha >= 0.99f;

        [Header("Dynamic Zone Behaviour")]
        [Tooltip("Peak fraction of bar half-width used for oscillation")] public float oscillationAmplitude = 0.30f;
        [Tooltip("Oscillation frequency in Hz")] public float oscillationFreq = 0.45f;
        [Tooltip("Zone shrink rate in frac/sec")] public float shrinkRate = 0.08f;
        [Tooltip("Absolute minimum green-zone width in px")] public float minZoneWidthPx = 40f;

        [Header("Fade")]
        [Tooltip("UI fade-in speed (alpha units per second)")]
        public float fadeInSpeed = 12f;
        [Tooltip("UI fade-out speed (alpha units per second)")]
        public float fadeOutSpeed = 12f;

        float barHalfWidth;
        float zoneHalfWidth;    // px
        float zoneCentre;       // px (±barHalfWidth)
        float elapsed;

        CanvasGroup cg;

        public float NormalizedError { get; private set; }  // 0=center, 1=edge

        void Awake()
        {
            cg = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();

            // Prevent first-time “showcase” when parent canvas is enabled:
            cg.alpha = 0f;
            // It’s fine for this GO to be enabled/disabled by your parent,
            // but we ensure it starts invisible.
        }

        /* ================== API ================== */
        public void Begin(float greenFrac)
        {
            StopAllCoroutines();                 // idempotent
            LayoutRebuilder.ForceRebuildLayoutImmediate(bar);

            barHalfWidth = bar.rect.width * 0.5f;
            greenFrac = Mathf.Clamp(greenFrac, 0.05f, 0.8f);
            zoneHalfWidth = Mathf.Max(minZoneWidthPx, bar.rect.width * 0.5f * greenFrac);
            zoneCentre = 0f;
            elapsed = 0f;
            NormalizedError = 0f;

            ApplyZoneVisual();
            SetPointer(0f);

            // Ensure visible object + start from 0 alpha (fast, fair fade)
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            cg.alpha = 0f;
            StartCoroutine(FadeIn());
        }

        public void End()
        {
            StopAllCoroutines();

            // If inactive in hierarchy, don’t try to fade — just force hidden state.
            if (!gameObject.activeInHierarchy)
            {
                if (cg == null) cg = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
                cg.alpha = 0f;
                // keep inactive as is
                return;
            }

            StartCoroutine(FadeOut());
        }

        public void Tick(float dt)
        {
            elapsed += dt;
            zoneCentre = Mathf.Sin(elapsed * oscillationFreq * Mathf.PI * 2f) * oscillationAmplitude * barHalfWidth;
            zoneHalfWidth = Mathf.Max(minZoneWidthPx, zoneHalfWidth - shrinkRate * bar.rect.width * dt);
            ApplyZoneVisual();
            RecomputeError();
        }

        public void SetPointer(float t)
        {
            t = Mathf.Clamp(t, -1f, 1f);
            pointer.anchoredPosition = new Vector2(t * barHalfWidth, 0f);
            RecomputeError();
        }

        public bool IsOutside(float t)
        {
            float px = Mathf.Clamp(t, -1f, 1f) * barHalfWidth;
            return px < (zoneCentre - zoneHalfWidth) || px > (zoneCentre + zoneHalfWidth);
        }

        /* ------------- helpers ------------- */
        void RecomputeError()
        {
            float px = Mathf.Abs(pointer.anchoredPosition.x - zoneCentre);
            NormalizedError = Mathf.InverseLerp(0f, zoneHalfWidth, px);
        }

        void ApplyZoneVisual()
        {
            float width = zoneHalfWidth * 2f;
            greenZone.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            greenZone.anchoredPosition = new Vector2(zoneCentre, 0f);
        }

        IEnumerator FadeIn()
        {
            while (cg.alpha < 1f)
            {
                cg.alpha = Mathf.MoveTowards(cg.alpha, 1f, fadeInSpeed * Time.deltaTime);
                yield return null;
            }
            cg.alpha = 1f;
        }

        IEnumerator FadeOut()
        {
            while (cg.alpha > 0f)
            {
                cg.alpha = Mathf.MoveTowards(cg.alpha, 0f, fadeOutSpeed * Time.deltaTime);
                yield return null;
            }
            cg.alpha = 0f;

            // Optional: disable after fade to keep hierarchy clean
            gameObject.SetActive(false);
        }
    }
}
