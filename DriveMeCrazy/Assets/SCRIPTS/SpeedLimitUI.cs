using UnityEngine;

namespace ArcadeVP
{
    /// Simple enable / disable by index.
    public class SpeedLimitUI : MonoBehaviour
    {
        [Tooltip("One child GO per sign (order = index)")]
        public GameObject[] signObjects;

        int current = -1;

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
            // ensure we’re active before starting a coroutine
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

    }
}
