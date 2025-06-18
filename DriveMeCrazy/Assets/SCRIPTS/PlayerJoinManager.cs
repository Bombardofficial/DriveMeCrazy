using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using System.Collections;
public class PlayerJoinManager : MonoBehaviour
{
    /* ??????????????? UI ??????????????? */
    [Header("UI")]
    [SerializeField] Image[] seatIcons;     // size 4
    [SerializeField] TextMeshProUGUI[] seatTexts;     // size 4
    [SerializeField] GameObject lobbyPanel;
    [SerializeField] Color freeCol = Color.white;
    [SerializeField] Color takenCol = new(0.2f, 1f, 0.2f);

    /* ????? Lobby visuals & car ????? */
    [Header("Lobby Visuals")]
    [SerializeField] GameObject[] playerDummies;  // 4
    [SerializeField] ArcadeVP.ArcadeVehicleController car;
    [SerializeField] CarSeatManager seatMgr;

    /* ????? Prefabs / Options ????? */
    [Header("Prefabs & Options")]
    [SerializeField] GameObject passengerPrefab;
    [SerializeField] bool quickStartSingleDriver = false;
    [SerializeField] GameObject driverPrefabOverride;

    /* ????? Countdown & SFX ????? */
    [Header("Countdown & SFX")]
    [SerializeField] float lobbyCountdownSeconds = 10f;
    [SerializeField] TextMeshProUGUI countdownText;
    [SerializeField] AudioSource countdownAudio;
    [SerializeField] AudioSource joinAudio;            // plays once per join

    /* ????? Intro camera ????? */
    [Header("Intro Camera Animation")]
    [SerializeField] Animator cameraAnimator;          // on intro cam
    [SerializeField] AnimationClip introClip;               // exact clip
    [SerializeField] string cameraAnimTrigger = "Play";
    [SerializeField] Animator lobbyAnimator;          // on intro cam
    [SerializeField] string lobbyAnimTrigger = "LobbyStart";
    /* ????? Canvas fades ????? */
    [Header("Canvas Fades")]
    [SerializeField] CanvasGroup fadeOutCanvas;
    [SerializeField] CanvasGroup gameplayHUD;
    [SerializeField] float fadeOutDuration = 1.25f;
    [SerializeField] float hudFadeDuration = 2f;

    /* ????? Music duck / rise ????? */
    [Header("Background Music")]
    [SerializeField] AudioSource carenginesound;                // bgm to fade in

    /* ????? Camera swap ????? */
    [Header("Camera Swap")]
    [SerializeField] Camera introCamera;                 // physical cam
    [SerializeField] GameObject gameplayCamera;              // v-cam root or cam

    [SerializeField] private TextMeshProUGUI transitionText;
    [SerializeField] private TextMeshProUGUI countdownGametext;
    [SerializeField] private AudioReverbFilter musicReverb;

    [SerializeField] AudioSource lobbymusic;
    [SerializeField] AudioSource countdownGameAudio;
    [SerializeField] AudioClip gocountdown;
    [SerializeField] AudioClip first3countdown;

    [Header("Gameplay Music")]
    [SerializeField] private AudioSource gameplayMusic;          // drag your gameplay AudioSource
    private float gameplayMusicTargetVol;
    private AudioLowPassFilter gameplayLPF;
    private AudioReverbFilter gameplayReverb;

    public static bool IsRaceStarted { get; private set; }

    /* ??? Internals ??? */
    int joinCount;
    bool counting;
    float timeLeft;
    bool raceStarted;
    float musicTargetVol;
    CarDriverInput driverInput;

    /* ??????????????????????????????????? */
    #region SETUP
    void Start()
    {
        if (!seatMgr || playerDummies.Length < 4 || !introClip)
        {
            Debug.LogError("[PlayerJoinManager] Missing references."); enabled = false; return;
        }
        raceStarted = false;
        IsRaceStarted = false;

        gameplayLPF = gameplayMusic.GetComponent<AudioLowPassFilter>();
        gameplayReverb = gameplayMusic.GetComponent<AudioReverbFilter>();

        gameplayMusicTargetVol = gameplayMusic.volume;     // remember designer volume
        if (quickStartSingleDriver)
        {
            /* 1.   Spawn driver instantly (enables car & HUD) */
            SpawnDriverImmediately();          // also sets raceStarted = true

            /* 2.   Hard-kill anything lobby-related */
            if (lobbymusic) lobbymusic.Stop();
            if (countdownAudio) countdownAudio.Stop();
            if (countdownGameAudio) countdownGameAudio.Stop();

            if (transitionText) transitionText.gameObject.SetActive(false);
            if (countdownGametext) countdownGametext.gameObject.SetActive(false);
            if (countdownText) countdownText.gameObject.SetActive(false);
            if (lobbyPanel) lobbyPanel.SetActive(false);
            foreach (var d in playerDummies) if (d) d.SetActive(false);

            if (fadeOutCanvas) fadeOutCanvas.alpha = 0f;      // no black splash
            if (introCamera) introCamera.gameObject.SetActive(false);  // kill lobby cam
            if (gameplayCamera) gameplayCamera.SetActive(true);

            /* 3.   Make sure engine sound is up and running */
            if (carenginesound)
            {
                if (!carenginesound.isPlaying) carenginesound.Play();
                carenginesound.volume = 1f;     // or any “normal” value you prefer
            }

            if (gameplayMusic)
            {
                if (!gameplayMusic.isPlaying) gameplayMusic.Play();
                gameplayMusic.volume = gameplayMusicTargetVol;
                if (gameplayLPF) gameplayLPF.cutoffFrequency = 22000f;
                if (gameplayReverb)
                {
                    gameplayReverb.dryLevel = 0f;
                    gameplayReverb.enabled = false;
                }
            }

            /* 4.   Skip rest of setup completely */
            return;
        }
        else
        {
            foreach (var d in playerDummies) if (d) d.SetActive(false);

            if (gameplayHUD) gameplayHUD.alpha = 0f;
            if (fadeOutCanvas) fadeOutCanvas.alpha = 1f;

            if (gameplayCamera) gameplayCamera.SetActive(false);
            transitionText.gameObject.SetActive(false);
            countdownGametext.gameObject.SetActive(false);
            /* store & mute music */
            if (carenginesound)
            {
                musicTargetVol = carenginesound.volume;
                carenginesound.volume = 0f;
            }
            if (lobbymusic)
            {
                musicTargetVol = lobbymusic.volume;
                lobbymusic.Play(); // ? Play music here
            }

            if (gameplayMusic)
            {
                gameplayMusic.volume = 0f;                       // start silent

                if (gameplayLPF) gameplayLPF.cutoffFrequency = 2000f;   // muffled
                if (gameplayReverb) gameplayReverb.dryLevel = -10000f; // fully wet
            }

            var lobbyLPF = lobbymusic?.GetComponent<AudioLowPassFilter>();
            if (lobbyLPF) lobbyLPF.cutoffFrequency = 3000;
            lobbyAnimator.ResetTrigger(lobbyAnimTrigger);
            lobbyAnimator.SetTrigger(lobbyAnimTrigger);

            musicReverb.dryLevel = 0;



            car.enabled = false;
            PlayerInputManager.instance.playerJoinedEvent.AddListener(OnPlayerJoined);

            if (countdownText) countdownText.text = "WAITING FOR DRIVER…";
            RefreshUI();
        }
        
    }
    #endregion

    void Update()
    {
        if (!counting || raceStarted) return;

        timeLeft -= Time.deltaTime;
        if (countdownText) countdownText.text = $"RACE STARTS IN: {Mathf.CeilToInt(timeLeft)}";

        if (timeLeft <= 0f)
        {
            counting = false;
            StartCoroutine(RaceSequence());
        }
    }

    /* ??????????? Player Joined ??????????? */
    void OnPlayerJoined(PlayerInput pi)
    {
        if (lobbymusic)
        {
            lobbymusic.Stop(); // ? Play music here
        }
        int seatIdx = joinCount;
        Transform seat = seatMgr.GetSeat(seatIdx);

        pi.transform.SetPositionAndRotation(seat.position, seat.rotation);
        pi.transform.SetParent(seat, true);

        var passenger = pi.GetComponent<Passenger>();
        PlayerManager.Instance?.RegisterPassenger(passenger, seatIdx);

        if (seatIdx == 0)
        {
            driverInput = pi.GetComponent<CarDriverInput>();
            driverInput.SetVehicle(car);
            driverInput.enabled = false;
            PlayerInputManager.instance.playerPrefab = passengerPrefab;
        }

        if (seatIdx < playerDummies.Length && playerDummies[seatIdx])
            playerDummies[seatIdx].SetActive(true);

        joinCount++;
        RefreshUI();

        /* restart countdown & jingle */
        timeLeft = lobbyCountdownSeconds;
        counting = true;
        if (countdownAudio) { countdownAudio.Stop(); countdownAudio.Play(); }

        /* play join SFX */
        if (joinAudio) joinAudio.PlayOneShot(joinAudio.clip);
    }

    /* ??????????? Race Sequence ??????????? */
    System.Collections.IEnumerator RaceSequence()
    {
        countdownText?.gameObject.SetActive(false);
        PlayerInputManager.instance.DisableJoining();

        /* 1) fade out lobby splash */
        yield return FadeCanvas(fadeOutCanvas, 1f, 0f, fadeOutDuration);

        /* 2) trigger intro */
        cameraAnimator.ResetTrigger(cameraAnimTrigger);
        cameraAnimator.SetTrigger(cameraAnimTrigger);
        StartCoroutine(ShowTransitionTextDelayed(3f));
        StartCoroutine(ShowCountdownText(introClip.length - 3f));
        if (lobbymusic)
        {
            musicTargetVol = lobbymusic.volume;
            lobbymusic.Play(); // ? Play music here
        }
        float clipLen = introClip.length / cameraAnimator.speed;
        float hudDelay = Mathf.Max(0f, clipLen - hudFadeDuration);

        if (musicReverb)
            StartCoroutine(FadeReverbDryLevel(musicReverb, 0f, -10000f, 4f, clipLen - 4f));

        // Fade out music in last 2 seconds
        if (lobbymusic)
        {
            var lpf = lobbymusic.GetComponent<AudioLowPassFilter>();
            lpf.cutoffFrequency = 22000;
            StartCoroutine(FadeAudio(lobbymusic, musicTargetVol, 0f, 2f, clipLen - 1f));
            if (lpf) StartCoroutine(FadeLowpassCutoff(lpf, 22000f, 2000f, 2f, clipLen - 2f));
        }
        if (gameplayMusic)
        {
            gameplayMusic.Stop();
            gameplayMusic.time = 0f;
            gameplayMusic.Play();
            float musicInDelay = Mathf.Max(0f, clipLen - 2f);       // begin 2 s before end
            StartCoroutine(FadeAudio(gameplayMusic, 0f, gameplayMusicTargetVol, 2f, musicInDelay));

            if (gameplayLPF) StartCoroutine(FadeLowpassCutoff(gameplayLPF, 2000f, 22000f, 2f, musicInDelay));
            if (gameplayReverb) StartCoroutine(FadeReverbDryLevel(gameplayReverb, -10000f, 0f, 2f, musicInDelay));
        }

        /* 3) HUD + music fade in sync */
        StartCoroutine(FadeCanvas(gameplayHUD, 0f, 1f, hudFadeDuration, hudDelay));
        if (carenginesound)
            StartCoroutine(FadeAudio(carenginesound, 0f, musicTargetVol, hudFadeDuration, hudDelay));

        /* 4) wait until the Animator state finishes exactly */
        yield return WaitForClipToEnd(cameraAnimator, introClip.name);

        /* 5) clean up & swap cameras */
        lobbyPanel.SetActive(false);
        if (gameplayCamera) gameplayCamera.SetActive(true);
        if (introCamera) introCamera.gameObject.SetActive(false);

        /* 6) go! */
        car.enabled = true;
        if (driverInput) driverInput.enabled = true;
        raceStarted = true;
        IsRaceStarted = true;
    }
    IEnumerator FadeLowpassCutoff(AudioLowPassFilter lpf, float from, float to, float dur, float delay = 0f)
    {
        if (!lpf) yield break;
        if (delay > 0f) yield return new WaitForSeconds(delay);

        float t = 0f;
        lpf.cutoffFrequency = from;

        while (t < dur)
        {
            t += Time.deltaTime;
            lpf.cutoffFrequency = Mathf.Lerp(from, to, t / dur);
            yield return null;
        }

        lpf.cutoffFrequency = to;
    }

    IEnumerator FadeReverbDryLevel(AudioReverbFilter reverb, float from, float to, float dur, float delay = 0f)
    {
        if (!reverb) yield break;
        if (delay > 0f) yield return new WaitForSeconds(delay);

        float t = 0f;
        reverb.dryLevel = from;

        while (t < dur)
        {
            t += Time.deltaTime;
            reverb.dryLevel = Mathf.Lerp(from, to, t / dur);
            yield return null;
        }

        reverb.dryLevel = to;
        if (reverb == gameplayReverb && Mathf.Approximately(to, 0f))
        {
            reverb.enabled = false;
        }
    }


    IEnumerator ShowTransitionTextDelayed(float delay)
    {
        if (!transitionText) yield break;

        Color color = transitionText.color;
        color.a = 0f;
        transitionText.color = color;
        transitionText.gameObject.SetActive(true);

        yield return new WaitForSeconds(delay);

        // Fade in
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime;
            color.a = Mathf.Lerp(0f, 1f, t);
            transitionText.color = color;
            yield return null;
        }

        yield return new WaitForSeconds(3f);

        // Fade out
        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime;
            color.a = Mathf.Lerp(1f, 0f, t);
            transitionText.color = color;
            yield return null;
        }

        transitionText.gameObject.SetActive(false);
    }

    /* ????????? Helpers ????????? */
    static System.Collections.IEnumerator WaitForClipToEnd(Animator anim, string stateName)
    {
        /* wait until we actually enter the desired state */
        while (!anim.GetCurrentAnimatorStateInfo(0).IsName(stateName))
            yield return null;

        /* then wait until it reaches the end (normalizedTime ? 1) */
        while (anim.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f ||
               anim.IsInTransition(0))
            yield return null;
    }

    System.Collections.IEnumerator FadeCanvas(CanvasGroup cg, float from, float to,
                                              float dur, float delay = 0f)
    {
        if (!cg) yield break;
        if (delay > 0f) yield return new WaitForSeconds(delay);

        float t = 0f; cg.alpha = from;
        while (t < dur) { t += Time.deltaTime; cg.alpha = Mathf.Lerp(from, to, t / dur); yield return null; }
        cg.alpha = to;
    }

    System.Collections.IEnumerator FadeAudio(AudioSource src, float from,
                                             float to, float dur, float delay = 0f)
    {
        if (!src) yield break;
        if (delay > 0f) yield return new WaitForSeconds(delay);

        float t = 0f; src.volume = from;
        while (t < dur) { t += Time.deltaTime; src.volume = Mathf.Lerp(from, to, t / dur); yield return null; }
        src.volume = to;
    }

    IEnumerator ShowCountdownText(float startDelay)
    {
        yield return new WaitForSeconds(startDelay);

        string[] countdownTexts = { "3", "2", "1", "Go Crazy!" };
        float[] fontSizes = { 300f, 300f, 300f, 220f };

        float pulseDuration = 0.4f;
        float pauseBetween = 0.2f;

        for (int i = 0; i < countdownTexts.Length; i++)
        {
            string currentText = countdownTexts[i];
            float currentFontSize = fontSizes[i];

            countdownGametext.text = currentText;
            countdownGametext.fontSize = currentFontSize;
            countdownGametext.fontStyle = (i == 3) ? FontStyles.Italic : FontStyles.Normal;

            countdownGametext.color = new Color(1f, 1f, 1f, 1f);
            countdownGametext.transform.localScale = Vector3.one * 0.5f;
            countdownGametext.gameObject.SetActive(true);

            // Play correct sound
            if (countdownGameAudio)
            {
                if (i < 3)
                    countdownGameAudio.PlayOneShot(first3countdown);
                else
                    countdownGameAudio.PlayOneShot(gocountdown);
            }

            // Pulse animation
            float elapsed = 0f;
            Vector3 targetScale = Vector3.one;
            while (elapsed < pulseDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / pulseDuration;
                countdownGametext.transform.localScale = Vector3.Lerp(Vector3.one * 0.5f, targetScale, t);
                yield return null;
            }

            // Hold
            yield return new WaitForSeconds(pauseBetween);

            // Fade out
            float fadeTime = 0.3f;
            elapsed = 0f;
            Color color = countdownGametext.color;
            while (elapsed < fadeTime)
            {
                elapsed += Time.deltaTime;
                color.a = Mathf.Lerp(1f, 0f, elapsed / fadeTime);
                countdownGametext.color = color;
                yield return null;
            }

            countdownGametext.gameObject.SetActive(false);
        }
    }




    void SpawnDriverImmediately()
    {
        GameObject prefab = driverPrefabOverride
                          ?? PlayerInputManager.instance?.playerPrefab;
        if (!prefab) { Debug.LogError("[Quick-Start] No driver prefab set!"); return; }

        /* 1. instantiate at seat 0 ------------------------------------------------*/
        const int seatIndex = 0;
        Transform seat = seatMgr.GetSeat(seatIndex);

        PlayerInput pi = PlayerInput.Instantiate(prefab,
                                                 playerIndex: 0,
                                                 controlScheme: null,
                                                 pairWithDevice: null);

        pi.transform.SetPositionAndRotation(seat.position, seat.rotation);
        pi.transform.SetParent(seat, true);

        /* 2. register with PlayerManager ---------------------------------------- */
        var passenger = pi.GetComponent<Passenger>();
        if (PlayerManager.Instance)       // safety in case the root is disabled
            PlayerManager.Instance.RegisterPassenger(passenger, seatIndex);

        /* 3. wire up driving ----------------------------------------------------- */
        driverInput = pi.GetComponent<CarDriverInput>();
        driverInput.SetVehicle(car);
        driverInput.enabled = true;
        car.enabled = true;

        /* 4. housekeeping -------------------------------------------------------- */
        joinCount = 1;
        raceStarted = true;
        lobbyPanel.SetActive(false);
        PlayerInputManager.instance?.DisableJoining();
        if (gameplayHUD) gameplayHUD.alpha = 1f;
    }

    void OnDestroy()
    {
        var pim = PlayerInputManager.instance;
        if (pim) pim.playerJoinedEvent.RemoveListener(OnPlayerJoined);
    }


    void RefreshUI()
    {
        int maxSeats = seatMgr ? seatMgr.SeatCount : seatIcons.Length;

        for (int i = 0; i < seatIcons.Length; ++i)
        {
            bool taken = i < joinCount;
            bool exists = i < maxSeats;

            seatIcons[i].gameObject.SetActive(exists);
            if (!exists) continue;

            seatIcons[i].color = taken ? takenCol : freeCol;
            seatTexts[i].text = taken ? $"PLAYER {i + 1}" : "PRESS GAS TO JOIN";
        }

        if (countdownText && !counting)
            countdownText.text = "WAITING FOR DRIVER...";
    }

    /* ????????????? called by the Start-Race button ????????????? */
    public void StartRace()
    {
        if (raceStarted || joinCount == 0) return;
        raceStarted = true;

        lobbyPanel.SetActive(false);
        countdownText.gameObject.SetActive(false);   // hide timer
        PlayerInputManager.instance.DisableJoining();

        car.enabled = true;
        if (driverInput) driverInput.enabled = true;
    }
}