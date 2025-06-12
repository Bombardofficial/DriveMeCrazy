using System.Collections;
using UnityEngine;

public class SteeringSabotage : MonoBehaviour
{
    [Tooltip("Seconds the sabotage lasts in total.")]
    [SerializeField] float duration = 2f;

    [Tooltip("Max steering kick (+/-).")]
    [Range(0f, 1f)][SerializeField] float maxSteer = 0.8f;

    [Tooltip("How quick the wheel oscillates (Hz).")]
    [SerializeField] float wobbleFrequency = 2f;

    Coroutine _co;

    void OnEnable() => ArcadeVP.InputManager_ArcadeVP.OnSteeringSabotageTriggered += Fire;
    void OnDisable() => ArcadeVP.InputManager_ArcadeVP.OnSteeringSabotageTriggered -= Fire;

    void Fire()
    {
        if (_co == null) _co = StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        // pick a random side so it doesn’t *always* go right
        float sign = Random.value < .5f ? -1 : 1;
        float t = 0f;

        while (t < duration)
        {
            // sinusoidal wobble looks more natural than constant lock
            float phase = Mathf.Sin(2 * Mathf.PI * wobbleFrequency * t);
            float steer = sign * maxSteer * phase;

            ArcadeVP.VehicleInputMixer.AddSteer(steer);
            t += Time.deltaTime;
            yield return null;
        }
        _co = null;
    }
}
