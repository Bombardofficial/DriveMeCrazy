using UnityEngine;
using Cinemachine;

public class CameraShake : MonoBehaviour
{
    public CinemachineVirtualCamera virtualCam;
    public float defaultShakeDuration = 0.5f;
    public float defaultAmplitude = 1.2f;
    public float defaultFrequency = 2f;

    private CinemachineBasicMultiChannelPerlin noise;
    private float shakeTimer;

    void Start()
    {
        if (virtualCam == null)
            virtualCam = GetComponent<CinemachineVirtualCamera>();

        if (virtualCam != null)
            noise = virtualCam.GetCinemachineComponent<CinemachineBasicMultiChannelPerlin>();
    }

    void Update()
    {
        if (shakeTimer > 0)
        {
            shakeTimer -= Time.deltaTime;
            if (shakeTimer <= 0f)
            {
                StopShake();
            }
        }
    }

    public void DoShake(float intensity = -1f, float duration = -1f)
    {
        if (noise == null) return;

        noise.m_AmplitudeGain = (intensity > 0f) ? intensity : defaultAmplitude;
        noise.m_FrequencyGain = defaultFrequency;
        shakeTimer = (duration > 0f) ? duration : defaultShakeDuration;
    }

    public void StopShake()
    {
        if (noise == null) return;
        noise.m_AmplitudeGain = 0f;
        noise.m_FrequencyGain = 0f;
    }
}
