using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ArcadeVP
{
    public class SpeedLimitUI : MonoBehaviour
    {
        public static SpeedLimitUI Instance { get; private set; }

        [Header("Single Image UI")]
        [SerializeField] private Image signImage;          // 1 db Image a canvason
        [SerializeField] private CanvasGroup canvasGroup;  // fade-hez

        [Header("Fade")]
        [Min(0f)] public float fadeInSeconds = 0.12f;
        [Min(0f)] public float fadeOutSeconds = 0.12f;

        [Header("Pre-cue (optional)")]
        public AudioSource sfx;
        public AudioClip preCueBeep;
        public float preCueScale = 1.15f;

        int _current = -1;
        Coroutine _fadeCR;
        Coroutine _pulseCR;

        // index -> sprite (ezt tölti fel a SpeedLimitZone entry)
        readonly Dictionary<int, Sprite> _spriteByIndex = new();

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            if (!canvasGroup) canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
            if (!signImage) signImage = GetComponentInChildren<Image>(true);

            HideAllImmediate();
        }

        void HideAllImmediate()
        {
            _current = -1;

            if (signImage)
            {
                signImage.sprite = null;
                signImage.enabled = false;
            }

            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        // ---- THIS IS WHAT YOU WANT TO CALL FROM SpeedLimitZone ENTRY ----
        public void ShowSprite(int index, Sprite sprite)
        {
            if (sprite) _spriteByIndex[index] = sprite;
            Show(index);
        }

        // Backwards-compatible: if somebody calls Show(index), it will show the registered sprite (if any).
        public void Show(int index)
        {
            if (index == _current && canvasGroup.alpha > 0.99f) return;

            _current = index;

            // find sprite for this index (registered by zone)
            _spriteByIndex.TryGetValue(index, out var spr);

            if (signImage)
            {
                signImage.sprite = spr;
                signImage.enabled = (spr != null);
            }

            // if no sprite -> hide (so you instantly see it’s not configured)
            if (spr == null)
            {
                FadeTo(0f, fadeOutSeconds);
                return;
            }

            FadeTo(1f, fadeInSeconds);
        }

        public void Hide(int index)
        {
            if (index != _current) return;

            _current = -1;
            FadeTo(0f, fadeOutSeconds);
        }

        public void HideAll()
        {
            _current = -1;
            FadeTo(0f, fadeOutSeconds);
        }

        void FadeTo(float targetAlpha, float seconds)
        {
            if (_fadeCR != null) StopCoroutine(_fadeCR);
            _fadeCR = StartCoroutine(FadeRoutine(targetAlpha, seconds));
        }

        IEnumerator FadeRoutine(float target, float seconds)
        {
            float start = canvasGroup.alpha;

            if (seconds <= 0.0001f)
            {
                canvasGroup.alpha = target;
            }
            else
            {
                float t = 0f;
                while (t < seconds)
                {
                    t += Time.unscaledDeltaTime;
                    float k = Mathf.Clamp01(t / seconds);
                    canvasGroup.alpha = Mathf.Lerp(start, target, k);
                    yield return null;
                }
                canvasGroup.alpha = target;
            }

            if (canvasGroup.alpha <= 0.001f && signImage)
            {
                signImage.enabled = false;
                signImage.sprite = null;
            }

            _fadeCR = null;
        }

        public void FlashRed()
        {
            if (_current < 0) return;
            StartCoroutine(Blink());
        }

        IEnumerator Blink()
        {
            for (int i = 0; i < 2; ++i)
            {
                canvasGroup.alpha = 0f; yield return new WaitForSecondsRealtime(0.08f);
                canvasGroup.alpha = 1f; yield return new WaitForSecondsRealtime(0.08f);
            }
        }

        public void PlayPreCue(float duration = 0.3f)
        {
            if (_current < 0) return;
            if (!signImage || !signImage.enabled) return;

            if (_pulseCR != null) StopCoroutine(_pulseCR);
            _pulseCR = StartCoroutine(Pulse(duration));
        }

        IEnumerator Pulse(float dur)
        {
            if (preCueBeep && sfx) sfx.PlayOneShot(preCueBeep);

            var rt = signImage.transform as RectTransform;
            if (!rt) yield break;

            float t = 0f;
            Vector3 start = rt.localScale;
            Vector3 peak = start * preCueScale;

            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float half = dur * 0.5f;

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
            _pulseCR = null;
        }
    }
}
