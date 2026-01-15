using UnityEngine;

[RequireComponent(typeof(PlayerManager))]
public class Damageable : MonoBehaviour
{
    public float maxHealth = 15f;

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

        _pm.ShowDamageDone(damageAmount/maxHealth * 100f);

        if (_health > 0 || !_pm) return;

        _pm.SwapDriverSabotage();
        
        _health = maxHealth;
    }
}
