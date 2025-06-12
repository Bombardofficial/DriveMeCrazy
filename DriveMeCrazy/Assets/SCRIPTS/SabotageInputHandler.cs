// Filename: SabotageInputHandler.cs
using UnityEngine;
using UnityEngine.InputSystem;

// Place this script in the same namespace to easily access the events.
namespace ArcadeVP
{
    [RequireComponent(typeof(PlayerInput))]
    public class SabotageInputHandler : MonoBehaviour
    {
        private PlayerInput _playerInput;
        private InputAction _steeringSabotageAction;
        private InputAction _handbrakeSabotageAction;
        private Passenger _passenger;

        private bool IsCurrentDriver =>
            PlayerManager.Instance && PlayerManager.Instance.CurrentDriver == _passenger;

        void Awake()
        {
            _playerInput = GetComponent<PlayerInput>();
            _passenger = GetComponent<Passenger>();
            // Find actions by name from the asset attached to PlayerInput
            _steeringSabotageAction = _playerInput.actions.FindAction("SteeringSabotage", true);
            _handbrakeSabotageAction = _playerInput.actions.FindAction("HandbrakeSabotage", true);
        }

        void OnEnable()
        {
            _steeringSabotageAction.performed += FireSteeringSabotage;
            _handbrakeSabotageAction.performed += FireHandbrakeSabotage;
        }

        void OnDisable()
        {
            _steeringSabotageAction.performed -= FireSteeringSabotage;
            _handbrakeSabotageAction.performed -= FireHandbrakeSabotage;
        }

        private void FireSteeringSabotage(InputAction.CallbackContext context)
        {
            if (IsCurrentDriver) return;
            // Call the static method from your existing script
            InputManager_ArcadeVP.FireSteeringSabotage();
        }

        private void FireHandbrakeSabotage(InputAction.CallbackContext context)
        {
            if (IsCurrentDriver) return;
            // Call the static method from your existing script
            InputManager_ArcadeVP.FireHandbrakeSabotage();
        }
    }
}