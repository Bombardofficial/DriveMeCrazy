using System.Collections;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class NewDriverBanner : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Egy TMP text ami tartalmazza az egész bannert (két sor).")]
    [SerializeField] private TextMeshProUGUI bannerText;

    [Tooltip("CanvasGroup a fade-hez. Ha üres, megpróbálja a bannerText-en/parenten megkeresni.")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Text Content")]
    [SerializeField] private string line1 = "New Driver:";
    [SerializeField] private string line2Prefix = "Player ";

    [Header("Timings")]
    [SerializeField] private float fadeInDuration = 0.18f;
    [SerializeField] private float popDuration = 0.22f;
    [SerializeField] private float holdDuration = 1.4f;
    [SerializeField] private float fadeOutDuration = 0.35f;

    [Header("Pop / Funky")]
    [SerializeField] private float startScale = 0.85f;
    [SerializeField] private float overshootScale = 1.12f;
    [SerializeField] private float endScale = 1.0f;

    [Header("Audio (optional)")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip newDriverClip;
    [Range(0f, 1f)]
    [SerializeField] private float sfxVolume = 1f;

    [Header("Optional: tint text by driver color")]
    [SerializeField] private bool tintByDriverColor = true;

    private Coroutine _routine;
    private RectTransform _rt;

    void Awake()
    {
        if (!bannerText)
        {
            Debug.LogError("[NewDriverBanner] bannerText nincs beállítva.", this);
            enabled = false;
            return;
        }

        if (!canvasGroup)
        {
            canvasGroup = bannerText.GetComponent<CanvasGroup>();
            if (!canvasGroup) canvasGroup = bannerText.GetComponentInParent<CanvasGroup>();
        }

        if (!canvasGroup)
        {
            // Biztosíték: ha nincs, rakunk a bannerText-re.
            canvasGroup = bannerText.gameObject.AddComponent<CanvasGroup>();
        }

        _rt = bannerText.rectTransform;

        // start hidden
        canvasGroup.alpha = 0f;
        bannerText.gameObject.SetActive(false);
    }

    void OnEnable()
    {
        PlayerManager.OnDriverChanged += HandleDriverChanged;

        // Ha már létezik driver (pl. quickstart / scene reload), akkor egyszer mutassuk
        if (PlayerManager.Instance != null && PlayerManager.Instance.CurrentDriver != null)
        {
            ShowForDriver(PlayerManager.Instance.CurrentDriver);
        }
    }

    void OnDisable()
    {
        PlayerManager.OnDriverChanged -= HandleDriverChanged;
    }

    private void HandleDriverChanged(Passenger oldDriver, Passenger newDriver)
    {
        if (newDriver == null) return;

        // ha ugyanaz maradna (edge-case), ne spammelje
        if (oldDriver == newDriver) return;
        ShowForDriver(newDriver);
    }

    private void ShowForDriver(Passenger driver)
    {
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(ShowRoutine(driver));
    }

    private IEnumerator ShowRoutine(Passenger driver)
    {
        bannerText.gameObject.SetActive(true);

        // Text build (két sor)
        int num = driver.PlayerNumber;
        bannerText.text = $"{line1}\n{line2Prefix}{num}";

        // Szín opcionálisan a PlayerManagerbõl
        if (tintByDriverColor && PlayerManager.Instance != null)
        {
            bannerText.color = PlayerManager.Instance.GetColorForPlayerNumber(num);
        }

        // SFX
        if (sfxSource && newDriverClip)
            sfxSource.PlayOneShot(newDriverClip, sfxVolume);

        // Reset anim state
        canvasGroup.alpha = 0f;
        _rt.localScale = Vector3.one * startScale;

        // Fade-in + pop (párhuzamosan)
        float t = 0f;

        // 1) Fade in
        while (t < fadeInDuration)
        {
            t += Time.deltaTime;
            canvasGroup.alpha = Mathf.Clamp01(t / fadeInDuration);
            yield return null;
        }
        canvasGroup.alpha = 1f;

        // 2) Pop overshoot (ease out)
        t = 0f;
        while (t < popDuration)
        {
            t += Time.deltaTime;
            float a = t / popDuration;
            float eased = 1f - Mathf.Pow(1f - a, 3f); // cubic out
            _rt.localScale = Vector3.one * Mathf.Lerp(startScale, overshootScale, eased);
            yield return null;
        }

        // 3) Settle back (gyors, kicsi)
        float settleDur = 0.10f;
        t = 0f;
        float from = _rt.localScale.x;
        while (t < settleDur)
        {
            t += Time.deltaTime;
            float a = t / settleDur;
            _rt.localScale = Vector3.one * Mathf.Lerp(from, endScale, a);
            yield return null;
        }
        _rt.localScale = Vector3.one * endScale;

        // Hold
        yield return new WaitForSeconds(holdDuration);

        // Fade out
        t = 0f;
        float startA = canvasGroup.alpha;
        while (t < fadeOutDuration)
        {
            t += Time.deltaTime;
            float a = t / fadeOutDuration;
            canvasGroup.alpha = Mathf.Lerp(startA, 0f, a);
            yield return null;
        }
        canvasGroup.alpha = 0f;

        bannerText.gameObject.SetActive(false);
        _routine = null;
    }
}
