using System.Collections.Generic;
using UnityEngine;

public class HatCustomizationUIManager : MonoBehaviour
{
    [Header("UI Slots (size 4)")]
    public HatCustomizationSlotUI[] slots;

    [Header("Hat Options")]
    public List<GameObject> hatPrefabs;  // index 0 could be "None" (null or empty prefab)
    public List<string> hatNames;

    // per-player runtime state
    private HatEquipper[] equippers = new HatEquipper[4];
    private int[] selectedHatIndex = new int[4];

    void Awake()
    {
        for (int i = 0; i < slots.Length; i++)
            slots[i].Init(this);
    }

    // Call this when a player is spawned/assigned to a seat
    public void RegisterPlayer(int seatIndex, GameObject playerRoot)
    {
        equippers[seatIndex] = playerRoot.GetComponentInChildren<HatEquipper>(true);

        // refresh UI immediately
        UpdateSlotLabel(seatIndex);
        ApplyHat(seatIndex);
    }

    public void StepHat(int seatIndex, int dir)
    {
        if (hatPrefabs == null || hatPrefabs.Count == 0) return;

        int n = hatPrefabs.Count;
        selectedHatIndex[seatIndex] = (selectedHatIndex[seatIndex] + dir) % n;
        if (selectedHatIndex[seatIndex] < 0) selectedHatIndex[seatIndex] += n;

        UpdateSlotLabel(seatIndex);
        ApplyHat(seatIndex);
    }

    void UpdateSlotLabel(int seatIndex)
    {
        int idx = selectedHatIndex[seatIndex];
        string label = (hatNames != null && idx < hatNames.Count) ? hatNames[idx] : $"Hat {idx}";
        slots[seatIndex].SetHatLabel(label);
    }

    void ApplyHat(int seatIndex)
    {
        var eq = equippers[seatIndex];
        if (!eq) return;

        var prefab = hatPrefabs[selectedHatIndex[seatIndex]];

        eq.hatPrefab = prefab;
        eq.EquipHat();
    }
}
