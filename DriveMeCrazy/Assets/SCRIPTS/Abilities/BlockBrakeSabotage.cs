using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BlockBrakeSabotageSabotage : MonoBehaviour
{
    [Tooltip("Duration for blocking of brake")]
    [SerializeField] float duration = 2;

    void OnEnable() => ArcadeVP.InputManager_ArcadeVP.OnBlockBrakeSabotageTriggered += Fire;
    void OnDisable() => ArcadeVP.InputManager_ArcadeVP.OnBlockBrakeSabotageTriggered -= Fire;

    void Fire(Passenger passenger)
    {
        StartCoroutine(Run(passenger));
    }

    IEnumerator Run(Passenger passenger)
    {
        float t = 0f;

        ArcadeVP.VehicleInputMixer.BlockBrake();
        Debug.Log("Brake blocked!");

        while (t < duration)
        {
            t += Time.deltaTime;
            yield return null;
        }

        ArcadeVP.VehicleInputMixer.UnBlockBrake();
        Debug.Log("Brake unblocked!");
    }
}
