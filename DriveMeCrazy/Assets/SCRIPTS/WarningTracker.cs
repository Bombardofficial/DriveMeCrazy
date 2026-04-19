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
    [SerializeField] private float forwardDotThreshold = 0.7f;
    [SerializeField] private float checkInterval = 0.02f; // 10x pro Sekunde
    [SerializeField] private float warningHoldDuration = 0.3f; // Warnung bleibt 0.3s länger an

    private bool _warningCurrentlyOn = false;
    private float _lastValidObstacleTime;
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
        int currentLane = vehicle.CurrentLane;
        Vector3 carPos = vehicle.transform.position;
        Vector3 carForward = vehicle.transform.forward;

        foreach (var meta in obstacleManager.ActiveMetas)
        {
            if (meta.go == null || !meta.go.activeInHierarchy) continue;

            // 1. Filter: Nur Hindernisse, keine Collectables
            Obstacle obs = meta.go.GetComponent<Obstacle>();
            if (obs == null || obs.HasBeenHit) continue;

            // 2. Spur-Filter
            if (meta.lane != currentLane) continue;

            // 3. Distanz-Filter (wieder fest auf 20-30m)
            float distance = Vector3.Distance(carPos, meta.go.transform.position);
            if (distance > warningDistance) continue;

            // 4. Sichtkegel
            Vector3 toObstacle = (meta.go.transform.position - carPos).normalized;
            if (Vector3.Dot(carForward, toObstacle) < forwardDotThreshold) continue;

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestObstacle = meta.go;
            }
        }

        // ZUSTANDS-LOGIK:
        if (bestObstacle != null)
        {
            // Wir haben ein Hindernis gefunden. 
            // Wenn es ein neues ist, UI triggern.
            if (_currentWarnedObstacle != bestObstacle)
            {
                _currentWarnedObstacle = bestObstacle;
                TriggerWarning(true);
            }
        }
        else
        {
            // Kein Hindernis in der aktuellen Spur gefunden -> Ausschalten
            if (_warningCurrentlyOn)
            {
                ClearWarning();
            }
        }

        /*
        if (metas != null)
        {
            for (int i = 0; i < metas.Count; i++)
            {
                var meta = metas[i];
                if (meta.go == null || !meta.go.activeInHierarchy) continue;

                // WICHTIG: Nur Hindernisse auf der aktuellen Spur!
                if (meta.lane != currentLane) continue;

                Vector3 toObstacle = meta.go.transform.position - carPos;
                float distance = toObstacle.magnitude;

                if (distance > warningDistance) continue;

                float dot = Vector3.Dot(carForward, toObstacle.normalized);
                if (dot < forwardDotThreshold) continue;

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestObstacle = meta.go;
                }
            }
        }
        */
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
