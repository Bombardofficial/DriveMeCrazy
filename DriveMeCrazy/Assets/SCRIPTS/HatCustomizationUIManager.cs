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

    //[SerializeField] PlayerJoinManager playerJoinManager;

    [Header("Action Map + Actions")]
    [SerializeField] private string customizationActionMap = "Lobby"; // IMPORTANT: must match your InputActions
    [SerializeField] private string actionLeft = "CustomizationLeft";
    [SerializeField] private string actionRight = "CustomizationRight";
    [SerializeField] private string actionConfirm = "CustomizationConfirm";


    [Header("Hat Options")]
    public List<GameObject> hatPrefabs;
    public List<string> hatNames;

    private readonly HatEquipper[] equippers = new HatEquipper[4];
    private readonly PlayerInput[] playerInputs = new PlayerInput[4];
    private readonly int[] selectedHatIndex = new int[4];

    private readonly bool[] ready = new bool[4];
    private bool running;
    //private int joinedCountAtRun;

    // which seats are participating THIS run
    private readonly List<int> activeSeats = new List<int>(4);

    private readonly HatEquipper[] dummyEquippers = new HatEquipper[4];

    // Handlers so we can unsubscribe
    private System.Action<InputAction.CallbackContext>[] leftHandlers = new System.Action<InputAction.CallbackContext>[4];
    private System.Action<InputAction.CallbackContext>[] rightHandlers = new System.Action<InputAction.CallbackContext>[4];
    private System.Action<InputAction.CallbackContext>[] confirmHandlers = new System.Action<InputAction.CallbackContext>[4];


    private void Awake()
    {
        if (customizationCanvasRoot != null)
            customizationCanvasRoot.SetActive(false);
        else
            gameObject.SetActive(false);
    }

    public void RegisterPlayer(int seatIndex, GameObject playerRoot)
    {
        if (seatIndex < 0 || seatIndex >= 4) return;

        equippers[seatIndex] = playerRoot.GetComponentInChildren<HatEquipper>(true);

        // Only set playerInputs[] if this object actually has a PlayerInput.
        var pi = playerRoot.GetComponent<PlayerInput>();
        if (pi != null)
            playerInputs[seatIndex] = pi;

        UpdateSlotLabel(seatIndex);

        // Apply hat to BOTH: real player (if exists) and dummy (if exists)
        ApplyHat(seatIndex);

        if (running && playerInputs[seatIndex] != null)
            HookInputsForSeat(seatIndex);

    /*if (seatIndex < 0 || seatIndex >= 4) return;

    equippers[seatIndex] = playerRoot.GetComponentInChildren<HatEquipper>(true);
    playerInputs[seatIndex] = playerRoot.GetComponent<PlayerInput>();

    // default label/hat
    UpdateSlotLabel(seatIndex);
    ApplyHat(seatIndex);

    // if customization already running, hook immediately
    if (running) HookInputsForSeat(seatIndex);
    */
}

public void RegisterDummy(int seatIndex, GameObject dummyRoot)
    {
        if (seatIndex < 0 || seatIndex >= 4) return;

        dummyEquippers[seatIndex] = dummyRoot.GetComponentInChildren<HatEquipper>(true);

        // Apply current selection immediately to the dummy (visual preview)
        ApplyHatToEquipper(dummyEquippers[seatIndex], seatIndex);
    }


    public IEnumerator RunCustomization(IReadOnlyList<PlayerInput> joinedPlayers, int joinedCount)
    {
        running = true;
        //joinedCountAtRun = Mathf.Clamp(joinedCount, 0, 4);

        // reset ready state for joined seats
        /*for (int i = 0; i < joinedCountAtRun; i++)
            ready[i] = false;
        */

        activeSeats.Clear();
        for (int seat = 0; seat < 4; seat++)
        {
            if (playerInputs[seat] != null)
                activeSeats.Add(seat);
        }

        if (activeSeats.Count == 0)
        {
            Debug.LogWarning("[HatCustomizationUI] No active players registered; skipping customization.");
            running = false;
            yield break;
        }

        // reset ready state only for active seats
        foreach (var seat in activeSeats)
            ready[seat] = false;

        if (customizationCanvasRoot) customizationCanvasRoot.SetActive(true);

        // Hook inputs based on SEAT INDEX, not FindObjectsOfType order
        /*for (int seat = 0; seat < joinedCountAtRun; seat++)
        {
            if (playerInputs[seat] == null)
            {
                Debug.LogWarning($"[HatCustomizationUI] playerInputs[{seat}] is null. Did RegisterPlayer run?");
                continue;
            }

            HookInputsForSeat(seat);
        }
        */

        // hook inputs for active seats only
        foreach (var seat in activeSeats)
            HookInputsForSeat(seat);


        // wait until all joined are ready
        while (!AllJoinedReady())
            yield return null;

        // cleanup
        /*for (int i = 0; i < joinedCountAtRun; i++)
            UnhookInputsForSeat(i);
        */

        // cleanup
        foreach (var seat in activeSeats)
            UnhookInputsForSeat(seat);

        if (customizationCanvasRoot) customizationCanvasRoot.SetActive(false);

        running = false;
    }

    private bool AllJoinedReady()
    {
        /*if (joinedCountAtRun <= 0) return false;
        for (int i = 0; i < joinedCountAtRun; i++)
            if (!ready[i]) return false;
        */

        foreach (var seat in activeSeats)
        {
            if (!ready[seat]) return false;
        }

        return true;
    }

    private void HookInputsForSeat(int seatIndex)
    {
        var pi = playerInputs[seatIndex];
        if (!pi) return;

        //pi.SwitchCurrentActionMap("Lobby");
        pi.SwitchCurrentActionMap(customizationActionMap);


        /*var left = pi.actions.FindAction("CustomizationLeft", true);
        var right = pi.actions.FindAction("CustomizationRight", true);
        var confirm = pi.actions.FindAction("CustomizationConfirm", true);

        UnhookInputsForSeat(seatIndex);

        leftHandlers[seatIndex] = _ => StepHat(seatIndex, -1);
        rightHandlers[seatIndex] = _ => StepHat(seatIndex, +1);
        confirmHandlers[seatIndex] = _ => ready[seatIndex] = true; // one-way ready (simpler)

        left.performed += leftHandlers[seatIndex];
        right.performed += rightHandlers[seatIndex];
        confirm.performed += confirmHandlers[seatIndex];
        */

        // Get actions from the PlayerInput's action asset
        var left = pi.actions.FindAction(actionLeft, true);
        var right = pi.actions.FindAction(actionRight, true);
        var confirm = pi.actions.FindAction(actionConfirm, true);

        UnhookInputsForSeat(seatIndex);

        leftHandlers[seatIndex] = _ => StepHat(seatIndex, -1);
        rightHandlers[seatIndex] = _ => StepHat(seatIndex, +1);
        confirmHandlers[seatIndex] = _ =>
        {
            ready[seatIndex] = true;
            Debug.Log($"[HatCustomizationUI] Seat {seatIndex} CONFIRMED ({pi.devices[0].displayName})");
        };

        left.performed += leftHandlers[seatIndex];
        right.performed += rightHandlers[seatIndex];
        confirm.performed += confirmHandlers[seatIndex];

        Debug.Log($"[HatCustomizationUI] Hooked seat {seatIndex} on map '{customizationActionMap}'");
}

    private void UnhookInputsForSeat(int seatIndex)
    {
        var pi = playerInputs[seatIndex];
        if (!pi) return;

        /*var left = pi.actions.FindAction("CustomizationLeft", false);
        var right = pi.actions.FindAction("CustomizationRight", false);
        var confirm = pi.actions.FindAction("CustomizationConfirm", false);
        */

        var left = pi.actions.FindAction(actionLeft, false);
        var right = pi.actions.FindAction(actionRight, false);
        var confirm = pi.actions.FindAction(actionConfirm, false);

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

    /*
    private void ApplyHat(int seatIndex)
    {
        var eq = equippers[seatIndex];
        if (!eq) return;

        eq.hatPrefab = hatPrefabs[selectedHatIndex[seatIndex]];
        eq.EquipHat();
    }
    */

    private void ApplyHat(int seatIndex)
    {
        ApplyHatToEquipper(equippers[seatIndex], seatIndex);
        ApplyHatToEquipper(dummyEquippers[seatIndex], seatIndex);
    }

    private void ApplyHatToEquipper(HatEquipper eq, int seatIndex)
    {
        if (!eq) return;
        eq.hatPrefab = hatPrefabs[selectedHatIndex[seatIndex]];
        eq.EquipHat();
    }

    public void ApplySelectionsToAllRegisteredPlayers()
    {
        for (int seat = 0; seat < 4; seat++)
            ApplyHat(seat);
    }

}
