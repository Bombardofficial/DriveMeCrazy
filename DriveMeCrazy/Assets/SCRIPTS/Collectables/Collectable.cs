using System.Collections;
using UnityEngine;

public class Collectable : MonoBehaviour
{
    // ---- NEW ----
    // This public reference will be set by the CollectableManager when it creates the pool.
    [HideInInspector]
    public CollectableManager manager;
    // -------------

    private void OnTriggerEnter(Collider other)
    {
        // Check if the object that hit this is the car (which has the Collector component)
        Collector collector = other.GetComponentInParent<Collector>();
        if (collector != null)
        {
            // Award points to the driver
            collector.IncrementPoints();

            // ---- MODIFIED ----
            // Instead of just deactivating, tell the manager to handle it.
            // This ensures the active count is managed correctly.
            if (manager != null)
            {
                manager.ReturnCollectableToPool(gameObject);
            }
            else
            {
                // Fallback for safety, though the manager should always be assigned.
                gameObject.SetActive(false);
            }
            // ------------------
        }
    }
}
