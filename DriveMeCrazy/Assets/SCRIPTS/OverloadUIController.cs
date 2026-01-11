using System.Collections;
using System.Collections.Generic;
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
        gameObject.SetActive(false);
    }

    public void PlaySuccess()
    {
        foreach (var button in uiButtons.Values)
        {
            button.PlaySuccessFlash();
        }

        timerFill.color = successColor;

        //pointsPopup.Show($"+{successPoints}", successColor);
    }

    public void PlayFailure()
    {
        foreach (var button in uiButtons.Values)
        {
            button.PlayFailureFlash();
        }

        timerFill.color = failureColor;

        //pointsPopup.Show($"-{failurePoints}", failureColor);
    }

    private void Clear()
    {
        foreach (Transform child in buttonContainer)
            Destroy(child.gameObject);

        uiButtons.Clear();
    }
}

