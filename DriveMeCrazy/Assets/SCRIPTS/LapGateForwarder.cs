using UnityEngine;

/// <summary>
/// Sits on the Lap Gate.  Forwards only collisions that come from the
/// car object controlled by PlayerJoinManager.  Filter is by layer, so
/// you never depend on tags or object names.
/// </summary>
[RequireComponent(typeof(Collider))]
public class LapGateForwarder : MonoBehaviour
{
    [Tooltip("Drag the active PlayerJoinManager here")]
    [SerializeField] private PlayerJoinManager mgr;

    // Tiny helper so we don’t recompute each time
    private int carLayerMask;
    public GameObject fireworks;
    void Awake()
    {
        if (!mgr)
            mgr = FindObjectOfType<PlayerJoinManager>();   // last-chance auto-hook

        if (!mgr)
        {
            Debug.LogError("[LapGateForwarder] No PlayerJoinManager assigned!");
            enabled = false;
            return;
        }

        // Cache a bitmask for ultra-fast comparison
        carLayerMask = 1 << mgr.CarLayer;

        // Make sure *this* collider is trigger so the messages fire
        var col = GetComponent<Collider>();
        if (!col.isTrigger)
            col.isTrigger = true;

        fireworks.SetActive(false);
    }

    void OnTriggerEnter(Collider other)
    {
        int otherMask = 1 << other.gameObject.layer;
        Debug.Log($"[LapGate] hit by {other.name} (layer {LayerMask.LayerToName(other.gameObject.layer)})");

        if ((otherMask & carLayerMask) == 0)       // not the player car
            return;
        fireworks.SetActive(true);
        // *** count the lap! ***
        mgr.LapGateCrossed();                      // ? one clean, type-safe call
    }
}
