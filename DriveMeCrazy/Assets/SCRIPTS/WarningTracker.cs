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
    [SerializeField] private float warningDistance = 20f;
    [SerializeField] private float forwardDotThreshold = 0.35f;
    [SerializeField] private float checkInterval = 0.1f; // 10x pro Sekunde

    private bool _warningCurrentlyOn = false;
    private float _nextCheckTime;

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
        if (!PlayerJoinManager.IsRaceStarted)
        {
            ClearWarning(); 
            return;
        }

        if (vehicle == null || obstacleManager == null || feedbackUI == null)
        {
            ClearWarning();
            return;
        }

        var metas = obstacleManager.ActiveMetas;

        Vector3 carPos = vehicle.transform.position;
        Vector3 carForward = vehicle.transform.forward;
        int currentLane = vehicle.CurrentLane;

        float bestDistance = float.MaxValue;
        GameObject bestObstacle = null;


        if (metas != null && metas.Count > 0)
        {
            for (int i = 0; i < metas.Count; i++)
            {
                var meta = metas[i];

                if (meta.go == null || !meta.go.activeInHierarchy)
                    continue;

                Obstacle obstacleComponent = meta.go.GetComponent<Obstacle>();
                if (obstacleComponent != null && obstacleComponent.HasBeenHit)
                    continue;

                // Nur aktuelle Spur
                if (meta.lane != currentLane)
                    continue;
                // ---- auskommentieren --> auf alle hindernisse warnung (gleich) 

                Vector3 toObstacle = meta.go.transform.position - carPos;
                float distance = toObstacle.magnitude;

                if (distance > warningDistance)
                    continue;

                Vector3 dirToObstacle = toObstacle.normalized;
                float dot = Vector3.Dot(carForward, dirToObstacle);

                // Nur vor dem Auto
                if (dot < forwardDotThreshold)
                    continue;

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestObstacle = meta.go;
                }
            }
        }

        if (bestObstacle != null)
        {
            if (!_warningCurrentlyOn)
            {
                _warningCurrentlyOn = true;
                TelemetryLogger.Instance?.SetWarningActive(true);

                if (feedbackUI != null)
                    feedbackUI.SetWarningActive(true);                
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

    private void ClearWarning()
    {
        _warningCurrentlyOn = false;

        if (feedbackUI != null)
            feedbackUI.SetWarningActive(false);

        TelemetryLogger.Instance?.SetWarningActive(false);
    }

    /*
    private void ClearWarning()
    {
        if (_warningCurrentlyOn && _currentlyWarnedObstacleId != -1)
        {
            TelemetryLogger.Instance?.RegisterWarningObstacleAvoided(_currentlyWarnedObstacleId);
        }

        _warningCurrentlyOn = false;
        _currentlyWarnedObstacleId = -1;

        if (feedbackUI != null)
            feedbackUI.SetWarningActive(false);

        TelemetryLogger.Instance?.SetWarningActive(false);
    }
    */

    public void ForceClearWarningAfterHit()
    {
        ClearWarning();
    }
}
