using System.Collections;
using UnityEngine;

public class HandbrakeSabotage : MonoBehaviour
{
    [Tooltip("Seconds the sabotage lasts in total.")]
    [SerializeField] float duration = 1.5f;

    [Tooltip("Brake intensity at the peak (0–1).")]
    [Range(0f, 1f)][SerializeField] float peakSlow = 1f;

    [Tooltip("Ease-in/out time (seconds).")]
    [SerializeField] float ramp = 0.25f;

    Coroutine _co;

    void OnEnable() => ArcadeVP.InputManager_ArcadeVP.OnHandbrakeSabotageTriggered += Fire;
    void OnDisable() => ArcadeVP.InputManager_ArcadeVP.OnHandbrakeSabotageTriggered -= Fire;

    void Fire()
    {
        if (_co == null) _co = StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        float t = 0f;
        while (t < duration)
        {
            // smooth ramp up / down
            float k = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / ramp));           // in
            k *= Mathf.SmoothStep(0, 1, Mathf.Clamp01((duration - t) / ramp)); // out

            ArcadeVP.VehicleInputMixer.AddSlowBrake(k * peakSlow);
            t += Time.deltaTime;
            yield return null;
        }
        _co = null;
    }
}
