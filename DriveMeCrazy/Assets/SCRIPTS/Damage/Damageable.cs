using UnityEngine;

[RequireComponent(typeof(PlayerManager))]
public class Damageable : MonoBehaviour
{
    public float maxHealth = 15f;
    // We no longer need damageFactor, as the damage is now defined
    // on the obstacle that hits the player.

    private float _health;
    private PlayerManager _pm;
    public float Health => _health;

    void Awake()
    {
        _health = maxHealth;
        _pm = GetComponent<PlayerManager>();
    }

    /// <summary>
    /// Inflicts a specific amount of damage to this object.
    /// This method is now simpler and directly uses the value passed to it.
    /// </summary>
    /// <param name="damageAmount">The amount of health to lose.</param>
    public void InflictDamage(int damageAmount)
    {
        // Directly subtract the damage from the obstacle.
        _health -= damageAmount;

        if (_health > 0 || !_pm) return;

        // Logic for when health runs out.
        int target = Random.Range(0, _pm.Passengers.Count);
        _pm.SwapWithDriver(target);
        _health = maxHealth;
    }
}
