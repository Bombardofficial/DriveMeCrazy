using UnityEngine;
using ArcadeVP;

[RequireComponent(typeof(Collider))]
public class SpeedLimitZoneExit : MonoBehaviour
{
    public string targetTag = "Driver";

    [Tooltip("MUST match the entering SpeedLimitZone.signIndex for this zone")]
    public int signIndex = 0;

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(targetTag)) return;

        var car = other.attachedRigidbody
            ? other.attachedRigidbody.GetComponent<ArcadeVP.ArcadeVehicleController>()
            : other.GetComponent<ArcadeVP.ArcadeVehicleController>();

        if (!car) return;

        // ---- QUICK UI FIX ----
        if (SpeedLimitUI.Instance)
            SpeedLimitUI.Instance.Hide(signIndex);

        Debug.Log($"[SpeedZone] EXIT signIndex={signIndex}");
        car.ExitSpeedLimit(signIndex);

        if (DriftState.IsInDriftZone)
        {
            DriftState.IsInDriftZone = false;
            TelemetryLogger.Instance?.SetCurveActive(false);
            Debug.Log("[DriftZone] Exited Curve");
        }
    }
}
