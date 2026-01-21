using TMPro;
using UnityEngine;

public class HatCustomizationSlotUI : MonoBehaviour
{
    public int playerIndex;        // 0..3
    public TMP_Text hatNameText;   // your "New Text" TMP

    public void SetHatLabel(string name)
    {
        if (hatNameText != null)
            hatNameText.text = name;
    }
}
