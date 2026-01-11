using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(
    fileName = "OverloadIconLibrary",
    menuName = "UI/Overload Icon Library"
)]
public class OverloadIconLibrary : ScriptableObject
{
    [System.Serializable]
    public struct IconEntry
    {
        public OverloadControlType controlType;
        public Sprite icon;
    }

    [SerializeField]
    private List<IconEntry> icons = new();

    private Dictionary<OverloadControlType, Sprite> _lookup;

    private void OnEnable()
    {
        _lookup = new Dictionary<OverloadControlType, Sprite>();
        foreach (var entry in icons)
        {
            _lookup[entry.controlType] = entry.icon;
        }
    }

    public Sprite GetIcon(OverloadControlType type)
    {
        if (_lookup != null && _lookup.TryGetValue(type, out var sprite))
            return sprite;

        Debug.LogWarning($"[IconLibrary] Missing icon for {type}");
        return null;
    }
}