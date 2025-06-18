using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class FlagFadeController : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public RawImage flagImage;
    [Range(0f, 1f)] public float maxAlpha = 87f / 255f;
    public float fadeSpeed = 2f;

    private float targetAlpha = 0f;
    private bool isHovered = false;
    private bool lockedFade = false;

    void Start()
    {
        SetAlpha(0f);
    }

    void Update()
    {
        if (!flagImage) return;

        Color color = flagImage.color;
        float currentAlpha = color.a;
        float newAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, fadeSpeed * Time.deltaTime);
        SetAlpha(newAlpha);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (lockedFade) return;
        isHovered = true;
        targetAlpha = maxAlpha;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (lockedFade) return;
        isHovered = false;
        targetAlpha = 0f;
    }

    public void LockFadeIn()
    {
        lockedFade = true;
        targetAlpha = maxAlpha;
    }

    public void ResetFade()
    {
        lockedFade = false;
        isHovered = false;
        targetAlpha = 0f;
    }

    private void SetAlpha(float alpha)
    {
        if (flagImage != null)
        {
            Color color = flagImage.color;
            color.a = alpha;
            flagImage.color = color;
        }
    }
}
