using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class OverloadUIController : MonoBehaviour
{
    public OverloadUIButton buttonPrefab;
    public Transform buttonContainer;
    public Image timerFill;
    public Color successColor;
    public Color failureColor;
    public TMP_Text successMessage;
    public TMP_Text failureMessage;
    public TMP_Text overheatMessage;
    public TMP_Text holdMessage;

    private CanvasGroup overloadUICanvas;
    private Dictionary<int, OverloadUIButton> uiButtons = new();

    void Awake()
    {
        overloadUICanvas = GetComponent<CanvasGroup>();
        HideImmediate();
    }

    public void Show(List<OverloadButton> buttons)
    {
        Clear();

        gameObject.SetActive(true);

        overloadUICanvas.alpha = 1f;
        overloadUICanvas.interactable = false;
        overloadUICanvas.blocksRaycasts = false;

        foreach (var button in buttons)
        {
            var ui = Instantiate(buttonPrefab, buttonContainer);

            ui.ResetVisuals();
            ui.SetButtonType(button._buttonType);
            uiButtons[button._buttonId] = ui;
        }
        
        timerFill.color = Color.white;
        timerFill.fillAmount = 1f;

        overheatMessage.gameObject.SetActive(true);
        holdMessage.gameObject.SetActive(true);
    }

    public void UpdateTimer(float ratio)
    {
        timerFill.fillAmount = ratio;
    }

    public void SetHeld(int buttonId, int playerId)
    {
        uiButtons[buttonId].SetHeld(playerId);
    }

    public void SetReleased(int buttonId)
    {
        uiButtons[buttonId].SetReleased();
    }

    public void Hide()
    {
        Clear();
        gameObject.SetActive(false);
    }

    public void HideImmediate()
    {
        overloadUICanvas.alpha = 0f;
        Clear();
        gameObject.SetActive(false);
    }

    public void PlaySuccess()
    {
        foreach (var button in uiButtons.Values)
        {
            button.PlaySuccessFlash();
        }

        overheatMessage.gameObject.SetActive(false);
        holdMessage.gameObject.SetActive(false);
        successMessage.gameObject.SetActive(true);

        timerFill.color = successColor;

        //pointsPopup.Show($"+{successPoints}", successColor);
    }

    public void PlayFailure()
    {
        foreach (var button in uiButtons.Values)
        {
            button.PlayFailureFlash();
        }

        overheatMessage.gameObject.SetActive(false);
        holdMessage.gameObject.SetActive(false);
        failureMessage.gameObject.SetActive(true);

        timerFill.color = failureColor;

        //pointsPopup.Show($"-{failurePoints}", failureColor);
    }

    private void Clear()
    {
        foreach (Transform child in buttonContainer)
            Destroy(child.gameObject);

        uiButtons.Clear();

        successMessage.gameObject.SetActive(false);
        failureMessage.gameObject.SetActive(false);
        overheatMessage.gameObject.SetActive(false);
        holdMessage.gameObject.SetActive(false);
    }
}

