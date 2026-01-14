using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class GameTutorialController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject tutorialCanvasRoot;  // GameTutorialCanvas
    [SerializeField] private GameObject[] panels;            // 4 panels in order
    [SerializeField] private CanvasGroup canvasGroup;        // optional (can be null)

    [Header("Input")]
    [Tooltip("Action map that contains NextTutorial/BackTutorial/SkipTutorial")]
    [SerializeField] private string tutorialActionMapName = "UI";

    private readonly List<PlayerInput> _players = new();
    private readonly List<InputAction> _nextActions = new();
    private readonly List<InputAction> _backActions = new();
    private readonly List<InputAction> _skipActions = new();

    private readonly Dictionary<PlayerInput, string> _previousMaps = new();

    private int _index = 0;
    private bool _running = false;
    private bool _consumeLock = false; // prevents multiple triggers same frame
    private bool _skipRequested = false;

    public bool IsRunning => _running;

    private void Awake()
    {
        // Make sure only one panel is active when enabled
        HideAllPanels();
        if (tutorialCanvasRoot) tutorialCanvasRoot.SetActive(false);

        if (!canvasGroup && tutorialCanvasRoot)
            canvasGroup = tutorialCanvasRoot.GetComponent<CanvasGroup>();
    }

    /// <summary>
    /// Starts the tutorial and waits until it ends (via skip or last panel -> next).
    /// Call with the list of joined PlayerInput components.
    /// </summary>
    public IEnumerator RunTutorial(IReadOnlyList<PlayerInput> joinedPlayers)
    {
        Debug.Log($"[Tutorial] RunTutorial called. canvasRoot={tutorialCanvasRoot}, panels={panels?.Length}");


        if (_running) yield break;

        if (tutorialCanvasRoot == null)
        {
            Debug.LogWarning("[GameTutorialController] tutorialCanvasRoot is not assigned.");
            yield break;
        }
        if (panels == null || panels.Length == 0)
        {
            Debug.LogWarning("[GameTutorialController] panels array is empty.");
            yield break;
        }

        _running = true;
        _skipRequested = false;
        _consumeLock = false;

        // Cache players
        _players.Clear();
        for (int i = 0; i < joinedPlayers.Count; i++)
        {
            if (joinedPlayers[i] != null) _players.Add(joinedPlayers[i]);
        }

        // Enable UI + show first panel
        tutorialCanvasRoot.SetActive(true);
        if (canvasGroup) canvasGroup.alpha = 1f;

        _index = 0;
        ShowPanel(_index);

        // Switch maps + subscribe to actions for all players
        HookInputs();

        // Wait until finished
        while (_running && !_skipRequested)
            yield return null;

        // Cleanup
        UnhookInputs();
        HideAllPanels();
        tutorialCanvasRoot.SetActive(false);

        _running = false;
    }

    private void HookInputs()
    {
        _previousMaps.Clear();
        _nextActions.Clear();
        _backActions.Clear();
        _skipActions.Clear();

        foreach (var pi in _players)
        {
            if (!pi) continue;

            // Store current map and switch to UI/tutorial map
            _previousMaps[pi] = pi.currentActionMap != null ? pi.currentActionMap.name : "";
            if (!string.IsNullOrEmpty(tutorialActionMapName))
            {
                try { pi.SwitchCurrentActionMap(tutorialActionMapName); }
                catch { Debug.LogWarning($"[GameTutorialController] Could not switch {pi.name} to map {tutorialActionMapName}."); }
            }

            // Find actions by name (same pattern as SabotageInputHandler)
            var next = pi.actions.FindAction("NextTutorial", true);
            var back = pi.actions.FindAction("BackTutorial", true);
            var skip = pi.actions.FindAction("SkipTutorial", true);

            // Subscribe
            if (next != null)
            {
                next.performed += OnNext;
                _nextActions.Add(next);
            }
            if (back != null)
            {
                back.performed += OnBack;
                _backActions.Add(back);
            }
            if (skip != null)
            {
                skip.performed += OnSkip;
                _skipActions.Add(skip);
            }
        }
    }

    private void UnhookInputs()
    {
        foreach (var a in _nextActions) if (a != null) a.performed -= OnNext;
        foreach (var a in _backActions) if (a != null) a.performed -= OnBack;
        foreach (var a in _skipActions) if (a != null) a.performed -= OnSkip;

        // Restore action maps
        foreach (var kvp in _previousMaps)
        {
            var pi = kvp.Key;
            var prev = kvp.Value;
            if (!pi) continue;
            if (string.IsNullOrEmpty(prev)) continue;

            try { pi.SwitchCurrentActionMap(prev); }
            catch { /* ignore */ }
        }

        _previousMaps.Clear();
    }

    private void OnNext(InputAction.CallbackContext ctx)
    {
        if (_consumeLock) return;
        StartCoroutine(ConsumeThisFrame());

        // last panel -> finish
        if (_index >= panels.Length - 1)
        {
            _skipRequested = true; // ends loop
            return;
        }

        _index++;
        ShowPanel(_index);
    }

    private void OnBack(InputAction.CallbackContext ctx)
    {
        if (_consumeLock) return;
        StartCoroutine(ConsumeThisFrame());

        _index = Mathf.Max(0, _index - 1);
        ShowPanel(_index);
    }

    private void OnSkip(InputAction.CallbackContext ctx)
    {
        if (_consumeLock) return;
        StartCoroutine(ConsumeThisFrame());

        _skipRequested = true;
    }

    private IEnumerator ConsumeThisFrame()
    {
        _consumeLock = true;
        yield return null;
        _consumeLock = false;
    }

    private void HideAllPanels()
    {
        if (panels == null) return;
        for (int i = 0; i < panels.Length; i++)
        {
            if (panels[i]) panels[i].SetActive(false);
        }
    }

    private void ShowPanel(int i)
    {
        HideAllPanels();
        if (i >= 0 && i < panels.Length && panels[i])
            panels[i].SetActive(true);
    }
}

