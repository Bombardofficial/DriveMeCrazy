using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class IconLibrary
{
    private static OverloadIconLibrary _instance;

    public static void Initialize(OverloadIconLibrary asset)
    {
        _instance = asset;
    }

    public static Sprite Get(OverloadControlType type)
    {
        if (_instance == null)
        {
            Debug.LogError("[IconLibrary] Not initialized!");
            return null;
        }

        return _instance.GetIcon(type);
    }
}
