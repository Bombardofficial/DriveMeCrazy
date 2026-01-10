// Filename: SabotageInputHandler.cs
using UnityEngine;
using UnityEngine.InputSystem;

// Place this script in the same namespace to easily access the events.
namespace ArcadeVP
{
    [RequireComponent(typeof(PlayerInput))]
    public class CoopTaskInputHandler : MonoBehaviour
    {
        private PlayerInput _playerInput;
        private InputAction _coopTaskAction;
        private Passenger _passenger;

        private EngineOverloadManager _coopTaskManager;

        void Awake()
        {
            _playerInput = GetComponent<PlayerInput>();
            _passenger = GetComponent<Passenger>();
            _coopTaskManager = FindObjectOfType<EngineOverloadManager>();

            _coopTaskAction = _playerInput.actions.FindAction("Task", true);
        }

        void OnEnable()
        {

            _coopTaskAction.performed += FireCoopTask;
            _coopTaskAction.canceled += FireCoopTask;

            if (_coopTaskAction == null)
            {
                Debug.LogWarning("[CoopTaskInputHandler] Coop Task keys not bound!");
            }
        }

        void OnDisable()
        {

            _coopTaskAction.performed -= FireCoopTask;
            _coopTaskAction.canceled -= FireCoopTask;
        }

        private void FireCoopTask(InputAction.CallbackContext context)
        {
            if (context.performed)
                _coopTaskManager.OnPlayerHoldButton(_passenger, context.control);
            if (context.canceled)
                _coopTaskManager.OnPlayerReleaseButton(_passenger, context.control);
        }
        
    }
}