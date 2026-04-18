using ArcadeVP;
using System;
using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PlayerJoinManager : MonoBehaviour
{
    /* ?????????????????????????? TUTORIAL ?????????????????????????? */
    [Header("Tutorial")]
    [SerializeField] private GameTutorialController tutorialController;
    [SerializeField] private GameObject playerJoinCanvasRoot;

    /* ???????????????????????? CUSTOMIZATION ???????????????????????? */
    /*
    [Header("Customization")]
    [SerializeField] bool EnableCusomizationMenu = false;
    [SerializeField] private HatCustomizationUIManager hatUI;
    [SerializeField] private GameObject customizationCanvasRoot;

    [Header("Customization Camera Fly")]
    [SerializeField] private Camera lobbyCamera;               // assign Intro Camera here
    [SerializeField] private Transform customizationCamPose;   // assign CustomizationCamPose here
    [SerializeField] private Camera mainCamera;                // (optional) gameplay/main cam to disable
    [SerializeField] private float customizationFlyTime = 0.8f;
    [SerializeField] private Animator cameraAnimatorRev;          // animator that normally drives lobby camera

    private Vector3 lobbyCamStartPos;
    private Quaternion lobbyCamStartRot;
    private Coroutine camFlyRoutine;
    private bool customizationCamActive;
    */
    
    /* ?????????????????????????? INSPECTOR ?????????????????????????? */
    [Header("UI (Lobby)")]
    [SerializeField] Image[] seatIcons;          // size 4
    [SerializeField] TextMeshProUGUI[] seatTexts;
    [SerializeField] GameObject lobbyPanel;
    [SerializeField] Color freeCol = Color.white;
    [SerializeField] Color takenCol = new(0.2f, 1f, 0.2f);

    [Header("Lobby Visuals")]
    [SerializeField] GameObject[] playerDummies; // 4
    [SerializeField] ArcadeVP.ArcadeVehicleController car;
    [SerializeField] CarSeatManager seatMgr;

    [Header("Lobby Player Colors (Dummies)")]
    [SerializeField] bool tintLobbyDummies = true;

    [Tooltip("Optional override. If empty, uses PlayerManager.ForcedColorProperty. If that is also empty, auto-tries _BaseColor/_Color/_TintColor.")]
    [SerializeField] string lobbyForcedColorProperty = "";

    [Tooltip("Tried if no forced property is provided.")]
    [SerializeField] string[] lobbyColorPropertyCandidates = new[] { "_BaseColor", "_Color", "_TintColor" };

    [Tooltip("If true, seat icon color will use the same per-player color instead of takenCol.")]
    [SerializeField] bool tintSeatIconsWithPlayerColors = true;

    // --- runtime caches for dummies ---
    Renderer[][] _dummyRenderers;
    MaterialPropertyBlock[] _dummyMpbs;
    int[] _dummyColorPropIds; // -1 = not found

    [Header("Prefabs & Options")]
    [SerializeField] GameObject passengerPrefab;
    [SerializeField] bool quickStartSingleDriver = false;
    [SerializeField] GameObject driverPrefabOverride;

    [Header("Countdown & SFX")]
    [SerializeField] float lobbyCountdownSeconds = 10f;
    [SerializeField] TextMeshProUGUI countdownText;
    [SerializeField] AudioSource countdownAudio;
    [SerializeField] AudioSource joinAudio;

    [Header("Intro Camera Animation")]
    [SerializeField] Animator cameraAnimator;
    [SerializeField] AnimationClip introClip;
    [SerializeField] AnimationClip resultsClip;
    [SerializeField] string cameraAnimTrigger = "Play";
    [SerializeField] string cameraResultsAnimTrigger = "Results";
    [SerializeField] Animator lobbyAnimator;
    [SerializeField] string lobbyAnimTrigger = "LobbyStart";

    [Header("Canvas Fades")]
    [SerializeField] CanvasGroup fadeOutCanvas;
    [SerializeField] CanvasGroup gameplayHUD;
    [SerializeField] float fadeOutDuration = 1.25f;
    [SerializeField] float hudFadeDuration = 2f;

    [Header("Background Music")]
    [SerializeField] AudioSource carenginesound;

    [Header("Camera Swap")]
    [SerializeField] Camera introCamera;         // physical cam
    [SerializeField] GameObject gameplayCamera;  // virtual cam root

    [Header("Runtime Text")]
    [SerializeField] TextMeshProUGUI transitionText;
    [SerializeField] TextMeshProUGUI countdownGametext;

    [Header("Lobby Music Bus")]
    [SerializeField] AudioReverbFilter musicReverb;
    [SerializeField] AudioSource lobbyMusic;
    [SerializeField] AudioSource countdownGameAudio;
    [SerializeField] AudioClip gocountdown;
    [SerializeField] AudioClip first3countdown;

    [Header("Gameplay Music Bus")]
    [SerializeField] AudioSource gameplayMusic;
    private float gameplayMusicTargetVol;
    private AudioLowPassFilter gameplayLPF;
    private AudioReverbFilter gameplayReverb;

    /* ????????????? NEW  Lap / Finish / Result Fields ????????????? */
    [Header("Lap System")]
    [SerializeField] private TextMeshProUGUI lapsText;       // �Laps: 0/10�
    [SerializeField] private int lapsToFinish = 10;          // finish after X
    [Tooltip("OPTIONAL.  Leave empty if this script is placed ON the LapGate")]
    [SerializeField] private Collider lapGateTrigger;        // start/finish
    [Tooltip("Tag on the car root GameObject")]
    [SerializeField] private string playerCarTag = "PlayerCar";

    [Header("Results & End-Game UI")]
    [SerializeField] CanvasGroup resultsPanel;       // parent group
    [SerializeField] TextMeshProUGUI resultsText;    // tall TMP for list
    [SerializeField] TextMeshProUGUI winnerText;     // big banner
    [SerializeField] TextMeshProUGUI autoReturnText;   // �Returning in ��
    [SerializeField] Image blackFadeImage;          // full-screen image

    public CanvasGroup fadeintoLobby;
    public bool isDragRace;
    public AudioSource crowdroar;
    /* ?????????????????????? STATIC / INTERNALS ??????????????????? */
    public static bool IsRaceStarted { get; private set; }
    private int joinCount;
    private bool counting;
    private float timeLeft;
    private bool raceStarted;
    private float lobbyMusicTargetVol;
    float musicTargetVol;
    private CarDriverInput driverInput;

    private int currentLap = 0;
    private bool finalising = false;      // prevents multi-trigger
    private Vector2 startPos;
    public int CarLayer => car.gameObject.layer;

    /* ????????????????????????????? SETUP ?????????????????????????? */
    void Start()
    {
        if (!seatMgr || playerDummies.Length < 4 || !introClip)
        { Debug.LogError("[PlayerJoinManager] Missing references."); enabled = false; return; }

        // Cache filters
        gameplayLPF = gameplayMusic.GetComponent<AudioLowPassFilter>();
        gameplayReverb = gameplayMusic.GetComponent<AudioReverbFilter>();

        gameplayMusicTargetVol = gameplayMusic.volume;
        lobbyMusicTargetVol = lobbyMusic ? lobbyMusic.volume : 1f;

        if (introCamera == null) introCamera = introCamera;

        // Hook lap trigger (if this script isn�t placed on the gate)
        if (lapGateTrigger) lapGateTrigger.isTrigger = true;

        startPos = winnerText.rectTransform.anchoredPosition;

        CacheLobbyDummyTintData();
        ResetLobbyDummies();
        PrepareLobbyState();
    }

    IEnumerator AutoReturnCountdown(int seconds = 10)
    {
        // fade-in
        autoReturnText.alpha = 0;
        autoReturnText.gameObject.SetActive(true);
        for (float f = 0; f < 1f; f += Time.deltaTime)
        {
            autoReturnText.alpha = f;     // 1-sec fade
            yield return null;
        }
        autoReturnText.alpha = 1;

        // ticking
        for (int s = seconds; s >= 0; --s)
        {
            autoReturnText.text = $"Going back to main menu in {s}...";
            yield return new WaitForSeconds(1f);
        }

        // fade screen to black and load
        StartCoroutine(FadeAndLoad("MainMenu"));
    }

    IEnumerator DelayedLobbyTrigger()
    {
        yield return null; // wait 1 frame
        yield return StartCoroutine(FadeCanvas(fadeintoLobby, 1f, 0f, 2f));
        lobbyAnimator.ResetTrigger(lobbyAnimTrigger);
        lobbyAnimator.SetTrigger(lobbyAnimTrigger);
    }

    /* ????????????????? LOBBY INITIALISATION BRANCH ????????????????? */
    void PrepareLobbyState()
    {
        IsRaceStarted = raceStarted = false;
        currentLap = 0;
        UpdateLapUI();

        if (quickStartSingleDriver)
        {
            /* 1.   Spawn driver instantly (enables car & HUD) */
            SpawnDriverImmediately();          // now also sets IsRaceStarted

            /* 2.   Hard-kill anything lobby-related */
            if (lobbyMusic) lobbyMusic.Stop();
            if (countdownAudio) countdownAudio.Stop();
            if (countdownGameAudio) countdownGameAudio.Stop();

            if (transitionText) transitionText.gameObject.SetActive(false);
            if (countdownGametext) countdownGametext.gameObject.SetActive(false);
            if (countdownText) countdownText.gameObject.SetActive(false);
            if (lobbyPanel) lobbyPanel.SetActive(false);
            ResetLobbyDummies();

            if (fadeOutCanvas) fadeOutCanvas.alpha = 0f;
            if (resultsPanel) resultsPanel.alpha = 0f;
            if (introCamera) introCamera.gameObject.SetActive(false);
            if (gameplayCamera) gameplayCamera.SetActive(true);

            /* 3.   Make sure engine/gameplay audio is up */
            if (carenginesound)
            {
                if (!carenginesound.isPlaying) carenginesound.Play();
                carenginesound.volume = 1f;
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

            /* 4.   Bring gameplay systems online (spawners, speed-zone FX) */
            EnableGameplaySystems(); // <-- NEW

            /* 5.   Skip rest of setup completely */
            return;
        }
        else
        {
            StartCoroutine(DelayedLobbyTrigger());
            /* hide gameplay HUD & results */
            if (gameplayHUD) gameplayHUD.alpha = 0f;
            if (resultsPanel) resultsPanel.alpha = 0f;
            if (resultsPanel) resultsPanel.gameObject.SetActive(false);
            if (winnerText) winnerText.gameObject.SetActive(false);
            if (transitionText) transitionText.gameObject.SetActive(false);
            if (countdownGametext) countdownGametext.gameObject.SetActive(false);
            if (carenginesound)
            {
                musicTargetVol = carenginesound.volume;
                carenginesound.volume = 0f;
            }
            /* music buses */
            if (lobbyMusic)
            {
                lobbyMusic.volume = lobbyMusicTargetVol;
                lobbyMusic.Play();
                var lpf = lobbyMusic.GetComponent<AudioLowPassFilter>(); if (lpf) lpf.cutoffFrequency = 3000;
                var rev = lobbyMusic.GetComponent<AudioReverbFilter>(); if (rev) rev.dryLevel = 0;
            }
            if (gameplayMusic)
            {
                gameplayMusic.volume = 0f;
                if (gameplayLPF) gameplayLPF.cutoffFrequency = 2000;
                if (gameplayReverb) gameplayReverb.dryLevel = -10000;
            }

            if (fadeOutCanvas) fadeOutCanvas.alpha = 1f;
            if (introCamera) introCamera.gameObject.SetActive(true);
            if (gameplayCamera) gameplayCamera.SetActive(false);

            car.enabled = false;
            foreach (var d in playerDummies) if (d) d.SetActive(false);
            PlayerInputManager.instance.playerJoinedEvent.AddListener(OnPlayerJoined);
            PlayerInputManager.instance.EnableJoining();
            if (countdownText) countdownText.text = "WAITING FOR DRIVER�";
            RefreshUI();
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
        if (PlayerManager.Instance)
            PlayerManager.Instance.RegisterPassenger(passenger, seatIndex);

        /* 3. wire up driving ----------------------------------------------------- */
        driverInput = pi.GetComponent<CarDriverInput>();
        driverInput.SetVehicle(car);
        driverInput.enabled = true;
        car.enabled = true;

        /* 4. housekeeping -------------------------------------------------------- */
        joinCount = 1;
        raceStarted = true;
        IsRaceStarted = true;                // <-- NEW: static flag for other systems
        counting = false; timeLeft = 0f;
        if (lobbyPanel) lobbyPanel.SetActive(false);
        PlayerInputManager.instance?.DisableJoining();
        if (gameplayHUD) gameplayHUD.alpha = 1f;
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

            if (taken && tintSeatIconsWithPlayerColors && PlayerManager.Instance != null)
            {
                // In lobby, join order maps to PlayerNumber => i+1
                seatIcons[i].color = PlayerManager.Instance.GetColorForPlayerNumber(i + 1);
            }
            else
            {
                seatIcons[i].color = taken ? takenCol : freeCol;
            }
            seatTexts[i].text = taken ? $"PLAYER {i + 1}" : "PRESS GAS TO JOIN";
        }

        if (countdownText && !counting)
            countdownText.text = "WAITING FOR DRIVER...";
    }

    /* ????????????????????????? UPDATE ????????????????????????????? */
    void Update()
    {
        if (finalising) return;
        if (counting && !raceStarted)
        {
            timeLeft -= Time.deltaTime;
            if (countdownText)
                countdownText.text = $"RACE STARTS IN: {Mathf.CeilToInt(timeLeft)}";

            if (timeLeft <= 0f)
            {
                counting = false;
                StartCoroutine(RaceSequence());
            }
        }
    }

    /* ??????????????????????? LAP TRIGGER ?????????????????????????? */
    public void LapGateCrossed()
    {
        if (!raceStarted || finalising)
            return;

        currentLap++;
        UpdateLapUI();

        if (currentLap >= lapsToFinish || isDragRace)
            StartCoroutine(FinishRaceSequence());
    }

    /* ?????????????? PLAYER JOIN FLOW (unchanged) ??????????????? */
    void OnPlayerJoined(PlayerInput pi)
    {
        if (lobbyMusic) lobbyMusic.Stop();

        int seatIdx = joinCount;
        Transform seat = seatMgr.GetSeat(seatIdx);
        pi.transform.SetPositionAndRotation(seat.position, seat.rotation);
        pi.transform.SetParent(seat, true);

        //hatUI.RegisterPlayer(seatIdx, pi.gameObject);

        var passenger = pi.GetComponent<Passenger>();
        PlayerManager.Instance?.RegisterPassenger(passenger, seatIdx);

        if (seatIdx == 0) // driver
        {
            driverInput = pi.GetComponent<CarDriverInput>();
            driverInput.SetVehicle(car); driverInput.enabled = false;
            PlayerInputManager.instance.playerPrefab = passengerPrefab;
        }

        if (seatIdx < playerDummies.Length && playerDummies[seatIdx])
        {
            playerDummies[seatIdx].SetActive(true);

            // also equip hats on the lobby dummy you actually see
            /*if (hatUI != null && playerDummies[seatIdx] != null)
            {
                //hatUI.RegisterPlayer(seatIdx, playerDummies[seatIdx]);
                hatUI.RegisterDummy(seatIdx, playerDummies[seatIdx]);
            }*/

            // player identity = Passenger.PlayerNumber (NOT seat)
            if (tintLobbyDummies && passenger != null && PlayerManager.Instance != null)
            {
                Color c = PlayerManager.Instance.GetColorForPlayerNumber(passenger.PlayerNumber);
                ApplyColorToLobbyDummy(seatIdx, c);
            }
        }

        joinCount++;
        RefreshUI();

        timeLeft = lobbyCountdownSeconds;
        counting = true;
        countdownAudio?.Play();
        joinAudio?.PlayOneShot(joinAudio.clip);
    }

    /* ??????????????????? TUTORIAL FUNTIONS ???????????????????? */
    private PlayerInput[] GetJoinedPlayers()
    {
        // All player objects instantiated by PlayerInputManager have PlayerInput on them
        return FindObjectsOfType<PlayerInput>();
    }


    /* ??????????????????? RACE START COROUTINE ???????????????????? */
    IEnumerator RaceSequence()
    {
        countdownText?.gameObject.SetActive(false);
        PlayerInputManager.instance.DisableJoining();

        var players = GetJoinedPlayers();

        /// hide join UI
        if (playerJoinCanvasRoot) playerJoinCanvasRoot.SetActive(false);

        // --- CUSTOMIZATION FIRST ---
        /*if(EnableCusomizationMenu =! false)
        {
            if (hatUI != null)
            {
                // (hatUI can toggle its own canvas root; depends on your setup)
                yield return hatUI.RunCustomization(players, joinCount);
                hatUI.ApplySelectionsToAllRegisteredPlayers();
            }
            else
            {
                Debug.LogWarning("[PlayerJoinManager] hatUI is NULL (not assigned).");
            }
        }
        else
        {
            hatUI = null;
            Debug.LogWarning("Customization Menu disabled.");
        }*/



        // --- THEN TUTORIAL ---
        if (tutorialController != null)
        {
            yield return tutorialController.RunTutorial(players);
        }
        else
        {
            Debug.LogWarning("[PlayerJoinManager] tutorialController is NULL (not assigned in Inspector).");
        }

        SwitchAllPlayersToActionMap("Gameplay");

        foreach (var pi in players)
        {
            Debug.Log($"Player {pi.playerIndex} current map: {pi.currentActionMap?.name}");
        }

        yield return FadeCanvas(fadeOutCanvas, 1f, 0f, fadeOutDuration);

        /* intro cam anim */
        cameraAnimator.ResetTrigger(cameraAnimTrigger);
        cameraAnimator.SetTrigger(cameraAnimTrigger);

        StartCoroutine(ShowTransitionTextDelayed(3f));
        StartCoroutine(ShowCountdownText(introClip.length - 3f));

        float clipLen = introClip.length / cameraAnimator.speed;
        float hudDelay = Mathf.Max(0f, clipLen - hudFadeDuration);

        // music crossfade
        if (lobbyMusic)
        {
            lobbyMusic.Play();
            var lpf = lobbyMusic.GetComponent<AudioLowPassFilter>();
            StartCoroutine(FadeAudio(lobbyMusic, lobbyMusicTargetVol, 0f, 2f, clipLen - 1f));
            if (lpf) StartCoroutine(FadeLowpassCutoff(lpf, 3000, 2000, 2f, clipLen - 2f));
        }
        if (gameplayMusic)
        {
            gameplayMusic.Stop(); gameplayMusic.time = 0f; gameplayMusic.Play();
            StartCoroutine(FadeAudio(gameplayMusic, 0f, gameplayMusicTargetVol, 2f, clipLen - 2f));
            if (gameplayLPF) StartCoroutine(FadeLowpassCutoff(gameplayLPF, 2000, 22000, 2f, clipLen - 2f));
            if (gameplayReverb) StartCoroutine(FadeReverbDryLevel(gameplayReverb, -10000, 0, 2f, clipLen - 2f));
        }
        if (carenginesound)
            StartCoroutine(FadeAudio(carenginesound, 0f, musicTargetVol, hudFadeDuration, hudDelay));
        // HUD fade
        StartCoroutine(FadeCanvas(gameplayHUD, 0, 1, hudFadeDuration, hudDelay));

        PlayerManager.Instance.ReapplyPlayerColors();
        PlayerManager.Instance.InitSabotageHandlers();

        // Wait intro anim
        yield return WaitForClipToEnd(cameraAnimator, introClip.name);

        // Swap cameras
        lobbyPanel.SetActive(false);
        gameplayCamera.SetActive(true);
        introCamera.gameObject.SetActive(false);

        // Start driving
        car.enabled = true;
        driverInput.enabled = true;
        raceStarted = IsRaceStarted = true;

        // Start logging of game
        if (TelemetryLogger.Instance != null)
        {
            TelemetryLogger.Instance.StartLogging();
        }

        // Ensure systems are on in case they were off
        EnableGameplaySystems(); // <-- NEW safeguard
    }

    void DisableGameplaySystems()
    {
        /* Spawners */
        foreach (var om in FindObjectsOfType<ObstacleManager>())
            om.enabled = false;                 // coroutines stop automatically
        foreach (var cm in FindObjectsOfType<CollectableManager>())
            cm.enabled = false;

        /* Speed-zone post-processing */
        foreach (var fx in FindObjectsOfType<SpeedZoneFeedbackFX>())
            fx.enabled = false;                 // OnDisable resets effects
    }

    // ---------- NEW ----------
    void EnableGameplaySystems()
    {
        // Re-enable anything the finish sequence might have turned off
        foreach (var om in FindObjectsOfType<ObstacleManager>())
            om.enabled = true;
        foreach (var cm in FindObjectsOfType<CollectableManager>())
            cm.enabled = true;
        foreach (var fx in FindObjectsOfType<SpeedZoneFeedbackFX>())
            fx.enabled = true;
    }
    // -------------------------

    /* ???????????????????? FINISH  COROUTINE ?????????????????????? */
    IEnumerator FinishRaceSequence()
    {
        IsRaceStarted = raceStarted = false;   // master flag

        // End logging of game
        if (TelemetryLogger.Instance != null)
        {
            TelemetryLogger.Instance.StopLogging();
        }

        DisableGameplaySystems();
        counting = false;
        timeLeft = 0f;
        finalising = true;
        driverInput.enabled = false;
        gameplayHUD.alpha = 0;

        /* Music � gameplay OUT, lobby IN */
        if (gameplayReverb) gameplayReverb.enabled = true;
        StartCoroutine(FadeReverbDryLevel(gameplayReverb, 0, -10000, 3f));
        StartCoroutine(FadeAudio(gameplayMusic, gameplayMusicTargetVol, 0f, 3f));
        if (gameplayLPF) StartCoroutine(FadeLowpassCutoff(gameplayLPF, 22000, 2000, 3f));

        if (lobbyMusic)
        {
            lobbyMusic.Play();
            StartCoroutine(FadeAudio(lobbyMusic, 0f, lobbyMusicTargetVol, 3f));
            var lpf = lobbyMusic.GetComponent<AudioLowPassFilter>();
            if (lpf) StartCoroutine(FadeLowpassCutoff(lpf, 2000, 22000, 3f));
            var rev = lobbyMusic.GetComponent<AudioReverbFilter>();
            if (rev) StartCoroutine(FadeReverbDryLevel(rev, -10000, 0, 3f));
        }

        /* Cameras */
        gameplayCamera.SetActive(false);
        introCamera.gameObject.SetActive(true);
        cameraAnimator.ResetTrigger(cameraAnimTrigger);
        cameraAnimator.ResetTrigger(cameraResultsAnimTrigger);
        cameraAnimator.SetTrigger(cameraResultsAnimTrigger);     // reuse intro anim

        yield return new WaitForSeconds(5f);

        /* Results panel fade-in */
        resultsPanel.gameObject.SetActive(true);
        yield return FadeCanvas(resultsPanel, 0f, 1f, 1.5f);

        /* Build results list bottom-up */
        var passengers = PlayerManager.Instance.Passengers
                          .OrderBy(p => p.Points).ToList(); // lowest first
        resultsText.text = "";
        for (int i = 0; i < passengers.Count; ++i)
        {
            var p = passengers[i];
            string color = ColorUtility.ToHtmlStringRGBA(PlayerManager.Instance.GetColorForPlayerNumber(p.PlayerNumber));
            string line = $"<color=#{color}>Player {p.PlayerNumber}</color>  -  {p.Points} pts";
            resultsText.text = line + "\n" + resultsText.text;      // prepend
            yield return new WaitForSeconds(0.3f);
        }

        /* Winner banner shake */
        var winner = passengers.Last();
        winnerText.text = $"Player {PlayerManager.Instance.GetSeatIndex(winner) + 1} WINS!";
        winnerText.alpha = 0;

        winnerText.rectTransform.anchoredPosition = startPos;
        winnerText.gameObject.SetActive(true);
        crowdroar.Play();
        // fade-in & shake
        float t = 0, dur = 1f, shakeMag = 40f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float a = t / dur;
            winnerText.alpha = a;

            Vector2 shake = UnityEngine.Random.insideUnitCircle * shakeMag * (1f - a * .7f);
            winnerText.rectTransform.anchoredPosition = startPos + shake;
            yield return null;
        }
        winnerText.alpha = 1f;
        winnerText.rectTransform.anchoredPosition = startPos;

        /* Buttons fade-in after 1 s */
        yield return new WaitForSeconds(1f);
        StartCoroutine(AutoReturnCountdown(10));

        // Finished � now idling on results screen
    }
    void CacheLobbyDummyTintData()
    {
        int n = (playerDummies != null) ? playerDummies.Length : 0;
        if (n <= 0) return;

        _dummyRenderers = new Renderer[n][];
        _dummyMpbs = new MaterialPropertyBlock[n];
        _dummyColorPropIds = new int[n];

        for (int i = 0; i < n; i++)
        {
            _dummyColorPropIds[i] = -1;

            if (!playerDummies[i])
            {
                _dummyRenderers[i] = Array.Empty<Renderer>();
                continue;
            }

            _dummyRenderers[i] = playerDummies[i].GetComponentsInChildren<Renderer>(true);
            _dummyMpbs[i] = new MaterialPropertyBlock();
            _dummyColorPropIds[i] = ResolveColorPropertyForRenderers(_dummyRenderers[i]);
        }
    }

    void ResetLobbyDummies()
    {
        if (playerDummies == null) return;

        for (int i = 0; i < playerDummies.Length; i++)
        {
            if (!playerDummies[i]) continue;

            // Reset to white so lobby starts clean
            if (tintLobbyDummies) ApplyColorToLobbyDummy(i, Color.white);
            playerDummies[i].SetActive(false);
        }
    }

    void ApplyColorToLobbyDummy(int seatIdx, Color c)
    {
        if (!tintLobbyDummies) return;
        if (_dummyRenderers == null || _dummyMpbs == null || _dummyColorPropIds == null) return;
        if (seatIdx < 0 || seatIdx >= _dummyRenderers.Length) return;

        var rends = _dummyRenderers[seatIdx];
        if (rends == null || rends.Length == 0) return;

        int propId = _dummyColorPropIds[seatIdx];
        if (propId == -1) return;

        var mpb = _dummyMpbs[seatIdx] ?? (_dummyMpbs[seatIdx] = new MaterialPropertyBlock());

        for (int i = 0; i < rends.Length; i++)
        {
            var r = rends[i];
            if (!r) continue;

            if (r.GetComponentInParent<HatSocketMarker>() != null)
                continue;

            r.GetPropertyBlock(mpb);
            mpb.SetColor(propId, c);
            r.SetPropertyBlock(mpb);
        }
    }

    int ResolveColorPropertyForRenderers(Renderer[] renderers)
    {
        if (renderers == null || renderers.Length == 0) return -1;

        // 1) prefer explicit override on PlayerJoinManager
        string forced = lobbyForcedColorProperty;

        // 2) if empty, use PlayerManager's forced property (same as gameplay)
        if (string.IsNullOrEmpty(forced) && PlayerManager.Instance != null)
            forced = PlayerManager.Instance.ForcedColorProperty;

        if (!string.IsNullOrEmpty(forced))
        {
            int id = Shader.PropertyToID(forced);
            if (AnyHasProperty(renderers, id)) return id;
        }

        // 3) try common candidates
        if (lobbyColorPropertyCandidates != null)
        {
            for (int i = 0; i < lobbyColorPropertyCandidates.Length; i++)
            {
                string name = lobbyColorPropertyCandidates[i];
                if (string.IsNullOrEmpty(name)) continue;

                int id = Shader.PropertyToID(name);
                if (AnyHasProperty(renderers, id)) return id;
            }
        }

        Debug.LogWarning("[PlayerJoinManager] Lobby dummy shader has no _BaseColor/_Color/_TintColor. Set lobbyForcedColorProperty (or PlayerManager forcedColorProperty).");
        return -1;
    }

    bool AnyHasProperty(Renderer[] renderers, int propId)
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (!r) continue;

            var mat = r.sharedMaterial;
            if (mat && mat.HasProperty(propId))
                return true;
        }
        return false;
    }

    IEnumerator FadeAndLoad(string scene)
    {
        blackFadeImage.gameObject.SetActive(true);
        Color c = blackFadeImage.color; c.a = 0; blackFadeImage.color = c;

        float t = 0, dur = 1.2f;
        while (t < dur)
        {
            t += Time.deltaTime;
            c.a = Mathf.Lerp(0, 1, t / dur);
            blackFadeImage.color = c;
            yield return null;
        }
        SceneManager.LoadScene(scene);
    }

    /* ???????????????????? LAP UI UPDATE ????????????????????????? */
    void UpdateLapUI()
    {
        if (lapsText)
            lapsText.text = $"Laps: {currentLap}/{lapsToFinish}";
    }

    /* ?????????????? HELPERS � Fades & Waiters ?????????????? */
    IEnumerator FadeCanvas(CanvasGroup cg, float from, float to, float dur, float delay = 0)
    {
        if (!cg) yield break;
        if (delay > 0) yield return new WaitForSeconds(delay);
        float t = 0; cg.alpha = from;
        while (t < dur) { t += Time.deltaTime; cg.alpha = Mathf.Lerp(from, to, t / dur); yield return null; }
        cg.alpha = to;
    }
    IEnumerator FadeAudio(AudioSource src, float from, float to, float dur, float delay = 0)
    {
        if (!src) yield break;
        if (delay > 0) yield return new WaitForSeconds(delay);
        float t = 0; src.volume = from;
        while (t < dur) { t += Time.deltaTime; src.volume = Mathf.Lerp(from, to, t / dur); yield return null; }
        src.volume = to;
    }
    IEnumerator FadeLowpassCutoff(AudioLowPassFilter lpf, float from, float to, float dur, float delay = 0)
    {
        if (!lpf) yield break;
        if (delay > 0) yield return new WaitForSeconds(delay);
        float t = 0; lpf.cutoffFrequency = from;
        while (t < dur) { t += Time.deltaTime; lpf.cutoffFrequency = Mathf.Lerp(from, to, t / dur); yield return null; }
        lpf.cutoffFrequency = to;
    }
    IEnumerator FadeReverbDryLevel(AudioReverbFilter rev, float from, float to, float dur, float delay = 0)
    {
        if (!rev) yield break;
        if (delay > 0) yield return new WaitForSeconds(delay);
        float t = 0; rev.dryLevel = from;
        while (t < dur) { t += Time.deltaTime; rev.dryLevel = Mathf.Lerp(from, to, t / dur); yield return null; }
        rev.dryLevel = to;
        if (rev == gameplayReverb && Mathf.Approximately(to, 0)) rev.enabled = false;
    }
    static IEnumerator WaitForClipToEnd(Animator anim, string state)
    {
        while (!anim.GetCurrentAnimatorStateInfo(0).IsName(state)) yield return null;
        while (anim.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f || anim.IsInTransition(0)) yield return null;
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

            if (countdownGameAudio)
            {
                if (i < 3)
                    countdownGameAudio.PlayOneShot(first3countdown);
                else
                    countdownGameAudio.PlayOneShot(gocountdown);
            }

            float elapsed = 0f;
            Vector3 targetScale = Vector3.one;
            while (elapsed < pulseDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / pulseDuration;
                countdownGametext.transform.localScale = Vector3.Lerp(Vector3.one * 0.5f, targetScale, t);
                yield return null;
            }

            yield return new WaitForSeconds(pauseBetween);

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


    /* ??????????????? Camera fly customization menu ??????????????? */
    /*public void BeginCustomizationCamera()
    {
        Debug.Log("[CAMFLY] BeginCustomizationCamera() CALLED");

        if (!lobbyCamera || !customizationCamPose)
        {
            Debug.LogWarning("[CAMFLY] Missing lobbyCamera or customizationCamPose reference.");
            return;
        }

        // ensure correct camera is rendering
        lobbyCamera.gameObject.SetActive(true);
        if (mainCamera) mainCamera.gameObject.SetActive(false);

        // cache start pose
        lobbyCamStartPos = lobbyCamera.transform.position;
        lobbyCamStartRot = lobbyCamera.transform.rotation;

        // stop animator so it doesn't fight the fly
        if (cameraAnimatorRev) cameraAnimatorRev.enabled = false;

        customizationCamActive = true;
        StartCameraFly(customizationCamPose.position, customizationCamPose.rotation, reenableAnimator: false);
    }

    public void EndCustomizationCamera()
    {
        Debug.Log("[CAMFLY] EndCustomizationCamera() CALLED");

        if (!lobbyCamera || !customizationCamActive) return;

        customizationCamActive = false;
        StartCameraFly(lobbyCamStartPos, lobbyCamStartRot, reenableAnimator: true);
    }

    private void StartCameraFly(Vector3 targetPos, Quaternion targetRot, bool reenableAnimator)
    {
        if (camFlyRoutine != null) StopCoroutine(camFlyRoutine);
        camFlyRoutine = StartCoroutine(FlyCameraRoutine(targetPos, targetRot, reenableAnimator));
    }

    private IEnumerator FlyCameraRoutine(Vector3 targetPos, Quaternion targetRot, bool reenableAnimator)
    {
        Transform camT = lobbyCamera.transform;

        Vector3 startPos = camT.position;
        Quaternion startRot = camT.rotation;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.01f, customizationFlyTime);
            camT.position = Vector3.Lerp(startPos, targetPos, t);
            camT.rotation = Quaternion.Slerp(startRot, targetRot, t);
            yield return null;
        }

        if (reenableAnimator && cameraAnimatorRev)
            cameraAnimatorRev.enabled = true;
    }*/

    private void SwitchAllPlayersToActionMap(string mapName)
    {
        foreach (var pi in GetJoinedPlayers())
        {
            if (!pi) continue;
            pi.SwitchCurrentActionMap(mapName);
        }
    }

    /* ??????????????? CLEAN-UP ??????????????? */
    void OnDestroy()
    { PlayerInputManager.instance?.playerJoinedEvent.RemoveListener(OnPlayerJoined); }

    /* ????????????????????????????????????????????????????????????? */
}
