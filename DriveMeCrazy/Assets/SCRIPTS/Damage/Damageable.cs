using UnityEngine;

[RequireComponent(typeof(PlayerManager))]
public class Damageable : MonoBehaviour
{
    public float maxHealth = 15f;
    public float damageFactor = 5f;

    float _health;
    PlayerManager _pm;
    public float Health => _health;
    void Awake()
    {
        _health = maxHealth;
        _pm = GetComponent<PlayerManager>();
    }

    public void InflictDamage(float amount = -1f)
    {
        _health -= (amount > 0 ? amount : damageFactor);
        if (_health > 0 || !_pm) return;

        int target = Random.Range(0, _pm.Passengers.Count);

        //int target = (_pm.Passengers.Count > 1) ? 1 : 0;
        _pm.SwapWithDriver(target);

        _health = maxHealth;
    }
}
