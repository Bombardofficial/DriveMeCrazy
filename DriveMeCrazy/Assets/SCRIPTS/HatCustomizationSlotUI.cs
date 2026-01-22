using TMPro;
using UnityEngine;

public class HatCustomizationSlotUI : MonoBehaviour
{
    [Header("UI Refs")]
    [SerializeField] private CanvasGroup canvasGroup;   // easiest way to gray out
    public TMP_Text hatNameText;   // your "New Text" TMP

    [Header("Inactive Text")]
    [SerializeField] private string inactiveLabel = "None"; // or "" for empty

    public void SetJoined(bool joined)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = joined ? 1f : 0.35f;     // gray look
            canvasGroup.interactable = joined;
            canvasGroup.blocksRaycasts = joined;
        }

        if (!joined)
            SetHatLabel(inactiveLabel);
    }

    public void SetHatLabel(string name)
    {
        if (hatNameText != null)
            hatNameText.text = name;
    }
}
