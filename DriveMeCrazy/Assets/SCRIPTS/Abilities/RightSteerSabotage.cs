using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RightSteerSabotageSabotage : MonoBehaviour
{
    [Tooltip("Max steering kick (+/-).")]
    [Range(0f, 1f)][SerializeField] float maxSteer = 0.8f;

    [Tooltip("Cooldown for ability use per player")]
    [SerializeField] float cooldown = 3f;

    //private Dictionary<Passenger, float> _onCooldowns = new();

    Coroutine _co;

    void OnEnable() => ArcadeVP.InputManager_ArcadeVP.OnRightSteerSabotageTriggered += Fire;
    void OnDisable() => ArcadeVP.InputManager_ArcadeVP.OnRightSteerSabotageTriggered -= Fire;

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
        float sign = 1;
        float steer = sign * maxSteer;
        //_onCooldowns[passenger] = Time.time + cooldown;

        ArcadeVP.VehicleInputMixer.AddSteer(steer);

        yield return null;
        _co = null;
    }
}
