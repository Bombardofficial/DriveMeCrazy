using UnityEngine;

[RequireComponent(typeof(Collider))]
public class SpeedLimitZoneExit : MonoBehaviour
{
    public string targetTag = "Driver";

    [Tooltip("MUST match the entering SpeedLimitZone.signIndex for this corner")]
    public int signIndex = 0;

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(targetTag)) return;

        var car = other.attachedRigidbody
            ? other.attachedRigidbody.GetComponent<ArcadeVP.ArcadeVehicleController>()
            : other.GetComponent<ArcadeVP.ArcadeVehicleController>();

        if (!car) return;

        Debug.Log($"[SpeedZone] EXIT signIndex={signIndex}");
        car.ExitSpeedLimit(signIndex);
    }
}
