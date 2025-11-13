using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BlockSteeringSabotageSabotage : MonoBehaviour
{
    [Tooltip("Duration for blocking of gas")]
    [SerializeField] float duration = 1.5f;

    [Tooltip("Cooldown for ability use per player")]
    [SerializeField] float cooldown = 6f;

    //private Dictionary<Passenger, float> _onCooldowns = new();

    Coroutine _co;

    void OnEnable() => ArcadeVP.InputManager_ArcadeVP.OnBlockSteeringSabotageTriggered += Fire;
    void OnDisable() => ArcadeVP.InputManager_ArcadeVP.OnBlockSteeringSabotageTriggered -= Fire;

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
        float t = 0f;
        //_onCooldowns[passenger] = Time.time + cooldown;

        ArcadeVP.VehicleInputMixer.BlockSteering();
        Debug.Log("Steering blocked!");

        while (t < duration)
        {
            t += Time.deltaTime;
            yield return null;
        }

        ArcadeVP.VehicleInputMixer.UnBlockSteering();
        Debug.Log("Steering unblocked!");

        
        _co = null;
    }
}
