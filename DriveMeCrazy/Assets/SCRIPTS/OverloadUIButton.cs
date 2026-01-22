using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public static class PlayerColors
{
    private static readonly Color[] Colors =
    {
        new Color(0.2f, 0.6f, 1f),   // Player 0 – Blue
        new Color(1f, 0.3f, 0.3f),   // Player 1 – Red
        new Color(0.3f, 1f, 0.4f),   // Player 2 – Green
        new Color(1f, 0.9f, 0.3f),   // Player 3 – Yellow
    };

    public static Color Get(int playerId)
    {
        if (playerId < 0 || playerId >= Colors.Length)
        {
            Debug.LogWarning($"[PlayerColors] Invalid playerId {playerId}");
            return Color.white;
        }

        return Colors[playerId];
    }
}

public class OverloadUIButton : MonoBehaviour
{
    public Image icon;
    public Image ring;

    public void SetButtonType(OverloadControlType type)
    {
        icon.sprite = IconLibrary.Get(type);
    }

    public void SetHeld(int playerId)
    {
        ring.color = PlayerColors.Get(playerId);
        ring.enabled = true;
    }

    public void SetReleased()
    {
        ring.enabled = false;
    }

    public void PlaySuccessFlash()
    {
        LeanTween.cancel(gameObject);
        LeanTween.cancel(icon.gameObject);

        SetReleased();

        icon.color = Color.green;
        transform.localScale = Vector3.one * 1.1f;

        LeanTween.scale(gameObject, Vector3.one, 0.2f).setIgnoreTimeScale(true);
        LeanTween.alpha(icon.rectTransform, 0f, 0.3f).setIgnoreTimeScale(true);
    }

    public void PlayFailureFlash()
    {
        LeanTween.cancel(gameObject);
        LeanTween.cancel(icon.gameObject);

        SetReleased();

        icon.color = Color.red;

        LeanTween.moveX(gameObject, transform.position.x + 10f, 0.05f)
            .setLoopPingPong(2).setIgnoreTimeScale(true);

        LeanTween.alpha(icon.rectTransform, 0f, 0.4f).setIgnoreTimeScale(true);
    }

    public void ResetVisuals()
    {
        ring.enabled = false;
        icon.color = Color.white;
        icon.canvasRenderer.SetAlpha(1f);
        transform.localScale = Vector3.one;
    }
}
