using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BlockSteeringSabotageSabotage : MonoBehaviour
{
    [Tooltip("Duration for blocking of gas")]
    [SerializeField] float duration = 2f;

    void OnEnable() => ArcadeVP.InputManager_ArcadeVP.OnBlockSteeringSabotageTriggered += Fire;
    void OnDisable() => ArcadeVP.InputManager_ArcadeVP.OnBlockSteeringSabotageTriggered -= Fire;

    void Fire(Passenger passenger)
    {
        StartCoroutine(Run(passenger));
    }

    IEnumerator Run(Passenger passenger)
    {
        float t = 0f;

        ArcadeVP.VehicleInputMixer.BlockSteering();
        Debug.Log("Steering blocked!");

        while (t < duration)
        {
            t += Time.deltaTime;
            yield return null;
        }

        ArcadeVP.VehicleInputMixer.UnBlockSteering();
        Debug.Log("Steering unblocked!");
    }
}
