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

    private int _currentlyWarnedObstacleId = -1;
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
            ClearWarningAsAvoided(); 
            return;
        }

        if (vehicle == null || obstacleManager == null || feedbackUI == null)
        {
            ClearWarningAsAvoided();
            return;
        }

        var metas = obstacleManager.ActiveMetas;
        if (metas == null || metas.Count == 0)
        {
            ClearWarningAsAvoided();
            return;
        }

        Vector3 carPos = vehicle.transform.position;
        Vector3 carForward = vehicle.transform.forward;
        int currentLane = vehicle.CurrentLane;

        float bestDistance = float.MaxValue;
        GameObject bestObstacle = null;

        for (int i = 0; i < metas.Count; i++)
        {
            var meta = metas[i];

            if (meta.go == null || !meta.go.activeInHierarchy)
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

        /*if (bestObstacle != null)
        {
            int obstacleId = bestObstacle.GetInstanceID();
            _currentlyWarnedObstacleId = obstacleId;
            feedbackUI.SetWarningActive(true);
            TelemetryLogger.Instance?.SetWarningActive(true);
        }
        else
        {
            ClearWarning();
            feedbackUI.SetWarningActive(false);
            TelemetryLogger.Instance?.SetWarningActive(false);
        }*/

        if (bestObstacle != null)
        {
            int obstacleId = bestObstacle.GetInstanceID();

            // Warnung startet neu oder wechselt auf anderes Hindernis
            if (!_warningCurrentlyOn || _currentlyWarnedObstacleId != obstacleId)
            {
                _currentlyWarnedObstacleId = obstacleId;
                _warningCurrentlyOn = true;

                TelemetryLogger.Instance?.RegisterWarningStartedForObstacle(obstacleId);
            }

            if (feedbackUI != null)
                feedbackUI.SetWarningActive(true);
            //TelemetryLogger.Instance?.SetWarningActive(true);
        }
        else
        {
            ClearWarningAsAvoided();
            //ClearWarning();
        }
    }

    private void ClearWarningVisualOnly()
    {
        _warningCurrentlyOn = false;
        _currentlyWarnedObstacleId = -1;

        if (feedbackUI != null)
            feedbackUI.SetWarningActive(false);
    }

    private void ClearWarningAsAvoided()
    {
        if (_warningCurrentlyOn && _currentlyWarnedObstacleId != -1)
        {
            TelemetryLogger.Instance?.RegisterWarningObstacleAvoided(_currentlyWarnedObstacleId);
        }

        ClearWarningVisualOnly();
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

    public void ForceClearWarningAfterHit(int obstacleId)
    {
        if (_warningCurrentlyOn && _currentlyWarnedObstacleId == obstacleId)
        {
            ClearWarningVisualOnly();
        }
    }
}
