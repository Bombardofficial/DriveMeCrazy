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
        public float _generalSabotageCooldown = 2f;
        private PlayerInput _playerInput;

        private InputAction _blockingSabotageAction;
        private InputAction _disruptingSabotageAction;
        private float _lastBlock = 0f;
        private float _lastDisrupt = 0f;
        private float _lastSabotage = 0f;

        private Passenger _passenger;

        private SabotageIconHandler _iconHandler;

        private bool IsCurrentDriver =>
            PlayerManager.Instance && PlayerManager.Instance.CurrentDriver == _passenger;

        void Awake()
        {
            _playerInput = GetComponent<PlayerInput>();
            _passenger = GetComponent<Passenger>();
            _iconHandler = GetComponent<SabotageIconHandler>();

            _blockingSabotageAction = _playerInput.actions.FindAction("BlockingSabotage", true);
            _disruptingSabotageAction = _playerInput.actions.FindAction("DisruptingSabotage", true);
        }

        void OnEnable()
        {
            _blockingSabotageAction.performed += FireBlockingSabotage;
            _disruptingSabotageAction.performed += FireDisruptingSabotage;

            if (_blockingSabotageAction == null || _disruptingSabotageAction == null)
            {
                Debug.LogWarning("[InputManager_ArcadeVP] Sabotage keys not bound!");
            }
        }

        void OnDisable()
        {
            _blockingSabotageAction.performed -= FireBlockingSabotage;
            _disruptingSabotageAction.performed -= FireDisruptingSabotage;
        }

        private void FireBlockingSabotage(InputAction.CallbackContext context)
        {
            if (IsCurrentDriver || Time.time < _lastBlock + _blockingSabotageCooldown || Time.time < _lastSabotage + _generalSabotageCooldown) return;

            _lastBlock = Time.time;
            _lastSabotage = _lastBlock;
            PlayerManager.Instance._lastSabotage = _passenger;

            float randomValue = Random.value;
            if (randomValue < 0.3f) 
            { 
                InputManager_ArcadeVP.FireBlockGasSabotage(_passenger); 
                _iconHandler.ShowActionIcon(GetCurrentIconAnchor(), 4); 
                Debug.Log("BlockGasSabotage Fired!"); 
            }
            else if (randomValue < 0.3f) 
            { 
                InputManager_ArcadeVP.FireBlockBrakeSabotage(_passenger); 
                _iconHandler.ShowActionIcon(GetCurrentIconAnchor(), 6); 
                Debug.Log("BlockBrakeSabotage Fired!"); 
            }
            else if (randomValue <= 1f) 
            { 
                InputManager_ArcadeVP.FireBlockSteeringSabotage(_passenger); 
                _iconHandler.ShowActionIcon(GetCurrentIconAnchor(), 3); 
                Debug.Log("BlockSteeringSabotage Fired!");
            }
        }

        private void FireDisruptingSabotage(InputAction.CallbackContext context)
        {
            if (IsCurrentDriver || Time.time < _lastDisrupt + _disruptingSabotageCooldown || Time.time < _lastSabotage + _generalSabotageCooldown) return;

            _lastDisrupt = Time.time;
            _lastSabotage = _lastDisrupt;
            PlayerManager.Instance._lastSabotage = _passenger;
            
            float randomValue = Random.value;
            if (randomValue < 0.35f) 
            { 
                InputManager_ArcadeVP.FireLeftSteerSabotage(_passenger); 
                _iconHandler.ShowActionIcon(GetCurrentIconAnchor(), 1); 
                Debug.Log("LeftSteerSabotage Fired!"); 
            }
            else if (randomValue < 0.7f) 
            { 
                InputManager_ArcadeVP.FireRightSteerSabotage(_passenger); 
                _iconHandler.ShowActionIcon(GetCurrentIconAnchor(), 2); 
                Debug.Log("RightSteerSabotage Fired!"); 
            }
            else if (randomValue < 0.9f) 
            { 
                InputManager_ArcadeVP.FireSteeringSabotage(_passenger);
                _iconHandler.ShowActionIcon(GetCurrentIconAnchor(), 0); 
                Debug.Log("SteeringSabotage Fired!");
            }
            else if (randomValue <= 1f) 
            { 
                InputManager_ArcadeVP.FireHandbrakeSabotage(_passenger); 
                _iconHandler.ShowActionIcon(GetCurrentIconAnchor(), 5); 
                Debug.Log("HandbrakeSabotage Fired!"); 
            }
        }

        private Transform GetCurrentIconAnchor() => transform.parent.Find("SymbolAnchor");
        
    }
}