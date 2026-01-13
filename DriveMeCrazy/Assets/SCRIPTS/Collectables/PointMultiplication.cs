using System.Collections;
using System.Collections.Generic;
using ArcadeVP;
using UnityEngine;

[RequireComponent(typeof(PlayerManager))]
[RequireComponent(typeof(ArcadeVehicleController))]
public class PointMultiplication : MonoBehaviour
{

    public List<int> multiplicationZoneThresholds = new() {20, 40, 60, 80, 100};
    public List<float> muliplicatorValues = new() {0.1f, 0.5f, 1f, 1.5f, 2f, 2.5f};
    float unitsToKmh = 2.4f;
    int currentMultiplicationZone = 0;
    ArcadeVehicleController vehicle;
    PlayerManager playerManager;
    // Start is called before the first frame update
    void Start()
    {
        vehicle = GetComponent<ArcadeVehicleController>();
        playerManager = GetComponent<PlayerManager>();
    }

    // Update is called once per frame
    void Update()
    {
        float kmh = Mathf.Abs(vehicle.Speed) * unitsToKmh;
        int nextMultiplicationZone = 0;

        foreach (int threshold in multiplicationZoneThresholds)
        {
            if (kmh >= threshold)
                ++nextMultiplicationZone;
            else
                break;
        }

        if (nextMultiplicationZone != currentMultiplicationZone)
        {
            playerManager.SetPointMultiplier(muliplicatorValues[nextMultiplicationZone]);
            currentMultiplicationZone = nextMultiplicationZone;
        }
        
    }
}
