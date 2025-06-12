using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Collector : MonoBehaviour
{
    private PlayerManager playerManager;

    public void IncrementPoints()
    {
        playerManager.IncrementDriverPoints();
    }

    void Start()
    {
        playerManager = GetComponent<PlayerManager>();
    }
}