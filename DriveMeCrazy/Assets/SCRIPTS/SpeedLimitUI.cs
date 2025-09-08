using UnityEngine;

namespace ArcadeVP
{
    /// Simple enable / disable by index.
    public class SpeedLimitUI : MonoBehaviour
    {
        [Tooltip("One child GO per sign (order = index)")]
        public GameObject[] signObjects;

        [Header("Pre-cue (optional)")]
        [Tooltip("SFX source for pre-cue beep (optional)")]
        public AudioSource sfx;
        [Tooltip("Beep to play during pre-cue (optional)")]
        public AudioClip preCueBeep;
        [Tooltip("Sign scale during pre-cue pulse")]
        public float preCueScale = 1.15f;

        int current = -1;
        Coroutine _pulseCR;

        void Awake()
        {
            foreach (var go in signObjects) if (go) go.SetActive(false);
        }

        /// <param name="index">Supply -1 to hide all.</param>
        public void Show(int index)
        {
            if (index == current) return;

            foreach (var go in signObjects) if (go) go.SetActive(false);
            current = -1;

            if (index >= 0 && index < signObjects.Length && signObjects[index])
            {
                signObjects[index].SetActive(true);
                current = index;
            }
        }

        public void FlashRed()
        {
            if (current < 0) return;
            gameObject.SetActive(true);
            StartCoroutine(Blink());

            System.Collections.IEnumerator Blink()
            {
                var cg = gameObject.GetComponent<CanvasGroup>() ??
                         gameObject.AddComponent<CanvasGroup>();

                for (int i = 0; i < 2; ++i)
                {
                    cg.alpha = 0; yield return new WaitForSeconds(.1f);
                    cg.alpha = 1; yield return new WaitForSeconds(.1f);
                }
            }
        }

        /// Plays a short “pop” on the currently shown sign (if any) and an optional beep.
        public void PlayPreCue(float duration = 0.3f)
        {
            if (!isActiveAndEnabled || current < 0) return;
            if (_pulseCR != null) StopCoroutine(_pulseCR);
            _pulseCR = StartCoroutine(Pulse(duration));
        }

        System.Collections.IEnumerator Pulse(float dur)
        {
            if (preCueBeep && sfx) sfx.PlayOneShot(preCueBeep);

            var sign = signObjects[current];
            if (!sign) yield break;

            var t = 0f;
            var tr = sign.transform as RectTransform;
            var start = tr.localScale;
            var peak = start * preCueScale;

            // ease-out pop up, then settle
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float half = dur * 0.5f;
                if (t <= half)
                {
                    float k = t / half; // 0..1
                    tr.localScale = Vector3.Lerp(start, peak, 1f - (1f - k) * (1f - k));
                }
                else
                {
                    float k = (t - half) / half; // 0..1
                    tr.localScale = Vector3.Lerp(peak, start, k * k);
                }
                yield return null;
            }
            tr.localScale = start;
            _pulseCR = null;
        }
    }
}
