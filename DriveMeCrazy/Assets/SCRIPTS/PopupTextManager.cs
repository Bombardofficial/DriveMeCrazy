using UnityEngine;
using TMPro;

public class PopupTextManager : MonoBehaviour
{
    [Header("Colors")]
    [Tooltip("Points Color")]
    public Color pointsColor;
    [Tooltip("Damage Color")]
    public Color damageColor;

    [Header("Position")]
    [Tooltip("Popup Text Poition")]
    [SerializeField] private Vector3 popupTextPos;

    [Header("References")]
    [Tooltip("Popup Text Prefab")]
    public TextMeshProUGUI popupPrefab;

    [Tooltip("Canvas where the Popup should take place")]
    public Canvas canvas;

    public void Show(string message, Color color)
    {
        var popup = Instantiate(popupPrefab, canvas.transform);
        popup.gameObject.SetActive(true);

        popup.transform.localPosition = popupTextPos;

        popup.GetComponent<PopupText>().Init(message, color);
    }
}
