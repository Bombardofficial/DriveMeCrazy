using System.Collections;
using UnityEngine;

public class ObstacleTrigger : MonoBehaviour
{
    private bool hasRecentlyDamaged = false;

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"Trigger entered by: {other.name}");

        Damageable damageable = other.GetComponent<Damageable>();
        if (damageable != null && !hasRecentlyDamaged)
        {
            Debug.Log("Damageable component found. Inflicting damage.");
            damageable.InflictDamage();
            gameObject.SetActive(false);
            //StartCoroutine(DamageCooldown());
        }
        else
        {
            Debug.LogWarning("No Damageable component found on collided object.");
        }
    }

    private IEnumerator DamageCooldown()
    {
        hasRecentlyDamaged = true;
        yield return new WaitForSeconds(1.0f); // 1 second cooldown
        hasRecentlyDamaged = false;
    }

}