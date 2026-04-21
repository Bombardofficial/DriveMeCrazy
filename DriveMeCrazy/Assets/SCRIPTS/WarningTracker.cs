using ArcadeVP;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;
using static TMPro.SpriteAssetUtilities.TexturePacker_JsonArray;

public class WarningTracker : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ArcadeVehicleController vehicle;
    [SerializeField] private FeedbackIntensityUI feedbackUI;

    private bool _warningCurrentlyOn = false;
    private Obstacle _currentObstacleInZone = null;
    private GameObject _currentWarnedObstacle = null; 

    private void Reset()
    {
        vehicle = GetComponent<ArcadeVehicleController>();
    }

    private void Update()
    {
        if (_currentObstacleInZone != null)
        {
            CheckAndTrigger();
        }
    }

    private void CheckAndTrigger()
    {
        // Check: Sind wir auf der gleichen Spur?
        if (vehicle.CurrentLane == _currentObstacleInZone.currentLane)
        {
            if (!_warningCurrentlyOn) TriggerWarning(true, _currentObstacleInZone.id);
        }
        else
        {
            // Wenn wir in der Box sind, aber die Spur gewechselt haben
            if (_warningCurrentlyOn) ClearWarning();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        //if (!PlayerJoinManager.IsRaceStarted) return;
        if (!_warningCurrentlyOn)
            feedbackUI?.SetWarningActive(true);

        // Prüfen, ob wir in den collider eines Hindernisses gefahren sind
        Obstacle obs = other.GetComponent<Obstacle>();
        Debug.Log("Trigger Entered: " + (obs != null ? "Obstacle gefunden!" : "Kein Obstacle-Script am Trigger-Objekt")); 
        
        if (obs != null && !obs.HasBeenHit)
        {
            _currentObstacleInZone = obs;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (_warningCurrentlyOn) 
            feedbackUI?.SetWarningActive(false);

        Obstacle obs = other.GetComponent<Obstacle>();

        Debug.Log("Trigger verlassen von: " + other.name);

        if (obs != null && obs == _currentObstacleInZone)
        {
            _currentObstacleInZone = null;
            //if (_warningCurrentlyOn) ClearWarning();
        }
    }

    private void CheckWarnings()
    {
        // Wir prüfen jeden Frame nur noch: Sind wir in einer Zone?
        // Wenn ja, sind wir auf derselben Spur wie das Hindernis?
        if (_currentObstacleInZone != null && !_currentObstacleInZone.HasBeenHit)
        {
            // Vergleiche die Spur des Autos mit der Spur des Hindernisses
            if (vehicle.CurrentLane == _currentObstacleInZone.currentLane)
            {
                if (!_warningCurrentlyOn) TriggerWarning(true, _currentObstacleInZone.id);
            }
            else
            {
                // Wir sind im Kreis, aber auf einer anderen Spur -> Warnung aus
                if (_warningCurrentlyOn) ClearWarning();
            }
        }
        else
        {
            if (_warningCurrentlyOn) ClearWarning();
        }

        /*
        // Prüfe nur auf derselben Spur
        if (meta.lane != currentLane) continue;

        var obsComp = meta.go.GetComponent<Obstacle>();
        bestObstacleID = (obsComp != null) ? obsComp.id : -1;
        */
    }



    // Erweitertes Trigger-System
    private void TriggerWarning(bool active, int obstacleID)
    {
        _warningCurrentlyOn = active;
        feedbackUI?.SetWarningActive(active);
        TelemetryLogger.Instance?.SetWarningActive(active);

        // Telemetrie: Logge jetzt mit ID
        // TelemetryLogger.Instance?.LogWarningEvent(obstacleID, active);
        // --> Set warning umschreiben dass es auch die ID mitliefert
    }

    private void ClearWarning()
    {
        _currentWarnedObstacle = null; // Reset des getrackten Hindernisses
        TriggerWarning(false, 0);
    }

    public void ForceClearWarningAfterHit()
    {
        _currentWarnedObstacle = null;
        ClearWarning();
    }
}
