using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class FeedbackIntensityUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image warningYellowBorder;
    [SerializeField] private Image damageRedBorder;

    [Header("Warning Settings")]
    [SerializeField] private float warningFadeSpeed = 3f;
    [SerializeField] private float warningMaxAlpha = 0.85f;

    [Header("Damage Settings")]
    [SerializeField] private float damageFlashDuration = 0.25f;
    [SerializeField] private float damageFadeOutDuration = 0.35f;
    [SerializeField] private float damageMaxAlpha = 0.95f;

    private bool _warningActive;
    private float _warningAlpha;
    private Coroutine _damageRoutine;

    [Header("Audio")]
    [SerializeField] private AudioSource uiAudioSource;
    [SerializeField] private AudioClip warningClip;
    [SerializeField] private AudioClip damageClip;
    [SerializeField] private float warningCooldown = 0.35f;

    private float _lastWarningAudioTime = -999f;
    private bool _previousWarningActive;

    private void Awake()
    {
        SetImageAlpha(warningYellowBorder, 0f);
        SetImageAlpha(damageRedBorder, 0f);
    }

    private void Update()
    {
        UpdateWarningVisual();
    }

    private void UpdateWarningVisual()
    {
        if (warningYellowBorder == null) return;

        float target = _warningActive ? warningMaxAlpha : 0f;
        _warningAlpha = Mathf.MoveTowards(_warningAlpha, target, warningFadeSpeed * Time.deltaTime);
        SetImageAlpha(warningYellowBorder, _warningAlpha);
    }

    public void SetWarningActive(bool active)
    {
        if (active && !_warningActive)
        {
            if (uiAudioSource != null && warningClip != null && Time.time >= _lastWarningAudioTime + warningCooldown)
            {
                uiAudioSource.PlayOneShot(warningClip);
                _lastWarningAudioTime = Time.time;
            }
        }
        
        _warningActive = active;
    }

    public void TriggerDamageFlash()
    {
        if (damageRedBorder == null) return;

        if (uiAudioSource != null && damageClip != null)
        {
            uiAudioSource.PlayOneShot(damageClip);
        }

        if (_damageRoutine != null)
            StopCoroutine(_damageRoutine);

        _damageRoutine = StartCoroutine(DamageFlashRoutine());
    }

    private IEnumerator DamageFlashRoutine()
    {
        SetImageAlpha(damageRedBorder, damageMaxAlpha);

        float t = 0f;
        while (t < damageFlashDuration)
        {
            t += Time.deltaTime;
            yield return null;
        }

        t = 0f;
        while (t < damageFadeOutDuration)
        {
            t += Time.deltaTime;
            float alpha = Mathf.Lerp(damageMaxAlpha, 0f, t / damageFadeOutDuration);
            SetImageAlpha(damageRedBorder, alpha);
            yield return null;
        }

        SetImageAlpha(damageRedBorder, 0f);
        _damageRoutine = null;
    }

    private void SetImageAlpha(Image image, float alpha)
    {
        if (image == null) return;

        Color c = image.color;
        c.a = alpha;
        image.color = c;
    }
}
