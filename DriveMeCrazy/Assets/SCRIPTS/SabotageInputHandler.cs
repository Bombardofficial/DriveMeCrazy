// Filename: SabotageInputHandler.cs
using UnityEngine;
using UnityEngine.InputSystem;

// Place this script in the same namespace to easily access the events.
namespace ArcadeVP
{
    [RequireComponent(typeof(PlayerInput))]
    public class SabotageInputHandler : MonoBehaviour
    {

        public float _blockingSabotageCooldown = 6f;
        public float _disruptingSabotageCooldown = 4f;
        private PlayerInput _playerInput;
        /*private InputAction _steeringSabotageAction;
        private InputAction _handbrakeSabotageAction;
        private InputAction _leftSteerSabotageAction;
        private InputAction _rightSteerSabotageAction;
        private InputAction _blockBrakeSabotageAction;
        private InputAction _blockGasSabotageAction;
        private InputAction _blockSteeringSabotageAction;*/

        private InputAction _blockingSabotageAction;
        private InputAction _disruptingSabotageAction;
        private float _lastBlock = 0f;
        private float _lastDisrupt = 0f;

        private Passenger _passenger;

        private SabotageIconHandler _iconHandler;

        private bool IsCurrentDriver =>
            PlayerManager.Instance && PlayerManager.Instance.CurrentDriver == _passenger;

        void Awake()
        {
            _playerInput = GetComponent<PlayerInput>();
            _passenger = GetComponent<Passenger>();
            _iconHandler = GetComponent<SabotageIconHandler>();
            // Find actions by name from the asset attached to PlayerInput
            /*_steeringSabotageAction = _playerInput.actions.FindAction("SteeringSabotage", true);
            _handbrakeSabotageAction = _playerInput.actions.FindAction("HandbrakeSabotage", true);
            _leftSteerSabotageAction = _playerInput.actions.FindAction("LeftSteerSabotage", true);
            _rightSteerSabotageAction = _playerInput.actions.FindAction("RightSteerSabotage", true);
            _blockBrakeSabotageAction = _playerInput.actions.FindAction("BlockBrakeSabotage", true);
            _blockGasSabotageAction = _playerInput.actions.FindAction("BlockGasSabotage", true);
            _blockSteeringSabotageAction = _playerInput.actions.FindAction("BlockSteeringSabotage", true);*/

            _blockingSabotageAction = _playerInput.actions.FindAction("BlockingSabotage", true);
            _disruptingSabotageAction = _playerInput.actions.FindAction("DisruptingSabotage", true);
        }

        void OnEnable()
        {
            /*_steeringSabotageAction.performed += FireSteeringSabotage;
            _handbrakeSabotageAction.performed += FireHandbrakeSabotage;
            _leftSteerSabotageAction.performed += FireLeftSteerSabotage;
            _rightSteerSabotageAction.performed += FireRightSteerSabotage;
            _blockBrakeSabotageAction.performed += FireBlockBrakeSabotage;
            _blockGasSabotageAction.performed += FireBlockGasSabotage;
            _blockSteeringSabotageAction.performed += FireBlockSteeringSabotage;*/

            _blockingSabotageAction.performed += FireBlockingSabotage;
            _disruptingSabotageAction.performed += FireDisruptingSabotage;

            /*if (_steeringSabotageAction == null || _handbrakeSabotageAction == null || _leftSteerSabotageAction == null
                || _rightSteerSabotageAction == null || _blockBrakeSabotageAction == null 
                || _blockGasSabotageAction == null || _blockSteeringSabotageAction == null)
            {
                Debug.LogWarning("[InputManager_ArcadeVP] Sabotage keys not bound!");
            }*/

            if (_blockingSabotageAction == null || _disruptingSabotageAction == null)
            {
                Debug.LogWarning("[InputManager_ArcadeVP] Sabotage keys not bound!");
            }
        }

        void OnDisable()
        {
            /*_steeringSabotageAction.performed -= FireSteeringSabotage;
            _handbrakeSabotageAction.performed -= FireHandbrakeSabotage;
            _leftSteerSabotageAction.performed -= FireLeftSteerSabotage;
            _rightSteerSabotageAction.performed -= FireRightSteerSabotage;
            _blockBrakeSabotageAction.performed -= FireBlockBrakeSabotage;
            _blockGasSabotageAction.performed -= FireBlockGasSabotage;
            _blockSteeringSabotageAction.performed -= FireBlockSteeringSabotage;*/

            _blockingSabotageAction.performed -= FireBlockingSabotage;
            _disruptingSabotageAction.performed -= FireDisruptingSabotage;
        }

        /*private void FireSteeringSabotage(InputAction.CallbackContext context)
        {
            if (IsCurrentDriver) return;
            // Call the static method from your existing script
            InputManager_ArcadeVP.FireSteeringSabotage(_passenger);
        }

        private void FireHandbrakeSabotage(InputAction.CallbackContext context)
        {
            if (IsCurrentDriver) return;
            // Call the static method from your existing script
            InputManager_ArcadeVP.FireHandbrakeSabotage(_passenger);
        }

        private void FireLeftSteerSabotage(InputAction.CallbackContext context)
        {
            if (IsCurrentDriver) return;
            // Call the static method from your existing script
            InputManager_ArcadeVP.FireLeftSteerSabotage(_passenger);
        }

        private void FireRightSteerSabotage(InputAction.CallbackContext context)
        {
            if (IsCurrentDriver) return;
            // Call the static method from your existing script
            InputManager_ArcadeVP.FireRightSteerSabotage(_passenger);
        }

        private void FireBlockBrakeSabotage(InputAction.CallbackContext context)
        {
            if (IsCurrentDriver) return;
            // Call the static method from your existing script
            InputManager_ArcadeVP.FireBlockBrakeSabotage(_passenger);
        }

        private void FireBlockGasSabotage(InputAction.CallbackContext context)
        {
            if (IsCurrentDriver) return;
            // Call the static method from your existing script
            InputManager_ArcadeVP.FireBlockGasSabotage(_passenger);
        }

        private void FireBlockSteeringSabotage(InputAction.CallbackContext context)
        {
            if (IsCurrentDriver) return;
            // Call the static method from your existing script
            InputManager_ArcadeVP.FireBlockSteeringSabotage(_passenger);
        }*/

        private void FireBlockingSabotage(InputAction.CallbackContext context)
        {
            if (IsCurrentDriver || Time.time < _lastBlock + _blockingSabotageCooldown) return;

            _lastBlock = Time.time;

            float randomValue = Random.value;
            if (randomValue < 0.3f) { InputManager_ArcadeVP.FireBlockGasSabotage(_passenger); _iconHandler.ShowActionIcon(GetCurrentIconAnchor(), 4); Debug.Log("BlockGasSabotage Fired!"); }
            else if (randomValue < 0.7f) { InputManager_ArcadeVP.FireBlockBrakeSabotage(_passenger); _iconHandler.ShowActionIcon(GetCurrentIconAnchor(), 6); Debug.Log("BlockBrakeSabotage Fired!"); }
            else if (randomValue <= 1f) { InputManager_ArcadeVP.FireBlockSteeringSabotage(_passenger); _iconHandler.ShowActionIcon(GetCurrentIconAnchor(), 3); Debug.Log("BlockSteeringSabotage Fired!");}
        }

        private void FireDisruptingSabotage(InputAction.CallbackContext context)
        {
            if (IsCurrentDriver || Time.time < _lastDisrupt + _disruptingSabotageCooldown) return;

            _lastDisrupt = Time.time;
            
            float randomValue = Random.value;
            if (randomValue < 0.3f) { InputManager_ArcadeVP.FireLeftSteerSabotage(_passenger); _iconHandler.ShowActionIcon(GetCurrentIconAnchor(), 1); Debug.Log("LeftSteerSabotage Fired!"); }
            else if (randomValue < 0.6f) { InputManager_ArcadeVP.FireRightSteerSabotage(_passenger); _iconHandler.ShowActionIcon(GetCurrentIconAnchor(), 2); Debug.Log("RightSteerSabotage Fired!"); }
            else if (randomValue < 0.8f) { InputManager_ArcadeVP.FireHandbrakeSabotage(_passenger); _iconHandler.ShowActionIcon(GetCurrentIconAnchor(), 5); Debug.Log("HandbrakeSabotage Fired!"); }
            else if (randomValue <= 1f) { InputManager_ArcadeVP.FireSteeringSabotage(_passenger); _iconHandler.ShowActionIcon(GetCurrentIconAnchor(), 0); Debug.Log("SteeringSabotage Fired!");}
        }

        private Transform GetCurrentIconAnchor() => transform.parent.Find("SymbolAnchor");
        
    }
}