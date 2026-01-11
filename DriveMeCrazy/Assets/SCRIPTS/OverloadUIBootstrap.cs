using UnityEngine;

public class OverloadUIBootstrap : MonoBehaviour
{
    [SerializeField] private OverloadIconLibrary overloadIcons;

    void Awake()
    {
        IconLibrary.Initialize(overloadIcons);
    }
}
