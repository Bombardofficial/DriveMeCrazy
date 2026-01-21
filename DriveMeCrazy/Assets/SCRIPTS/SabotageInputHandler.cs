// Filename: SabotageInputHandler.cs
using UnityEngine;
using UnityEngine.InputSystem;

// Place this script in the same namespace to easily access the events.
namespace ArcadeVP
{
    [RequireComponent(typeof(PlayerInput))]
    public class SabotageInputHandler : MonoBehaviour
    {
        // BLOCKING = STEERING      ||      DISRUPTING = SPEED
        [Header("Sabotage Cooldown Settings")]
        [Tooltip("Base Blocking Cooldown Value")]
        [SerializeField] private float _blockingSabotageBaseCooldown = 5f;
        [Tooltip("Blocking Cooldown Increment per Player")]
        [SerializeField] private float _blockingSabotageCooldownIncrement = 2f;
        [Tooltip("Base Disrupting Cooldown Value")]
        [SerializeField] private float _disruptingSabotageBaseCooldown = 4f;
        [Tooltip("Disrupting Cooldown Increment per Player")]
        [SerializeField] private float _disruptingSabotageCooldownIncrement = 1.5f;
        [Tooltip("Base General Cooldown Value")]
        [SerializeField] private float _generalSabotageBaseCooldown = 2f;
        [Tooltip("General Cooldown Increment per Player")]
        [SerializeField] private float _generalSabotageCooldownIncrement = 1f;

        private float _blockingSabotageCooldown;
        private float _disruptingSabotageCooldown;
        private float _generalSabotageCooldown;
        private PlayerInput _playerInput;

        private InputAction _blockingSabotageAction;
        private InputAction _disruptingSabotageAction;
        private float _lastBlock = -6f;
        private float _lastDisrupt = -4f;
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
            _blockingSabotageAction.performed += FireSteeringSabotage;
            _disruptingSabotageAction.performed += FireSpeedSabotage;

            if (_blockingSabotageAction == null || _disruptingSabotageAction == null)
            {
                Debug.LogWarning("[InputManager_ArcadeVP] Sabotage keys not bound!");
            }
        }

        void OnDisable()
        {
            _blockingSabotageAction.performed -= FireSteeringSabotage;
            _disruptingSabotageAction.performed -= FireSpeedSabotage;
        }

        public void Init()
        {
            float additionalPlayerCount = PlayerManager.Instance.Passengers.Count - 2f;
            _blockingSabotageCooldown = _blockingSabotageBaseCooldown + additionalPlayerCount * _blockingSabotageCooldownIncrement;
            _disruptingSabotageCooldown = _disruptingSabotageBaseCooldown + additionalPlayerCount * _disruptingSabotageCooldownIncrement;
            _generalSabotageCooldown = _generalSabotageBaseCooldown + additionalPlayerCount * _generalSabotageCooldownIncrement;
        }

        /*private void FireBlockingSabotage(InputAction.CallbackContext context)
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
        }*/

        /*private void FireDisruptingSabotage(InputAction.CallbackContext context)
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
        }*/

        private void FireSteeringSabotage(InputAction.CallbackContext context)
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
                InputManager_ArcadeVP.FireBlockSteeringSabotage(_passenger); 
                _iconHandler.ShowActionIcon(GetCurrentIconAnchor(), 3); 
                Debug.Log("BlockSteeringSabotage Fired!"); 
            }
        }

        private void FireSpeedSabotage(InputAction.CallbackContext context)
        {
            if (IsCurrentDriver || Time.time < _lastBlock + _blockingSabotageCooldown || Time.time < _lastSabotage + _generalSabotageCooldown) return;

            _lastBlock = Time.time;
            _lastSabotage = _lastBlock;
            PlayerManager.Instance._lastSabotage = _passenger;

            float randomValue = Random.value;
            if (randomValue < 0.25f) 
            { 
                InputManager_ArcadeVP.FireBlockGasSabotage(_passenger); 
                _iconHandler.ShowActionIcon(GetCurrentIconAnchor(), 4); 
                Debug.Log("BlockGasSabotage Fired!"); 
            }
            else if (randomValue < 0.5f) 
            { 
                InputManager_ArcadeVP.FireBlockBrakeSabotage(_passenger); 
                _iconHandler.ShowActionIcon(GetCurrentIconAnchor(), 6); 
                Debug.Log("BlockBrakeSabotage Fired!"); 
            }
            else if (randomValue < 0.75f) 
            { 
                InputManager_ArcadeVP.FireHandbrakeSabotage(_passenger); 
                _iconHandler.ShowActionIcon(GetCurrentIconAnchor(), 5); 
                Debug.Log("HandbrakeSabotage Fired!"); 
            }
            else if (randomValue <= 1f)
            {
                InputManager_ArcadeVP.FirePressGasSabotage(_passenger); 
                _iconHandler.ShowActionIcon(GetCurrentIconAnchor(), 7); 
                Debug.Log("PressGasSabotage Fired!"); 
            }
        }

        private Transform GetCurrentIconAnchor() => transform.parent.Find("SymbolAnchor");

        public float LastBlock => _lastBlock;
        public float LastDisrupt => _lastDisrupt;
        public float Lastsabotage => _lastSabotage;
        public float BlockingSabotageCooldown => _blockingSabotageCooldown;
        public float DisruptingSabotageCooldown => _disruptingSabotageCooldown;
        public float GeneralSabotageCooldown => _generalSabotageCooldown;
        
    }
}