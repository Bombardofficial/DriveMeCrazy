using ArcadeVP;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class WarningTracker : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ArcadeVehicleController vehicle;
    [SerializeField] private FeedbackIntensityUI feedbackUI;

    private readonly Dictionary<int, Obstacle> _obstaclesInZone = new();

    private bool _warningCurrentlyOn;
    private int _activeWarningObstacleId = -1;

    private void Reset()
    {
        vehicle = GetComponentInParent<ArcadeVehicleController>();
    }

    private void Update()
    {
        RefreshWarningState();
    }

    private void RefreshWarningState()
    {
        RemoveInvalidObstacles();

        Obstacle bestObstacle = GetBestObstacleForCurrentLane();

        if (bestObstacle == null)
        {
            ClearWarning();
            return;
        }

        if (!_warningCurrentlyOn || _activeWarningObstacleId != bestObstacle.Id)
        {
            ActivateWarning(bestObstacle);
        }
    }

    private void RemoveInvalidObstacles()
    {
        List<int> idsToRemove = null;

        foreach (var kvp in _obstaclesInZone)
        {
            Obstacle obs = kvp.Value;
            if (obs == null || !obs.gameObject.activeInHierarchy || obs.HasBeenHit)
            {
                idsToRemove ??= new List<int>();
                idsToRemove.Add(kvp.Key);
            }
        }

        if (idsToRemove == null) return;

        foreach (int id in idsToRemove)
            _obstaclesInZone.Remove(id);
    }

    private Obstacle GetBestObstacleForCurrentLane()
    {
        Obstacle best = null;
        float bestSqrDistance = float.MaxValue;
        int carLane = GetWarningComparableCarLane();

        foreach (var kvp in _obstaclesInZone)
        {
            Obstacle obs = kvp.Value;
            if (obs == null || obs.HasBeenHit) continue;
            //Debug.Log($"LUCY - CHECKING OBSTACLE | id={obs.Id} obsLane={obs.CurrentLane} carLane={carLane}");
            if (obs.CurrentLane != carLane) continue;

            float sqrDist = (obs.transform.position - vehicle.transform.position).sqrMagnitude;
            if (sqrDist < bestSqrDistance)
            {
                bestSqrDistance = sqrDist;
                best = obs;
            }
        }

        return best;
    }

    private int GetWarningComparableCarLane()
    {
        int lane = vehicle.CurrentLane;

        // Für 3 Spuren:
        // Vehicle-Konvention ist gespiegelt gegenüber Obstacle-/World-Konvention
        // 0 <-> 2, 1 bleibt 1
        return 2 - lane;
    }

    private void ActivateWarning(Obstacle obstacle)
    {
        //Debug.Log($"LUCY - WARNING ON | obstacleId={obstacle.Id} obstacleLane={obstacle.CurrentLane} carLane={vehicle.CurrentLane}");

        _warningCurrentlyOn = true;
        _activeWarningObstacleId = obstacle.Id;

        feedbackUI?.SetWarningActive(true);
        TelemetryLogger.Instance?.SetWarningActive(true, obstacle.Id);
    }

    private void ClearWarning()
    {
        if (!_warningCurrentlyOn)
            return;

        _warningCurrentlyOn = false;
        _activeWarningObstacleId = -1;

        feedbackUI?.SetWarningActive(false);
        TelemetryLogger.Instance?.SetWarningActive(false, -1);
    }


    private void OnTriggerEnter(Collider other)
    {
        Obstacle obs = other.GetComponentInParent<Obstacle>();
        if (obs == null) return;
        if (obs.HasBeenHit) return;

        //Debug.Log($"LUCY - TRIGGER ENTER | other={other.name} obstacleId={obs.Id} obstacleLane={obs.CurrentLane} carLane={vehicle.CurrentLane}");
        _obstaclesInZone[obs.Id] = obs;
    }

    private void OnTriggerExit(Collider other)
    {
        Obstacle obs = other.GetComponentInParent<Obstacle>();
        if (obs == null) return;

        //Debug.Log($"LUCY - TRIGGER EXIT | other={other.name} obstacleId={obs.Id} obstacleLane={obs.CurrentLane} carLane={vehicle.CurrentLane}");
        _obstaclesInZone.Remove(obs.Id);

        if (_activeWarningObstacleId == obs.Id)
            RefreshWarningState();
    }

    public void NotifyObstacleHit(Obstacle obstacle)
    {
        if (obstacle == null) return;

        _obstaclesInZone.Remove(obstacle.Id);

        if (_activeWarningObstacleId == obstacle.Id)
            RefreshWarningState();
    }

    public void ForceClearWarningAfterHit()
    {
        ClearWarning();
    }
}
