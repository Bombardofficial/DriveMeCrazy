using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/* ==================================================================================================
   SPEED?ZONE MINI?GAME  –  "Dynamic Pressure Gauge"
   -----------------------------------------------------------------------------------------------
   ? Pointer slides horizontally (?1 .. +1).
   ? Green safe?zone SHRINKS gradually and OSCILLATES left?right.
   ? Driver throttles (accel) / brakes (slow) to nudge the pointer.
   ? Fail if pointer leaves safe?zone for longer than grace time (checked by controller).
   ? Fade?in / Fade?out behaviour unchanged so existing Begin()/End() calls still work.
   ==================================================================================================*/

namespace ArcadeVP
{
    public class SpeedZoneBalanceUI : MonoBehaviour
    {
        [Header("References")]
        public RectTransform bar;          // background (600×100)
        public RectTransform greenZone;    // green block (child of bar)
        public RectTransform pointer;      // thin red bar (child of bar)

        [Header("Dynamic Zone Behaviour")]
        [Tooltip("Peak fraction of bar half?width used for oscillation")] public float oscillationAmplitude = 0.30f;
        [Tooltip("Oscillation frequency in Hz")] public float oscillationFreq = 0.45f;
        [Tooltip("Zone shrink rate in frac/sec")] public float shrinkRate = 0.15f;
        [Tooltip("Absolute minimum green?zone width in px")] public float minZoneWidthPx = 40f;

        /* runtime */
        float barHalfWidth;
        float zoneHalfWidth;            // current half?width (px)
        float zoneCentre;               // centre X (px, ±barHalfWidth)
        float elapsed;

        CanvasGroup cg;

        /* ------------- public readouts ------------- */
        public float NormalizedError { get; private set; }  // 0 = perfect centre, 1 = at/over edge

        void Awake()
        {
            cg = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
            gameObject.SetActive(false);
            LayoutRebuilder.ForceRebuildLayoutImmediate(bar);
        }

        /* ================== API ================== */
        public void Begin(float greenFrac)
        {
            StopAllCoroutines();
            LayoutRebuilder.ForceRebuildLayoutImmediate(bar);

            barHalfWidth = bar.rect.width * 0.5f;
            greenFrac = Mathf.Clamp(greenFrac, 0.05f, 0.8f);
            zoneHalfWidth = Mathf.Max(minZoneWidthPx, bar.rect.width * 0.5f * greenFrac);
            zoneCentre = 0f;
            elapsed = 0f;
            NormalizedError = 0f;

            ApplyZoneVisual();
            SetPointer(0f);
            StartCoroutine(FadeIn());
        }

        public void End() { StopAllCoroutines(); StartCoroutine(FadeOut()); }

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
            cg.alpha = 0f; gameObject.SetActive(true);
            while (cg.alpha < 1f) { cg.alpha += Time.deltaTime * 4f; yield return null; }
            cg.alpha = 1f;
        }
        IEnumerator FadeOut()
        {
            while (cg.alpha > 0f) { cg.alpha -= Time.deltaTime * 4f; yield return null; }
            cg.alpha = 0f; gameObject.SetActive(false);
        }
    }
}
