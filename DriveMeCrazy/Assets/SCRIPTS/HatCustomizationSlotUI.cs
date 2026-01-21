using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HatCustomizationSlotUI : MonoBehaviour
{
    public int playerIndex;              // 0..3
    public TMP_Text hatNameText;         // your "New Text"
    public Button leftButton;
    public Button rightButton;

    HatCustomizationUIManager manager;

    public void Init(HatCustomizationUIManager m)
    {
        manager = m;
        leftButton.onClick.AddListener(() => manager.StepHat(playerIndex, -1));
        rightButton.onClick.AddListener(() => manager.StepHat(playerIndex, +1));
    }

    public void SetHatLabel(string name) => hatNameText.text = name;
}
