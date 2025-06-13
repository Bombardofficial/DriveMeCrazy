using System.Collections;
using UnityEngine;
using ArcadeVP; // Add this line to access the car controller namespace

/// <summary>
/// A physics-based obstacle. It is a dynamic object from the start.
/// When hit by a kinematic Rigidbody (like the player car), it manually
/// calculates the impact force and sends itself flying before being returned to the pool.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class Obstacle : MonoBehaviour
{
    [HideInInspector]
    public ObstacleManager manager; // Assigned by the ObstacleManager

    [HideInInspector]
    public int damageToInflict;

    [Header("Physics")]
    [Tooltip("How much to multiply the impact force by. Necessary because the player car is kinematic. Try values between 10 and 40.")]
    public float forceMultiplier = 25f; // Adjusted to a more reasonable default
    [Tooltip("How much upward force to add to make the object pop into the air.")]
    [Range(0f, 1f)] public float upwardForceBias = 0.3f;


    [Header("Lifetime")]
    [Tooltip("How many seconds after being hit before it returns to the pool. This is the value you can adjust.")]
    public float returnToPoolDelay = 5.0f;

    private Rigidbody _rb;
    private bool _hasBeenHit = false;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        // NOTE: For this script to work, the Rigidbody's "Collision Detection" on the prefab
        // MUST be set to "Continuous" or "Continuous Dynamic".
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Ignore collisions if this obstacle has already been hit.
        if (_hasBeenHit) return;

        // We now check for the car's controller first to handle the collision.
        if (collision.gameObject.TryGetComponent<ArcadeVehicleController>(out ArcadeVehicleController carController))
        {
            // --- DEBUGGING LINE ---
            // If you still pass through obstacles, check if this log appears in the console.
            // If it doesn't, the collision is not being detected by the physics engine.
            Debug.Log($"Obstacle '{name}' hit by car. Applying force.", gameObject);

            _hasBeenHit = true;

            // 1. Inflict damage on the player (if the car has the Damageable component).
            if (collision.gameObject.TryGetComponent<Damageable>(out Damageable playerDamage))
            {
                playerDamage.InflictDamage(damageToInflict);
            }

            // 2. --- REVISED AND CORRECTED FORCE CALCULATION ---
            Vector3 carVelocity = carController.CurrentVelocity;

            // The primary force direction is now the car's forward velocity.
            Vector3 forceDirection = carVelocity.normalized;

            // We add a controlled amount of upward force. This makes it pop up, not fly straight up.
            forceDirection = (forceDirection + Vector3.up * upwardForceBias).normalized;

            // Use the car's speed as the base for the force magnitude.
            float impactMagnitude = carVelocity.magnitude;

            // Apply the calculated force.
            _rb.AddForce(forceDirection * impactMagnitude * forceMultiplier, ForceMode.Impulse);

            // 3. Start the countdown to return this object to the pool.
            StartCoroutine(ReturnToPoolAfterDelay());
        }
    }

    private IEnumerator ReturnToPoolAfterDelay()
    {
        yield return new WaitForSeconds(returnToPoolDelay);
        if (manager != null)
        {
            // The manager's name for this function might be ReturnObstacleToPool
            manager.ReturnObstacleToPool(gameObject);
        }
        else
        {
            // Fallback if the manager is somehow lost.
            gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Called by the manager to reset the obstacle's state when it's reused from the pool.
    /// </summary>
    public void ResetState()
    {
        _hasBeenHit = false;

        // Stop all physical movement from the previous collision.
        _rb.velocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;

        // Reset rotation to ensure it spawns upright.
        transform.rotation = Quaternion.identity;
    }
}
