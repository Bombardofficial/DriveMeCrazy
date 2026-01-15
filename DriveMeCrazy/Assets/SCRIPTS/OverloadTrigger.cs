using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OverloadTrigger : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool oneShot = true;
    [SerializeField] public int _maxOverloads = 3;

    private bool _triggered = false;

    [SerializeField] private EngineOverloadManager _overloadManager;

    [SerializeField] private static int _overloadCount = 0;

    void Awake()
    {
        _overloadManager = FindObjectOfType<EngineOverloadManager>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_triggered && oneShot)
            return;
        
        Debug.Log($"[OverloadTrigger] Hit with {other.gameObject}");
        
        

        if (Random.Range(0f, 1f) > 0.5 ||_overloadCount >= _maxOverloads)
            return;

        // Adjust this check to your car setup
        if (!other.CompareTag("Driver"))
            return;

        _triggered = true;

        Debug.Log($"[OverloadTrigger] Tag correct {other.gameObject}");

        var passengers = GetPassengersFromCar(other);

        if (passengers.Count == 0)
            return;

        _overloadManager.TriggerOverload(passengers);

        ++_overloadCount;

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
