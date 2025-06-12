/* PlayerJoinManager_TMP.cs  –  put on the same object that owns
    the Player Input Manager component (LobbySystem in the scene) */

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;                                // <- TextMesh Pro
using UnityEngine.Events;

public class PlayerJoinManager : MonoBehaviour
{
    /* ???????????????? UI references ???????????????? */
    [Header("UI")]
    [SerializeField] Image[] seatIcons;      // size = 4
    [SerializeField] TextMeshProUGUI[] seatTexts;      // size = 4
    [SerializeField] GameObject lobbyPanel;      // PlayerJoinPanel root

    [SerializeField] Color freeCol = Color.white;
    [SerializeField] Color takenCol = new(0.2f, 1f, 0.2f);

    /* ????????????? Prefabs & gameplay refs ????????????? */
    [Header("Prefabs & Refs")]
    [SerializeField] GameObject passengerPrefab;
    [SerializeField] CarSeatManager seatMgr;
    [SerializeField] ArcadeVP.ArcadeVehicleController car;

    [Header("Quick-Start (skip lobby)")]
    [Tooltip("When TRUE the game spawns a driver instantly and hides the lobby UI.")]
    [SerializeField] bool quickStartSingleDriver = false;

    [Tooltip("Optional override – leave NULL to use the PIM playerPrefab.")]
    [SerializeField] GameObject driverPrefabOverride;

    /* ????????????? internal state ????????????? */
    int joinCount;
    CarDriverInput driverInput;   // we keep a handle so we can enable it later
    bool raceStarted;

    [Header("Auto-Start Countdown")]
    [SerializeField] float lobbyCountdownSeconds = 10f;
    [SerializeField] TextMeshProUGUI countdownText;   // drag UI label here

    float timeLeft;
    bool counting;

    /* ??????????????????????????????????????????????????? */
    void Start()
    {
        if (!seatMgr)
        {
            Debug.LogError("[PlayerJoinManager] Missing CarSeatManager!");
            enabled = false;
            return;
        }
        if (quickStartSingleDriver)
        {
            SpawnDriverImmediately();
            return;                         // skip all normal lobby initialisation
        }

        car.enabled = false;                  // freeze car until StartRace()
        var pim = PlayerInputManager.instance;
        if (pim != null) pim.playerJoinedEvent.AddListener(OnPlayerJoined);

        // ---- MODIFIED ----
        // Ensure the countdown text object is active from the very beginning.
        if (countdownText != null)
        {
            countdownText.gameObject.SetActive(true);
        }
        // ------------------

        RefreshUI(); // This will now set the initial "Waiting..." text.
    }

    void Update()
    {
        if (!counting || raceStarted) return;

        timeLeft -= Time.deltaTime;

        // ---- MODIFIED ----
        // Update the countdown text with a more professional format.
        countdownText.text = $"RACE STARTS IN: {Mathf.CeilToInt(timeLeft)}";
        // ------------------

        if (timeLeft <= 0f)
        {
            counting = false;
            StartRace();        // existing method, untouched
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
    }

    void OnDestroy()
    {
        var pim = PlayerInputManager.instance;
        if (pim) pim.playerJoinedEvent.RemoveListener(OnPlayerJoined);
    }

    /* ????????????? lobby loop ????????????? */
    void OnPlayerJoined(PlayerInput pi)
    {
        int seatIndex = joinCount;                  // 0,1,2,3…
        Transform seat = seatMgr.GetSeat(seatIndex);

        pi.transform.SetPositionAndRotation(seat.position, seat.rotation);
        pi.transform.SetParent(seat, true);

        /* ---- register with the central roster BEFORE any other logic -------- */
        var passenger = pi.GetComponent<Passenger>();
        if (PlayerManager.Instance)
            PlayerManager.Instance.RegisterPassenger(passenger, seatIndex);

        /* first joiner = driver --------------------------------------------------*/
        if (seatIndex == 0)
        {
            driverInput = pi.GetComponent<CarDriverInput>();
            driverInput.SetVehicle(car);
            driverInput.enabled = false;           // un-freeze at StartRace()

            PlayerInputManager.instance.playerPrefab = passengerPrefab;
        }

        joinCount++;
        RefreshUI();

        // ---- MODIFIED ----
        // This ensures the countdown only starts ONCE, when the first player joins,
        // and doesn't reset when other players join.
        if (!counting)
        {
            timeLeft = lobbyCountdownSeconds;
            counting = true;
        }
        // ------------------
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
            seatTexts[i].text = taken ? $"PLAYER {i + 1}"
                                      : "PRESS GAS TO JOIN";
        }

        // ---- MODIFIED ----
        // Manage the state of the countdown text.
        if (countdownText != null && !counting)
        {
            countdownText.text = "WAITING FOR DRIVER...";
        }
        // ------------------
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