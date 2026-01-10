using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class OverloadUIButton : MonoBehaviour
{
    public Image icon;
    public Image ring;

    public void SetButtonType(OverloadControlType type)
    {
        //icon.sprite = IconLibrary.Get(type);
    }

    public void SetHeld(int playerId)
    {
        //ring.color = PlayerColors.Get(playerId);
        ring.enabled = true;
    }

    public void SetReleased()
    {
        ring.enabled = false;
    }

    public void PlaySuccessFlash()
    {
        icon.color = Color.green;
        transform.localScale = Vector3.one * 1.1f;

        LeanTween.scale(gameObject, Vector3.one, 0.2f);
        LeanTween.alpha(icon.rectTransform, 0f, 0.3f);
    }

    public void PlayFailureFlash()
    {
        icon.color = Color.red;

        LeanTween.moveX(gameObject, transform.position.x + 10f, 0.05f)
            .setLoopPingPong(2);

        LeanTween.alpha(icon.rectTransform, 0f, 0.4f);
    }
}
