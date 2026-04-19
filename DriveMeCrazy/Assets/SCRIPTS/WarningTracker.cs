using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ArcadeVP;

public class WarningTracker : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ArcadeVehicleController vehicle;
    [SerializeField] private ObstacleManager obstacleManager;
    [SerializeField] private FeedbackIntensityUI feedbackUI;

    [Header("Warning Settings")]
    [SerializeField] private float warningDistance = 25f;
    [SerializeField] private float checkInterval = 0.02f; // 10x pro Sekunde
    [SerializeField] private float warningHoldDuration = 0.3f; // Warnung bleibt 0.3s länger an

    private bool _warningCurrentlyOn = false;
    private float _nextCheckTime;

    private GameObject _currentWarnedObstacle = null; // Speichert das Objekt, das die Warnung auslöst

    private void Reset()
    {
        vehicle = GetComponent<ArcadeVehicleController>();
    }

    private void Update()
    {
        if (Time.time < _nextCheckTime)
            return;

        _nextCheckTime = Time.time + checkInterval;
        CheckWarnings();
    }

    private void CheckWarnings()
    {
        if (!PlayerJoinManager.IsRaceStarted) { ClearWarning(); return; }
        if (vehicle == null || obstacleManager == null || feedbackUI == null) return;

        GameObject bestObstacle = null;
        float bestDistance = float.MaxValue;
        bool inCurve = DriftState.IsInDriftZone;
        int currentLane = vehicle.CurrentLane;
        Vector3 carPos = vehicle.transform.position;
        Vector3 carForward = vehicle.transform.forward;

        int obsOnLane = 0;
        int obsInCurveTotal = 0;

        foreach (var meta in obstacleManager.ActiveMetas)
        {
            if (meta.go == null || !meta.go.activeInHierarchy) continue;

            Obstacle obs = meta.go.GetComponent<Obstacle>();
            if (obs == null || obs.HasBeenHit) continue;

            float dist = Vector3.Distance(carPos, meta.go.transform.position);
            if (dist > warningDistance) continue;

            // --- Logik für Telemetrie ---
            obsInCurveTotal++;
            if (meta.lane == currentLane) obsOnLane++;

            // --- FILTER-LOGIK ---
            // 1. Wenn wir NICHT in der Kurve sind (Gerade), Filtern wir hart nach Spur.
            // 2. In der Kurve lassen wir alles zu (kein 'continue' bei anderem Lane-Index).
            if (!inCurve && meta.lane != currentLane) continue;

            // 2. DISTANZ-FILTER
            // Warnung nur, wenn das Hindernis innerhalb von z.B. 25-30 Metern ist.
            float distance = Vector3.Distance(carPos, meta.go.transform.position);
            if (distance > warningDistance) continue;

            // Dot-Produkt für Richtung (etwas großzügiger für Kurven)
            Vector3 toObstacle = (meta.go.transform.position - carPos).normalized;
            if (Vector3.Dot(carForward, toObstacle) < 0.1f) continue;

            // Finde das NÄCHSTE Hindernis auf der Spur
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestObstacle = meta.go;
            }
        }

        // ZUSTANDS-LOGIK (Unverändert)
        if (bestObstacle != null)
        {
            if (_currentWarnedObstacle != bestObstacle)
            {
                _currentWarnedObstacle = bestObstacle;
                TriggerWarning(true);
            }
        }
        else
        {
            if (_warningCurrentlyOn)
            {
                ClearWarning();
            }
        }
    }

    private void TriggerWarning(bool active)
    {
        _warningCurrentlyOn = active;
        feedbackUI?.SetWarningActive(active);
        TelemetryLogger.Instance?.SetWarningActive(active);
    }

    private void ClearWarning()
    {
        _currentWarnedObstacle = null; // Reset des getrackten Hindernisses
        TriggerWarning(false);
    }

    public void ForceClearWarningAfterHit()
    {
        _currentWarnedObstacle = null;
        ClearWarning();
    }
}
