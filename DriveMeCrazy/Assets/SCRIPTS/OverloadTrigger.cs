using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OverloadTrigger : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool oneShot = true;

    private bool _triggered;

    private EngineOverloadManager _overloadManager;

    void Awake()
    {
        _overloadManager = FindObjectOfType<EngineOverloadManager>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_triggered && oneShot)
            return;

        Debug.Log($"[OverloadTrigger] Hit with {other.gameObject}");

        // Adjust this check to your car setup
        if (!other.CompareTag("Driver"))
            return;

        Debug.Log($"[OverloadTrigger] Tag correct {other.gameObject}");

        var passengers = GetPassengersFromCar(other);

        if (passengers.Count == 0)
            return;

        _overloadManager.TriggerOverload(passengers);

        _triggered = true;
        gameObject.SetActive(false);
    }

    private List<Passenger> GetPassengersFromCar(Collider carCollider)
    {
        var manager = carCollider.GetComponentInParent<PlayerManager>();
        if (manager == null)
            return new List<Passenger>();

        return new List<Passenger>(manager.Passengers);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero, GetComponent<BoxCollider>().size);
    }
#endif
}
