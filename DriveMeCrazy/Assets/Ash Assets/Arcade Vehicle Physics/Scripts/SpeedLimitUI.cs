using UnityEngine;
using UnityEngine.UI;

namespace ArcadeVP
{
    public class SpeedLimitUI : MonoBehaviour
    {
        public Image img;                       // assign in Inspector
        [Range(0f, 1f)] public float fadeSpeed = 6f;

        Sprite current;

        void Awake() => SetVisible(false);

        public void Show(Sprite s)
        {
            img.enabled = s;              // turn Image on/off
            img.sprite = s;
        }

        void Update()                       // smooth alpha fade
        {
            if (!img) return;
            float target = current ? 1f : 0f;
            Color c = img.color;
            c.a = Mathf.MoveTowards(c.a, target, fadeSpeed * Time.deltaTime);
            img.color = c;
        }

        void SetVisible(bool v)
        {
            Color c = img.color;
            c.a = v ? 1f : 0f;
            img.color = c;
        }

        public void FlashRed()
        {
            if (!img) return;
            StopAllCoroutines();
            StartCoroutine(Blink());

            System.Collections.IEnumerator Blink()
            {
                Color normal = img.color;
                for (int i = 0; i < 2; i++)
                {
                    img.color = Color.red;
                    yield return new WaitForSeconds(0.1f);
                    img.color = normal;
                    yield return new WaitForSeconds(0.1f);
                }
            }
        }


#if UNITY_EDITOR
        void OnValidate()          // runs in edit?mode
        {
            if (img) img.sprite = current;   // forces the sprite every recomp
        }
#endif
    }
}
