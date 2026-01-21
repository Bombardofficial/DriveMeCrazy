using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpeedingSabotage : MonoBehaviour
{
    [Tooltip("Seconds the sabotage lasts in total.")]
    [SerializeField] float duration = 1.5f;

    void OnEnable() => ArcadeVP.InputManager_ArcadeVP.OnPressGasSabotageTriggered += Fire;
    void OnDisable() => ArcadeVP.InputManager_ArcadeVP.OnPressGasSabotageTriggered -= Fire;

    void Fire(Passenger passenger)
    {
        StartCoroutine(Run(passenger));
    }

    IEnumerator Run(Passenger passenger)
    {
        float t = 0f;
        ArcadeVP.VehicleInputMixer.SabotageGasPress();

        while (t < duration)
        {
            
            t += Time.deltaTime;
            yield return null;
        }

        ArcadeVP.VehicleInputMixer.SabotageGasRelease();
    }
}
