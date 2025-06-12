// Filename: GameUIManager.cs
using UnityEngine;
using TMPro; // Required for TextMeshPro UI elements
using System.Collections.Generic;

public class GameUIManager : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("The UI Text element for displaying car health.")]
    [SerializeField] private TextMeshProUGUI carDamageText;

    [Tooltip("A list of UI Text elements for player scores. Assign these in the Inspector.")]
    [SerializeField] private List<TextMeshProUGUI> playerScoreTexts;

    [Header("Game Component References")]
    [Tooltip("Drag the GameObject with the Damageable script here.")]
    [SerializeField] private Damageable carDamageable;

    private PlayerManager playerManager;

    // ---- NEW ----
    // This will store the text's original color.
    private Color _defaultHealthColor;
    // These are the colors for our new gradient.
    private readonly Color _cautionColor = new Color(1.0f, 0.6f, 0.0f); // A nice orange
    private readonly Color _dangerColor = Color.red;
    // -------------

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
        if (carDamageText != null)
        {
            _defaultHealthColor = carDamageText.color;
        }
        // -------------

        foreach (var scoreText in playerScoreTexts)
        {
            scoreText.gameObject.SetActive(false);
        }
    }

    void Update()
    {
        UpdateDamageUI();
        UpdatePlayerScoresUI();
    }

    // ---- MODIFIED ----
    // This entire method has been updated for the new health display.
    void UpdateDamageUI()
    {
        if (carDamageText == null) return;

        // Calculate health as a percentage from 100 down to 0.
        float healthPercent = (carDamageable.Health / carDamageable.maxHealth) * 100f;

        // Update the text to show "Car Health" and the new value.
        carDamageText.text = $"Car Health: {healthPercent:F0}%";

        // Logic to smoothly change the color based on the current health percentage.
        if (healthPercent > 50f)
        {
            // Health is good, use the default color.
            carDamageText.color = _defaultHealthColor;
        }
        else if (healthPercent > 25f)
        {
            // Health is between 50% and 25%. We smoothly interpolate from default to orange.
            // 't' will go from 0 (at 50% health) to 1 (at 25% health).
            float t = 1.0f - ((healthPercent - 25f) / 25f);
            carDamageText.color = Color.Lerp(_defaultHealthColor, _cautionColor, t);
        }
        else // Health is 25% or lower.
        {
            // Health is critical. We smoothly interpolate from orange to red.
            // 't' will go from 0 (at 25% health) to 1 (at 0% health).
            float t = 1.0f - (healthPercent / 25f);
            carDamageText.color = Color.Lerp(_cautionColor, _dangerColor, t);
        }
    }
    // --------------------

    void UpdatePlayerScoresUI()
    {
        IReadOnlyList<Passenger> passengers = playerManager.Passengers;

        for (int i = 0; i < playerScoreTexts.Count; i++)
        {
            if (i < passengers.Count)
            {
                playerScoreTexts[i].gameObject.SetActive(true);
                string driverTag = (passengers[i] == playerManager.CurrentDriver) ? " (Driver)" : "";
                playerScoreTexts[i].text = $"Player {i + 1}{driverTag}: {passengers[i].Points} Points";
            }
            else
            {
                playerScoreTexts[i].gameObject.SetActive(false);
            }
        }
    }
}