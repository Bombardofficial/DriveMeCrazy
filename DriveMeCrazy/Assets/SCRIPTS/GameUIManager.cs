// Filename: GameUIManager.cs
using UnityEngine;
using TMPro; // Required for TextMeshPro UI elements
using System.Collections.Generic;
using System.Collections;
using ArcadeVP;
using UnityEngine.UI;
using System;

public class GameUIManager : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("The UI Text element for displaying car health.")]
    [SerializeField] private TextMeshProUGUI carDamageText;
    [SerializeField] private TextMeshProUGUI carDamageNumber;

    [Tooltip("A list of UI Text elements for player scores. Assign these in the Inspector.")]
    [SerializeField] private List<TextMeshProUGUI> playerScoreTexts;
    [SerializeField] private List<TextMeshProUGUI> playerScoreNumbers;
    [SerializeField] private List<UnityEngine.UI.Image> playerScoreBackgrounds;

    [Tooltip("A list of UI Text elements for sabotage cooldowns. Assign these in the Inspector.")]
    [SerializeField] private List<Image> blockAbilityIcons;
    [SerializeField] private List<TextMeshProUGUI> blockCooldownNumbers;
    [SerializeField] private List<Image> blockCooldownOverlays;
    [SerializeField] private List<Image> disruptAbilityIcons;
    [SerializeField] private List<TextMeshProUGUI> disruptCooldownNumbers;
    [SerializeField] private List<Image> disruptCooldownOverlays;

    [Tooltip("A list of UI Text elements for Point Mulitplier. Assign these in the Inspector.")]
    [SerializeField] private TextMeshProUGUI pointMultiplierText;
    [SerializeField] private TextMeshProUGUI pointMultiplierNumber;

    [Tooltip("A list of UI Text elements for player scores. Assign these in the Inspector.")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Game Component References")]
    [Tooltip("Drag the GameObject with the Damageable script here.")]
    [SerializeField] private Damageable carDamageable;
    [Tooltip("Drag the GameObject with the PointMultiplication script here.")]
    [SerializeField] private PointMultiplication pointMultiplication;

    private PlayerManager playerManager;

    // ---- NEW ----
    // This will store the text's original color.
    private Color _defaultHealthColor;
    // These are the colors for our new gradient.
    private readonly Color _cautionColor = new Color(1.0f, 0.6f, 0.0f); // A nice orange
    private readonly Color _dangerColor = Color.red;
    // -------------

    [Header("Driver-Change Announcement")]
    [SerializeField] private TextMeshProUGUI newDriverText;   // drag a big TMP text here
    [SerializeField] private float announceDuration = 1.5f;  // total time on-screen
    [SerializeField] private float shakeMagnitude = 40f;     // pixels

    [Header("Multiplier Pop Animation")]
    [SerializeField] private float popFactor = 1.25f;   
    [SerializeField] private float popDuration = 0.2f;  
    private Vector3 baseMultiplierScale;

    public AudioSource newDriverAudio;
    public AudioClip newDriverAudioClip;

    private float lastMultiplier = 0f;

    void Start()
    {
        playerManager = PlayerManager.Instance;

        if (playerManager == null || carDamageable == null)
        {
            Debug.LogError("GameUIManager is missing critical references! Assign them in the Inspector.");
            gameObject.SetActive(false);
            return;
        }

        // ---- NEW ----
        // Store the original color of the health text at the start.
        if (carDamageNumber != null)
        {
            _defaultHealthColor = carDamageNumber.color;
        }
        // -------------

        foreach (var scoreText in playerScoreTexts)
        {
            scoreText.gameObject.SetActive(false);
        }
        foreach (var scoreNumber in playerScoreNumbers)
        {
            scoreNumber.gameObject.SetActive(false);
        }
        foreach (var scoreBack in playerScoreBackgrounds)
        {
            scoreBack.gameObject.SetActive(false);
        }

        PlayerManager.OnDriverChanged += HandleDriverChange;

        baseMultiplierScale = pointMultiplierNumber.transform.localScale;
    }

    void OnDestroy()     // or OnDisable if you prefer
    {
        PlayerManager.OnDriverChanged -= HandleDriverChange;
    }

    void HandleDriverChange(Passenger oldDriver, Passenger newDriver)
    {
        if (!PlayerJoinManager.IsRaceStarted)
            return;
        if (!newDriverText) return;

        string msg = $"PLAYER {newDriver.PlayerNumber}\nTAKES THE WHEEL!";
        newDriverAudio.PlayOneShot(newDriverAudioClip);
        StopAllCoroutines();                          // kill previous animation
        StartCoroutine(DriverAnnounceRoutine(msg));
    }

    IEnumerator DriverAnnounceRoutine(string message)
    {
        // set-up
        newDriverText.text = message;
        newDriverText.alpha = 0f;
        newDriverText.rectTransform.anchoredPosition = Vector2.zero;
        newDriverText.gameObject.SetActive(true);

        float half = announceDuration * 0.5f;
        float t = 0f;

        // ?? fade-in & shake ??????????????????????????????
        while (t < half)
        {
            t += Time.deltaTime;
            float a = t / half;                        // 0 ? 1
            newDriverText.alpha = Mathf.SmoothStep(0f, 1f, a);

            // simple screen-shake by jittering anchored position
            Vector2 shake = UnityEngine.Random.insideUnitCircle * shakeMagnitude * (1f - a * 0.6f);
            newDriverText.rectTransform.anchoredPosition = shake;

            yield return null;
        }

        // ?? fade-out (no shake) ??????????????????????????
        t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            float a = 1f - (t / half);                 // 1 ? 0
            newDriverText.alpha = a;
            yield return null;
        }

        newDriverText.gameObject.SetActive(false);
        newDriverText.rectTransform.anchoredPosition = Vector2.zero;
    }


    void Update()
    {
        UpdateDamageUI();
        UpdatePlayerScoresUI();
        UpdatePointMultiplierUI();
        UpdateAbilityCooldownsUI();
    }

    // ---- MODIFIED ----
    // This entire method has been updated for the new health display.
    void UpdateDamageUI()
    {
        if (carDamageNumber == null) return;

        // Calculate health as a percentage from 100 down to 0.
        float healthPercent = (carDamageable.Health / carDamageable.maxHealth) * 100f;

        // Update the text to show "Car Health" and the new value.
        carDamageNumber.text = $"{healthPercent:F0}%";

        // Logic to smoothly change the color based on the current health percentage.
        if (healthPercent > 50f)
        {
            // Health is good, use the default color.
            carDamageNumber.color = _defaultHealthColor;
        }
        else if (healthPercent > 25f)
        {
            // Health is between 50% and 25%. We smoothly interpolate from default to orange.
            // 't' will go from 0 (at 50% health) to 1 (at 25% health).
            float t = 1.0f - ((healthPercent - 25f) / 25f);
            carDamageNumber.color = Color.Lerp(_defaultHealthColor, _cautionColor, t);
        }
        else // Health is 25% or lower.
        {
            // Health is critical. We smoothly interpolate from orange to red.
            // 't' will go from 0 (at 25% health) to 1 (at 0% health).
            float t = 1.0f - (healthPercent / 25f);
            carDamageNumber.color = Color.Lerp(_cautionColor, _dangerColor, t);
        }
    }
    // --------------------

    void UpdatePlayerScoresUI()
    {
        IReadOnlyList<Passenger> passengers = playerManager.Passengers;

        // 1. hide everything first
        foreach (var tx in playerScoreTexts) tx.gameObject.SetActive(false);
        foreach (var num in playerScoreNumbers) num.gameObject.SetActive(false);
        foreach (var back in playerScoreTexts) back.gameObject.SetActive(false);

        // 2. (re)populate by permanent id ? slot index = PlayerNumber-1
        foreach (var p in passengers)
        {
            int slot = p.PlayerNumber - 1;
            if (slot < 0 || slot >= playerScoreTexts.Count) continue;

            var tx = playerScoreTexts[slot];
            tx.gameObject.SetActive(true);

            string driverTag = (p == playerManager.CurrentDriver) ? " (Driver)" : "";
            tx.text = $"Player {p.PlayerNumber}{driverTag}";
            tx.color = playerManager.GetColorForPlayerNumber(p.PlayerNumber);

            var numtx = playerScoreNumbers[slot];
            numtx.gameObject.SetActive(true);
            numtx.text = $"{p.Points} Points";

            playerScoreBackgrounds[slot].gameObject.SetActive(true);
        }
    }

    void UpdateAbilityCooldownsUI()
    {
        IReadOnlyList<Passenger> passengers = playerManager.Passengers;

        // 1. hide everything first
        foreach (var ic in blockAbilityIcons) ic.gameObject.SetActive(false);
        foreach (var num in blockCooldownNumbers) num.gameObject.SetActive(false);
        foreach (var ov in blockCooldownOverlays) ov.gameObject.SetActive(false);
        foreach (var ic in disruptAbilityIcons) ic.gameObject.SetActive(false);
        foreach (var num in disruptCooldownNumbers) num.gameObject.SetActive(false);
        foreach (var ov in disruptCooldownOverlays) ov.gameObject.SetActive(false);

        // 2. (re)populate by permanent id ? slot index = PlayerNumber-1
        foreach (var p in passengers)
        {
            int slot = p.PlayerNumber - 1;
            if (slot < 0 || slot >= playerScoreTexts.Count) continue;
            if (p.PlayerNumber == playerManager.CurrentDriver.PlayerNumber) continue;

            blockAbilityIcons[slot].gameObject.SetActive(true);
            blockAbilityIcons[slot].color = new(1f,1f,1f,1f);
            disruptAbilityIcons[slot].gameObject.SetActive(true);
            disruptAbilityIcons[slot].color = new(1f,1f,1f,1f);

            SabotageInputHandler inputHandler = p.GetComponent<SabotageInputHandler>();

            float time = Time.time;
            float blockingCooldownRemain = inputHandler.LastBlock + inputHandler._blockingSabotageCooldown - time;
            float disruptingCooldownRemain = inputHandler.LastDisrupt + inputHandler._disruptingSabotageCooldown - time;
            float sabotageCooldownRemain = inputHandler.Lastsabotage + inputHandler._generalSabotageCooldown - time;

            if (blockingCooldownRemain >= 0f)
            {
                TextMeshProUGUI text = blockCooldownNumbers[slot];
                text.gameObject.SetActive(true);
                text.text = $"{Math.Ceiling(blockingCooldownRemain)}";
                blockAbilityIcons[slot].color = new(1f,1f,1f,0.7f);
            }

            if (disruptingCooldownRemain >= 0f)
            {
                TextMeshProUGUI text = disruptCooldownNumbers[slot];
                text.gameObject.SetActive(true);
                text.text = $"{Math.Ceiling(disruptingCooldownRemain)}";
                disruptAbilityIcons[slot].color = new(1f,1f,1f,0.7f);
            }

            if (sabotageCooldownRemain >= 0f)
            {
                Image cover = blockCooldownOverlays[slot];
                cover.gameObject.SetActive(true);
                cover.fillAmount = sabotageCooldownRemain / inputHandler._generalSabotageCooldown;
                cover = disruptCooldownOverlays[slot];
                cover.gameObject.SetActive(true);
                cover.fillAmount = sabotageCooldownRemain / inputHandler._generalSabotageCooldown;
            }
        }
    }

    void UpdatePointMultiplierUI()
    {
        float currentMultiplier = pointMultiplication.GetCurrentMultiplier();

        pointMultiplierNumber.text = $"{currentMultiplier}";
        pointMultiplierNumber.color = pointMultiplication.GetCurrentMultiplicatorColor();
        pointMultiplierNumber.fontSize = pointMultiplication.GetCurrentFontSize();

        pointMultiplierText.color = pointMultiplication.GetCurrentMultiplicatorColor();

        if (currentMultiplier != lastMultiplier)
        {
            PopUpMultiplier();
            lastMultiplier = currentMultiplier;
        }
    }

    void PopUpMultiplier()
    {

        LeanTween.cancel(pointMultiplierNumber.gameObject);

        pointMultiplierNumber.transform.localScale = baseMultiplierScale;

        LeanTween.scale(pointMultiplierNumber.gameObject, baseMultiplierScale * popFactor, popDuration)
                 .setEaseOutBack()
                 .setOnComplete(() =>
                 {
                     LeanTween.scale(pointMultiplierNumber.gameObject, baseMultiplierScale, popDuration * 0.8f)
                              .setEaseInOutQuad();
                 });
    }
}