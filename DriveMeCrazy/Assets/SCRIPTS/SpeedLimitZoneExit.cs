//  SpeedLimitZoneExit.cs      (no numeric argument any more)
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class SpeedLimitZoneExit : MonoBehaviour
{
    public string targetTag = "Driver";

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(targetTag)) return;
        var car = other.attachedRigidbody
                 ? other.attachedRigidbody.GetComponent<ArcadeVP.ArcadeVehicleController>()
                 : other.GetComponent<ArcadeVP.ArcadeVehicleController>();

        if (car) { Debug.Log("[SpeedZone]  EXIT"); car.ExitSpeedLimit(); }
    }
}
