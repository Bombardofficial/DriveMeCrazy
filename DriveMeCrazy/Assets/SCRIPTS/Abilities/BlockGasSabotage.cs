using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BlockGasSabotageSabotage : MonoBehaviour
{
    [Tooltip("Duration for blocking of gas")]
    [SerializeField] float duration = 2f;

    void OnEnable() => ArcadeVP.InputManager_ArcadeVP.OnBlockGasSabotageTriggered += Fire;
    void OnDisable() => ArcadeVP.InputManager_ArcadeVP.OnBlockGasSabotageTriggered -= Fire;

    void Fire(Passenger passenger)
    {
        StartCoroutine(Run(passenger));
    }

    IEnumerator Run(Passenger passenger)
    {
        float t = 0f;

        ArcadeVP.VehicleInputMixer.BlockGas();
        Debug.Log("Gas blocked!");

        while (t < duration)
        {
            t += Time.deltaTime;
            yield return null;
        }

        ArcadeVP.VehicleInputMixer.UnBlockGas();
        Debug.Log("Gas unblocked!");
    }
}
