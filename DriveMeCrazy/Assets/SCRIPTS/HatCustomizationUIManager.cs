using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class HatCustomizationUIManager : MonoBehaviour
{
    [Header("Canvas Root")]
    [SerializeField] private GameObject customizationCanvasRoot; // CharacterCustomCanvas root

    [Header("UI Slots (size 4)")]
    public HatCustomizationSlotUI[] slots;

    [Header("Hat Options")]
    public List<GameObject> hatPrefabs;
    public List<string> hatNames;

    private readonly HatEquipper[] equippers = new HatEquipper[4];
    private readonly PlayerInput[] playerInputs = new PlayerInput[4];
    private readonly int[] selectedHatIndex = new int[4];

    private readonly bool[] ready = new bool[4];
    private bool running;
    private int joinedCountAtRun;

    // Handlers so we can unsubscribe
    private System.Action<InputAction.CallbackContext>[] leftHandlers = new System.Action<InputAction.CallbackContext>[4];
    private System.Action<InputAction.CallbackContext>[] rightHandlers = new System.Action<InputAction.CallbackContext>[4];
    private System.Action<InputAction.CallbackContext>[] confirmHandlers = new System.Action<InputAction.CallbackContext>[4];

    public void RegisterPlayer(int seatIndex, GameObject playerRoot)
    {
        if (seatIndex < 0 || seatIndex >= 4) return;

        equippers[seatIndex] = playerRoot.GetComponentInChildren<HatEquipper>(true);
        playerInputs[seatIndex] = playerRoot.GetComponent<PlayerInput>();

        // default label/hat
        UpdateSlotLabel(seatIndex);
        ApplyHat(seatIndex);

        // if customization already running, hook immediately
        if (running) HookInputsForSeat(seatIndex);
    }

    public IEnumerator RunCustomization(IReadOnlyList<PlayerInput> joinedPlayers, int joinedCount)
    {
        running = true;
        joinedCountAtRun = Mathf.Clamp(joinedCount, 0, 4);

        // reset ready state for joined seats
        for (int i = 0; i < joinedCountAtRun; i++)
            ready[i] = false;

        if (customizationCanvasRoot) customizationCanvasRoot.SetActive(true);

        // Hook inputs for all joined players
        for (int i = 0; i < joinedPlayers.Count && i < 4; i++)
        {
            // register if not already registered via PlayerJoinManager
            if (playerInputs[i] == null && joinedPlayers[i] != null)
                playerInputs[i] = joinedPlayers[i];

            HookInputsForSeat(i);
        }

        // wait until all joined are ready
        while (!AllJoinedReady())
            yield return null;

        // cleanup
        for (int i = 0; i < joinedCountAtRun; i++)
            UnhookInputsForSeat(i);

        if (customizationCanvasRoot) customizationCanvasRoot.SetActive(false);

        running = false;
    }

    private bool AllJoinedReady()
    {
        if (joinedCountAtRun <= 0) return false;
        for (int i = 0; i < joinedCountAtRun; i++)
            if (!ready[i]) return false;
        return true;
    }

    private void HookInputsForSeat(int seatIndex)
    {
        var pi = playerInputs[seatIndex];
        if (!pi) return;

        pi.SwitchCurrentActionMap("UI");

        var left = pi.actions.FindAction("CustomizationLeft", true);
        var right = pi.actions.FindAction("CustomizationRight", true);
        var confirm = pi.actions.FindAction("CustomizationConfirm", true);

        UnhookInputsForSeat(seatIndex);

        leftHandlers[seatIndex] = _ => StepHat(seatIndex, -1);
        rightHandlers[seatIndex] = _ => StepHat(seatIndex, +1);
        confirmHandlers[seatIndex] = _ => ready[seatIndex] = true; // one-way ready (simpler)

        left.performed += leftHandlers[seatIndex];
        right.performed += rightHandlers[seatIndex];
        confirm.performed += confirmHandlers[seatIndex];
    }

    private void UnhookInputsForSeat(int seatIndex)
    {
        var pi = playerInputs[seatIndex];
        if (!pi) return;

        var left = pi.actions.FindAction("CustomizationLeft", false);
        var right = pi.actions.FindAction("CustomizationRight", false);
        var confirm = pi.actions.FindAction("CustomizationConfirm", false);

        if (left != null && leftHandlers[seatIndex] != null) left.performed -= leftHandlers[seatIndex];
        if (right != null && rightHandlers[seatIndex] != null) right.performed -= rightHandlers[seatIndex];
        if (confirm != null && confirmHandlers[seatIndex] != null) confirm.performed -= confirmHandlers[seatIndex];

        leftHandlers[seatIndex] = null;
        rightHandlers[seatIndex] = null;
        confirmHandlers[seatIndex] = null;
    }

    private void StepHat(int seatIndex, int dir)
    {
        if (hatPrefabs == null || hatPrefabs.Count == 0) return;

        int n = hatPrefabs.Count;
        selectedHatIndex[seatIndex] = (selectedHatIndex[seatIndex] + dir) % n;
        if (selectedHatIndex[seatIndex] < 0) selectedHatIndex[seatIndex] += n;

        UpdateSlotLabel(seatIndex);
        ApplyHat(seatIndex);
    }

    private void UpdateSlotLabel(int seatIndex)
    {
        if (slots == null || seatIndex >= slots.Length || slots[seatIndex] == null) return;

        int idx = selectedHatIndex[seatIndex];
        string label = (hatNames != null && idx < hatNames.Count) ? hatNames[idx] : $"Hat {idx}";
        slots[seatIndex].SetHatLabel(label);
    }

    private void ApplyHat(int seatIndex)
    {
        var eq = equippers[seatIndex];
        if (!eq) return;

        eq.hatPrefab = hatPrefabs[selectedHatIndex[seatIndex]];
        eq.EquipHat();
    }
}
