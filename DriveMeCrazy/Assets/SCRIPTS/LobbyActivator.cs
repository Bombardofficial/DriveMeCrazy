using UnityEngine;

public class LobbyActivator : MonoBehaviour
{
    void Awake()
    {
        // turn UI + manager on as soon as the gameplay scene loads
        gameObject.SetActive(true);
        // also enable the PlayerJoinPanel
        var panel = GameObject.Find("PlayerJoinPanel");
        if (panel) panel.SetActive(true);
    }
}
