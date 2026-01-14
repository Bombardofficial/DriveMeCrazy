using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LeftSteerSabotageSabotage : MonoBehaviour
{
    [Tooltip("Max steering kick (+/-).")]
    [Range(0f, 1f)][SerializeField] float maxSteer = 0.8f;

    void OnEnable() => ArcadeVP.InputManager_ArcadeVP.OnLeftSteerSabotageTriggered += Fire;
    void OnDisable() => ArcadeVP.InputManager_ArcadeVP.OnLeftSteerSabotageTriggered -= Fire;

    void Fire(Passenger passenger)
    {
        StartCoroutine(Run(passenger));
    }

    IEnumerator Run(Passenger passenger)
    {
        float sign = -1;
        float steer = sign * maxSteer;

        ArcadeVP.VehicleInputMixer.AddSteer(steer);

        yield return null;
    }
}
