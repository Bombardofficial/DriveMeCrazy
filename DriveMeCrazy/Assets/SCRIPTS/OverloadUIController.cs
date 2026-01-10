using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

    public class OverloadUIController : MonoBehaviour
{
    public GameObject buttonPrefab;
    public Transform buttonContainer;
    public Image timerFill;
    public Color successColor;
    public Color failureColor;

    private Dictionary<int, OverloadUIButton> uiButtons = new();

    public void Show(List<OverloadButton> buttons)
    {
        Clear();

        foreach (var button in buttons)
        {
            var ui = Instantiate(buttonPrefab, buttonContainer)
                     .GetComponent<OverloadUIButton>();

            ui.SetButtonType(button._buttonType);
            uiButtons[button._buttonId] = ui;
        }

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

