using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SteeringSabotage : MonoBehaviour
{
    [Tooltip("Seconds the sabotage lasts in total.")]
    [SerializeField] float duration = 2f;

    [Tooltip("Max steering kick (+/-).")]
    [Range(0f, 1f)][SerializeField] float maxSteer = 0.8f;

    [Tooltip("How quick the wheel oscillates (Hz).")]
    [SerializeField] float wobbleFrequency = 4f;

    [Tooltip("Cooldown for ability use per player")]
    [SerializeField] float cooldown = 3f;

    private Dictionary<Passenger, float> _onCooldowns = new();

    Coroutine _co;

    void OnEnable() => ArcadeVP.InputManager_ArcadeVP.OnSteeringSabotageTriggered += Fire;
    void OnDisable() => ArcadeVP.InputManager_ArcadeVP.OnSteeringSabotageTriggered -= Fire;

    void Fire(Passenger passenger)
    {
        /*if (!_onCooldowns.ContainsKey(passenger) || Time.time >= _onCooldowns[passenger])
        {
            StartCoroutine(Run(passenger));
        }*/

        StartCoroutine(Run(passenger));
    }

    IEnumerator Run(Passenger passenger)
    {
        // pick a random side so it doesn't always go right
        float sign = Random.value < .5f ? -1 : 1;
        float t = 0f;
        //_onCooldowns[passenger] = Time.time + cooldown;

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
