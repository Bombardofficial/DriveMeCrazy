using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

public class Collectable : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"Trigger entered by: {other.name}");

        Collector collector = other.GetComponent<Collector>();
        if (collector != null)
        {
            Debug.Log("Damageable component found. Inflicting damage.");
            collector.IncrementPoints();
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
        yield return new WaitForSeconds(1.0f); // 1 second cooldown
    }
}