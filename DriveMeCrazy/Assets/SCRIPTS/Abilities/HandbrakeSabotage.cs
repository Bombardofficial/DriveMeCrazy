using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HandbrakeSabotage : MonoBehaviour
{
    [Tooltip("Seconds the sabotage lasts in total.")]
    [SerializeField] float duration = 1.5f;

    [Tooltip("Brake intensity at the peak (0-1).")]
    [Range(0f, 1f)][SerializeField] float peakSlow = 0.6f;

    [Tooltip("Ease-in/out time (seconds).")]
    [SerializeField] float ramp = 0.25f;

    [Tooltip("Cooldown for ability use per player")]
    [SerializeField] float cooldown = 3f;

    private Dictionary<Passenger, float> _onCooldowns = new();

    Coroutine _co;

    void OnEnable() => ArcadeVP.InputManager_ArcadeVP.OnHandbrakeSabotageTriggered += Fire;
    void OnDisable() => ArcadeVP.InputManager_ArcadeVP.OnHandbrakeSabotageTriggered -= Fire;

    void Fire(Passenger passenger)
    {
        print("Handbrake fired!");
        if (!_onCooldowns.ContainsKey(passenger) || Time.time >= _onCooldowns[passenger])
        {
            print("Handbrake started!");
            StartCoroutine(Run(passenger));
        }
    }

    IEnumerator Run(Passenger passenger)
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

        _onCooldowns[passenger] = Time.time + cooldown;
    }
}
