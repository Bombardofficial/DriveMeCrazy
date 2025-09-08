using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;

namespace ArcadeVP
{
    public enum MiniGameOutcome { Fail, Pass, Perfect }

    public class SpeedZoneBalanceUI : MonoBehaviour
    {
        [Header("References")]
        public RectTransform bar;          // background / white panel
        public RectTransform greenZone;
        public RectTransform pointer;

        [Header("Outcome Label (optional)")]
        public TextMeshProUGUI outcomeText;   // place this where you want in the layout
        public string perfectText = "PERFECT!";
        public string passText = "PASS";
        public string failText = "FAIL";

        public bool IsVisible => cg && cg.alpha > 0.01f;
        public bool IsFullyVisible => cg && gameObject.activeInHierarchy && cg.alpha >= 0.99f;

        [Header("Dynamic Zone Behaviour")]
        [Tooltip("Peak fraction of bar half-width used for oscillation")] public float oscillationAmplitude = 0.30f;
        [Tooltip("Oscillation frequency in Hz")] public float oscillationFreq = 0.45f;
        [Tooltip("Zone shrink rate in frac/sec")] public float shrinkRate = 0.08f;
        [Tooltip("Absolute minimum green-zone width in px")] public float minZoneWidthPx = 40f;

        [Header("Background Jiggle")]
        public bool jiggleEnabled = true;
        [Range(0f, 30f)] public float jiggleAmplitudePx = 14f;
        [Range(0.1f, 30f)] public float jiggleFreqHz = 7.5f;
        [Range(0f, 10f)] public float jiggleRotDegrees = 2.0f;

        [Header("Fade")]
        public float fadeInSpeed = 12f;
        public float fadeOutSpeed = 12f;

        [Header("SFX (optional)")]
        public AudioSource sfx;
        public AudioClip readySfx;
        public AudioClip passSfx;
        public AudioClip perfectSfx;
        public AudioClip failSfx;

        [Header("Party pulses")]
        public float readyScale = 1.10f;
        public float readyPulseTime = 0.18f;
        public float outcomePulseScale = 1.18f;
        public float outcomePulseTime = 0.28f;

        float barHalfWidth;
        float zoneHalfWidth;    // px
        float zoneCentre;       // px (±barHalfWidth)
        float elapsed;

        CanvasGroup cg;

        // baselines to avoid “scale creep”
        Vector2 baseBarPos;
        Quaternion baseBarRot;
        Vector3 baseBarScale = Vector3.one;
        Vector3 baseGreenScale = Vector3.one;
        Vector3 basePointerScale = Vector3.one;
        Vector3 baseOutcomeScale = Vector3.one;
        Vector2 baseOutcomePos;
        Color baseGreenColor = Color.white;

        public float NormalizedError { get; private set; }  // 0=center, 1=edge

        public void ClearOutcomeImmediate()
        {
            if (!outcomeText) return;
            outcomeText.text = string.Empty;
            outcomeText.gameObject.SetActive(false);
        }

        void Awake()
        {
            cg = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0f; // start hidden

            if (bar)
            {
                baseBarPos = bar.anchoredPosition;
                baseBarRot = bar.localRotation;
                baseBarScale = bar.localScale;
            }
            if (greenZone)
            {
                baseGreenScale = greenZone.localScale;
                var img = greenZone.GetComponent<Image>();
                if (img) baseGreenColor = img.color;
            }
            if (pointer) basePointerScale = pointer.localScale;
            if (outcomeText)
            {
                outcomeText.gameObject.SetActive(false);
                outcomeText.text = string.Empty;   // NEW
            }
        }

        /* ================== API ================== */
        public void Begin(float greenFrac)
        {
            StopAllCoroutines();

            // hard reset visuals at the very start
            ResetVisualState();

            LayoutRebuilder.ForceRebuildLayoutImmediate(bar);

            barHalfWidth = bar.rect.width * 0.5f;
            greenFrac = Mathf.Clamp(greenFrac, 0.05f, 0.8f);
            zoneHalfWidth = Mathf.Max(minZoneWidthPx, bar.rect.width * 0.5f * greenFrac);
            zoneCentre = 0f;
            elapsed = 0f;
            NormalizedError = 0f;

            ApplyZoneVisual();
            SetPointer(0f);

            if (!gameObject.activeSelf) gameObject.SetActive(true);
            ClearOutcomeImmediate();

            cg.alpha = 0f;
            StartCoroutine(FadeIn());
        }

        public void End()
        {
            StopAllCoroutines();
            ClearOutcomeImmediate();
            if (!gameObject.activeInHierarchy)
            {
                if (cg == null) cg = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
                cg.alpha = 0f;
                ResetVisualState();   // ensure no leftover scale if we ended early
                return;
            }
            StartCoroutine(FadeOut());
        }

        public void Tick(float dt)
        {
            elapsed += dt;

            // Zone motion/shrink
            zoneCentre = Mathf.Sin(elapsed * oscillationFreq * Mathf.PI * 2f) * oscillationAmplitude * barHalfWidth;
            zoneHalfWidth = Mathf.Max(minZoneWidthPx, zoneHalfWidth - shrinkRate * bar.rect.width * dt);
            ApplyZoneVisual();
            RecomputeError();

            // background jiggle (bar only) – reset on fade-out
            if (jiggleEnabled && bar)
            {
                float err = Mathf.Clamp01(NormalizedError);
                float amp = jiggleAmplitudePx * Mathf.Lerp(0.3f, 1f, err);
                float w = jiggleFreqHz * 2f * Mathf.PI * Time.unscaledTime;

                float ox = Mathf.Sin(w) * amp;
                float oy = Mathf.Sin(w * 0.8f + 1.3f) * amp * 0.55f;
                float rot = Mathf.Sin(w * 1.12f + 0.4f) * jiggleRotDegrees;

                bar.anchoredPosition = baseBarPos + new Vector2(ox, oy);
                bar.localRotation = Quaternion.Euler(0f, 0f, rot);
            }
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

        /* ---------- party feedback ---------- */
        public void PlayReadyPulse()
        {
            if (readySfx && sfx) sfx.PlayOneShot(readySfx);
            if (!bar) return;
            StartCoroutine(ScalePulse(bar, readyScale, readyPulseTime));
        }

        public void PlayOutcome(MiniGameOutcome outcome)
        {
            // play once, right before fade
            AudioClip clip = outcome switch
            {
                MiniGameOutcome.Perfect => perfectSfx,
                MiniGameOutcome.Pass => passSfx,
                _ => failSfx
            };
            if (clip && sfx) sfx.PlayOneShot(clip);

            // label: scale bounce ONLY. Do not move from designer-chosen spot.
            if (outcomeText)
            {
                outcomeText.text = outcome switch
                {
                    MiniGameOutcome.Perfect => perfectText,
                    MiniGameOutcome.Pass => passText,
                    _ => failText
                };
                outcomeText.gameObject.SetActive(true);
                StartCoroutine(OutcomeBounce(outcomeText.rectTransform));
            }

            if (greenZone) StartCoroutine(ScalePulse(greenZone, outcomePulseScale, outcomePulseTime));

            if (outcome == MiniGameOutcome.Perfect) StartCoroutine(ZoneTint(new Color(1f, 0.95f, 0.2f), 0.25f));
            else if (outcome == MiniGameOutcome.Fail) StartCoroutine(ZoneTint(new Color(1f, .3f, .3f), 0.22f));
        }

        IEnumerator OutcomeBounce(RectTransform rt)
        {
            float dur = 0.65f;
            float t = 0f;
            float start = 1f;
            float peak = 1.25f;

            // keep original placement
            Vector3 baseScale = rt.localScale;

            while (t < dur)
            {
                // ease-out overshoot
                float k = t / 0.12f;
                float s = Mathf.Lerp(start, peak, Mathf.Clamp01(k));
                float wobble = Mathf.Exp(-4f * t) * Mathf.Sin((t * 2.5f + .2f) * Mathf.PI) * 0.3f;
                rt.localScale = baseScale * (s + wobble);

                t += Time.unscaledDeltaTime;
                yield return null;
            }
            rt.localScale = baseScale;
        }

        IEnumerator ScalePulse(RectTransform rt, float scale, float time)
        {
            Vector3 start = rt.localScale;
            Vector3 peak = start * scale;
            float t = 0f;
            while (t < time)
            {
                t += Time.unscaledDeltaTime;
                float half = time * 0.5f;
                if (t <= half)
                {
                    float k = t / half;
                    rt.localScale = Vector3.Lerp(start, peak, 1f - (1f - k) * (1f - k));
                }
                else
                {
                    float k = (t - half) / half;
                    rt.localScale = Vector3.Lerp(peak, start, k * k);
                }
                yield return null;
            }
            rt.localScale = start;
        }

        IEnumerator ZoneTint(Color c, float time)
        {
            var img = greenZone ? greenZone.GetComponent<Image>() : null;
            if (!img) yield break;
            var start = img.color;
            float t = 0f;
            while (t < time)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.PingPong(t * 2f, 1f);
                img.color = Color.Lerp(start, c, k);
                yield return null;
            }
            img.color = start;
        }

        /* ------------- helpers ------------- */
        void RecomputeError()
        {
            float px = Mathf.Abs(pointer.anchoredPosition.x - zoneCentre);
            NormalizedError = Mathf.InverseLerp(0f, zoneHalfWidth, px);
        }

        void ApplyZoneVisual()
        {
            // set by absolute width; localScale must be baseline (we reset it)
            float width = zoneHalfWidth * 2f;
            greenZone.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            greenZone.anchoredPosition = new Vector2(zoneCentre, 0f);
        }

        void ResetVisualState()
        {
            if (bar)
            {
                bar.anchoredPosition = baseBarPos;
                bar.localRotation = baseBarRot;
                bar.localScale = baseBarScale;
            }
            if (greenZone)
            {
                greenZone.localScale = baseGreenScale;
                var img = greenZone.GetComponent<Image>();
                if (img) img.color = baseGreenColor;
            }
            if (pointer) pointer.localScale = basePointerScale;
            if (outcomeText)
            {
                var rt = outcomeText.rectTransform;
                rt.localScale = baseOutcomeScale;
                rt.anchoredPosition = baseOutcomePos;    // respect your placement
                outcomeText.text = string.Empty;      // NEW
                outcomeText.gameObject.SetActive(false);
            }
        }

        IEnumerator FadeIn()
        {
            while (cg.alpha < 1f)
            {
                cg.alpha = Mathf.MoveTowards(cg.alpha, 1f, fadeInSpeed * Time.unscaledDeltaTime);
                yield return null;
            }
            cg.alpha = 1f;
        }

        IEnumerator FadeOut()
        {
            // settle background while fading
            if (bar)
            {
                bar.anchoredPosition = baseBarPos;
                bar.localRotation = baseBarRot;
            }

            while (cg.alpha > 0f)
            {
                cg.alpha = Mathf.MoveTowards(cg.alpha, 0f, fadeOutSpeed * Time.unscaledDeltaTime);
                yield return null;
            }
            cg.alpha = 0f;

            // prevent any leftover scale/tint from aborted coroutines
            ClearOutcomeImmediate();
            ResetVisualState();

            gameObject.SetActive(false);
        }
    }
}
