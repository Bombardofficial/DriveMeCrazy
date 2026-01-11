using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class OverloadButton
{
    public int _buttonId;
    public int _assignedPlayerId = -1; // -1 = unassigned
    public bool _isHeld => _assignedPlayerId != -1;
    public OverloadControlType _buttonType;

    public OverloadButton(int id)
    {
        _buttonId = id;
    }
}

public enum OverloadControlType
{
    FaceSouth,
    FaceNorth,
    FaceEast,
    FaceWest,
    DpadUp,
    DpadDown,
    DpadLeft,
    DpadRight,
    LeftShoulder,
    RightShoulder,
    LeftTrigger,
    RightTrigger
}

public class EngineOverloadManager : MonoBehaviour
{
    [Header("Settings")]
    public float _overloadDuration = 4f;
    public int _successPointBonus = 50;
    public int _failurePointPenalty = 30;

    private float _timer;
    private bool _isActive;

    private List<int> _passengerPlayerIds = new();
    private Dictionary<int, OverloadButton> _buttons = new();
    private Dictionary<int, int> _playerToButton = new();

    // External references
    public PlayerManager _playerManager;
    public OverloadUIController _uiController;

    void Awake()
    {
        //_playerManager = FindObjectOfType<PlayerManager>();
        //_uiController = FindObjectOfType<OverloadUIController>();
    }

    public void TriggerOverload(List<Passenger> passengers)
    {
        if (_isActive) return;

        _isActive = true;
        _timer = _overloadDuration;

        _passengerPlayerIds.Clear();

        foreach (var player in passengers)
        {
            _passengerPlayerIds.Add(player.PlayerNumber);
            player.GetComponent<PlayerInput>().SwitchCurrentActionMap("CoopTask");
        }

        InitializeButtons(passengers.Count);

        _uiController.Show(_buttons.Values.ToList());
    }

    public void OnPlayerHoldButton(Passenger passenger, InputControl control)
    {
        int playerId = passenger.PlayerNumber;

        if (!_isActive) return;
        if (!_passengerPlayerIds.Contains(playerId)) return;
        if (_playerToButton.ContainsKey(playerId)) return;

        OverloadControlType? controlType = GetControlType(control);
        if (controlType == null) return;

        foreach (var button in _buttons.Values)
        {
            if (!button._isHeld && button._buttonType == controlType)
            {
                button._assignedPlayerId = playerId;
                _playerToButton[playerId] = button._buttonId;

                // Update UI
                _uiController.SetHeld(button._buttonId, playerId);

                return;
            }
        }
    }

    public void OnPlayerReleaseButton(Passenger passenger, InputControl control)
    {
        int playerId = passenger.PlayerNumber;

        if (!_playerToButton.ContainsKey(playerId)) return;

        int buttonId = _playerToButton[playerId];

        OverloadControlType? controlType = GetControlType(control);
        if (controlType == null) return;

        if (_buttons[buttonId]._buttonType == controlType) 
        {
            _buttons[buttonId]._assignedPlayerId = -1;
            _playerToButton.Remove(playerId);
            // Update UI
            _uiController.SetReleased(buttonId);
        }
    }

    void Update()
    {
        if (!_isActive) return;

        _timer -= Time.deltaTime;
        _uiController.UpdateTimer(_timer / _overloadDuration);

        if (AllButtonsHeld())
        {
            ResolveSuccess();
        }
        else if (_timer <= 0f)
        {
            ResolveFailure();
        }
    }

    private void InitializeButtons(int buttonCount)
    {
        _buttons.Clear();
        _playerToButton.Clear();

        int[] requiredButtons = GetRandomDistinctNumbers(0, 12, buttonCount);

        for (int i = 0; i < buttonCount; i++)
        {
            _buttons[i] = new OverloadButton(i);
            _buttons[i]._buttonType = (OverloadControlType)requiredButtons[i];
        }
    }

    private bool AllButtonsHeld()
    {
        foreach (var button in _buttons.Values)
        {
            if (!button._isHeld)
                return false;
        }
        return true;
    }

    private void ResolveSuccess()
    {
        _isActive = false;

        _playerManager.AwardAllPassengers(_successPointBonus);

        // Success feedback
        _uiController.PlaySuccess();

        Invoke(nameof(Cleanup), 0.6f);
    }

    private void ResolveFailure()
    {
        _isActive = false;

        _playerManager.AwardAllPassengers(-_failurePointPenalty);

        // Failure feedback
        _uiController.PlayFailure();

        Invoke(nameof(Cleanup), 0.8f);
    }

    private void Cleanup()
    {
        foreach (int playerId in _passengerPlayerIds)
        {
            var player = _playerManager.GetPassengerByNumber(playerId);
            player.GetComponent<PlayerInput>().SwitchCurrentActionMap("Gameplay");
        }

        _buttons.Clear();
        _playerToButton.Clear();
        _passengerPlayerIds.Clear();

        // Hide UI
        _uiController.Hide();
    }

    private OverloadControlType? GetControlType(InputControl control)
    {
        string path = control.path;

        if (path.Contains("buttonSouth")) return OverloadControlType.FaceSouth;
        if (path.Contains("buttonNorth")) return OverloadControlType.FaceNorth;
        if (path.Contains("buttonEast"))  return OverloadControlType.FaceEast;
        if (path.Contains("buttonWest"))  return OverloadControlType.FaceWest;

        if (path.Contains("dpad/up"))    return OverloadControlType.DpadUp;
        if (path.Contains("dpad/down"))  return OverloadControlType.DpadDown;
        if (path.Contains("dpad/left"))  return OverloadControlType.DpadLeft;
        if (path.Contains("dpad/right")) return OverloadControlType.DpadRight;

        if (path.Contains("leftShoulder"))  return OverloadControlType.LeftShoulder;
        if (path.Contains("rightShoulder")) return OverloadControlType.RightShoulder;

        if (path.Contains("leftTrigger"))  return OverloadControlType.LeftTrigger;
        if (path.Contains("rightTrigger")) return OverloadControlType.RightTrigger;

        return null;
    }

    private int[] GetRandomDistinctNumbers(int minInclusive, int maxExclusive, int n)
    {
        return Enumerable
            .Range(minInclusive, maxExclusive - minInclusive)
            .OrderBy(_ => Random.value)
            .Take(n)
            .ToArray();
    }
}
