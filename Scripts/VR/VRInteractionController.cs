/*
 * Author: Tobias Sorg
 * Date: 2025-01-15
 * Description: Handles VR controller input and interactions for the DECIDE framework
 * License: GPLv3
 */

using System.Collections;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.UI;
using DECIDE.Core;
using DECIDE.Avatars;
using DECIDE.Events;

namespace DECIDE.VR {
    /// <summary>
    /// Manages VR controller input and avatar classification interactions
    /// </summary>
    public class VRInteractionController : MonoBehaviour {
        [Header("XR Controllers")]
        [SerializeField] private XRController _leftController;
        [SerializeField] private XRController _rightController;
        [SerializeField] private XRRayInteractor _rightRayInteractor;
        
        [Header("Crosshair")]
        [SerializeField] private GameObject _crosshairPrefab;
        [SerializeField] private float _crosshairDistance = 10f;
        [SerializeField] private LayerMask _targetLayer;
        [SerializeField] private Color _defaultCrosshairColor = Color.white;
        [SerializeField] private Color _hostileCrosshairColor = Color.red;
        [SerializeField] private Color _friendlyCrosshairColor = Color.green;
        [SerializeField] private Color _unknownCrosshairColor = Color.yellow;
        
        [Header("UI")]
        [SerializeField] private GameObject _leftControllerUI;
        [SerializeField] private Canvas _leftControllerCanvas;
        [SerializeField] private Text _statusText;
        [SerializeField] private Button _startButton;
        [SerializeField] private Button _pauseButton;
        [SerializeField] private Button _quitButton;
        
        [Header("Movement")]
        [SerializeField] private float _moveSpeed = 3f;
        [SerializeField] private float _turnSpeed = 45f;
        [SerializeField] private bool _smoothTurn = true;
        [SerializeField] private float _smoothTurnSpeed = 90f;
        
        [Header("Haptic Feedback")]
        [SerializeField] private float _classificationHapticIntensity = 0.5f;
        [SerializeField] private float _classificationHapticDuration = 0.2f;
        [SerializeField] private float _errorHapticIntensity = 1f;
        [SerializeField] private float _errorHapticDuration = 0.5f;
        
        // Components
        private Transform _vrRig;
        private Transform _cameraTransform;
        private CharacterController _characterController;
        private GameObject _crosshair;
        private Renderer _crosshairRenderer;
        
        // State
        private AvatarController _currentTarget;
        private bool _isUIVisible = false;
        private float _turnValue = 0f;
        private Vector2 _moveInput;
        private bool _hasClassifiedCurrentTarget = false;
        
        // Input values
        private bool _triggerPressed = false;
        private bool _xButtonPressed = false;
        private bool _yButtonPressed = false;
        
        private void Awake() {
            _vrRig = transform.parent ?? transform;
            _cameraTransform = Camera.main.transform;
            _characterController = GetComponentInParent<CharacterController>() ?? 
                                 _vrRig.GetComponent<CharacterController>();
            
            SetupCrosshair();
            SetupUI();
        }
        
        private void Start() {
            // Hide UI initially
            if (_leftControllerUI != null) {
                _leftControllerUI.SetActive(false);
            }
        }
        
        private void Update() {
            HandleInput();
            HandleMovement();
            HandleRotation();
            UpdateCrosshair();
            HandleTargeting();
        }
        
        /// <summary>
        /// Sets up the crosshair
        /// </summary>
        private void SetupCrosshair() {
            if (_crosshairPrefab != null) {
                _crosshair = Instantiate(_crosshairPrefab);
                _crosshairRenderer = _crosshair.GetComponent<Renderer>();
                _crosshair.transform.SetParent(_rightController.transform);
                _crosshair.transform.localPosition = Vector3.forward * _crosshairDistance;
            }
        }
        
        /// <summary>
        /// Sets up the UI
        /// </summary>
        private void SetupUI() {
            if (_leftControllerUI != null && _leftControllerCanvas != null) {
                // Attach UI to left controller
                _leftControllerCanvas.transform.SetParent(_leftController.transform);
                _leftControllerCanvas.transform.localPosition = new Vector3(0, 0.1f, 0.05f);
                _leftControllerCanvas.transform.localRotation = Quaternion.Euler(-45f, 0, 0);
                
                // Setup button listeners
                if (_startButton != null) {
                    _startButton.onClick.AddListener(OnStartButtonPressed);
                }
                if (_pauseButton != null) {
                    _pauseButton.onClick.AddListener(OnPauseButtonPressed);
                }
                if (_quitButton != null) {
                    _quitButton.onClick.AddListener(OnQuitButtonPressed);
                }
            }
        }
        
        /// <summary>
        /// Handles all controller input
        /// </summary>
        private void HandleInput() {
            // Get input devices
            InputDevice leftDevice = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            InputDevice rightDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            
            // Left controller input
            if (leftDevice.isValid) {
                // X button (Start scenario)
                bool xButton;
                if (leftDevice.TryGetFeatureValue(CommonUsages.primaryButton, out xButton)) {
                    if (xButton && !_xButtonPressed) {
                        OnXButtonPressed();
                    }
                    _xButtonPressed = xButton;
                }
                
                // Y button (Toggle UI)
                bool yButton;
                if (leftDevice.TryGetFeatureValue(CommonUsages.secondaryButton, out yButton)) {
                    if (yButton && !_yButtonPressed) {
                        OnYButtonPressed();
                    }
                    _yButtonPressed = yButton;
                }
                
                // Thumbstick for movement
                Vector2 thumbstick;
                if (leftDevice.TryGetFeatureValue(CommonUsages.primary2DAxis, out thumbstick)) {
                    _moveInput = thumbstick;
                }
            }
            
            // Right controller input
            if (rightDevice.isValid) {
                // Trigger for classification
                float triggerValue;
                if (rightDevice.TryGetFeatureValue(CommonUsages.trigger, out triggerValue)) {
                    bool triggerCurrentlyPressed = triggerValue > 0.5f;
                    if (triggerCurrentlyPressed && !_triggerPressed) {
                        OnTriggerPressed();
                    }
                    _triggerPressed = triggerCurrentlyPressed;
                }
                
                // Thumbstick for rotation
                Vector2 thumbstick;
                if (rightDevice.TryGetFeatureValue(CommonUsages.primary2DAxis, out thumbstick)) {
                    _turnValue = thumbstick.x;
                }
            }
        }
        
        /// <summary>
        /// Handles player movement
        /// </summary>
        private void HandleMovement() {
            if (_characterController == null || _cameraTransform == null) {
                return;
            }
            
            Vector3 forward = _cameraTransform.forward;
            Vector3 right = _cameraTransform.right;
            
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();
            
            Vector3 moveDirection = forward * _moveInput.y + right * _moveInput.x;
            moveDirection *= _moveSpeed * Time.deltaTime;
            
            _characterController.Move(moveDirection);
        }
        
        /// <summary>
        /// Handles player rotation
        /// </summary>
        private void HandleRotation() {
            if (Mathf.Abs(_turnValue) > 0.1f) {
                float rotation = _turnValue * (_smoothTurn ? _smoothTurnSpeed : _turnSpeed) * Time.deltaTime;
                _vrRig.Rotate(0, rotation, 0);
            }
        }
        
        /// <summary>
        /// Updates the crosshair position and appearance
        /// </summary>
        private void UpdateCrosshair() {
            if (_crosshair == null || _rightController == null) {
                return;
            }
            
            // Raycast from controller
            Ray ray = new Ray(_rightController.transform.position, _rightController.transform.forward);
            RaycastHit hit;
            
            if (Physics.Raycast(ray, out hit, _crosshairDistance, _targetLayer)) {
                _crosshair.transform.position = hit.point;
                
                // Check if hitting an avatar
                AvatarController avatar = hit.collider.GetComponent<AvatarController>();
                if (avatar != null) {
                    UpdateCrosshairColor(avatar.Type);
                } else {
                    UpdateCrosshairColor(null);
                }
            } else {
                _crosshair.transform.localPosition = Vector3.forward * _crosshairDistance;
                UpdateCrosshairColor(null);
            }
        }
        
        /// <summary>
        /// Updates crosshair color based on target type
        /// </summary>
        private void UpdateCrosshairColor(AvatarType? targetType) {
            if (_crosshairRenderer == null) {
                return;
            }
            
            Color color = _defaultCrosshairColor;
            
            if (targetType.HasValue) {
                switch (targetType.Value) {
                    case AvatarType.Hostile:
                        color = _hostileCrosshairColor;
                        break;
                    case AvatarType.Friendly:
                        color = _friendlyCrosshairColor;
                        break;
                    case AvatarType.Unknown:
                        color = _unknownCrosshairColor;
                        break;
                }
            }
            
            _crosshairRenderer.material.color = color;
        }
        
        /// <summary>
        /// Handles avatar targeting
        /// </summary>
        private void HandleTargeting() {
            Ray ray = new Ray(_rightController.transform.position, _rightController.transform.forward);
            RaycastHit hit;
            
            if (Physics.Raycast(ray, out hit, _crosshairDistance, _targetLayer)) {
                AvatarController avatar = hit.collider.GetComponent<AvatarController>();
                
                if (avatar != null && avatar != _currentTarget) {
                    // New target acquired
                    _currentTarget = avatar;
                    _hasClassifiedCurrentTarget = false;
                    avatar.OnTargeted();
                } else if (avatar == null && _currentTarget != null) {
                    // Lost target
                    _currentTarget = null;
                    _hasClassifiedCurrentTarget = false;
                }
            } else if (_currentTarget != null) {
                // No longer targeting anything
                _currentTarget = null;
                _hasClassifiedCurrentTarget = false;
            }
        }
        
        /// <summary>
        /// Called when trigger is pressed
        /// </summary>
        private void OnTriggerPressed() {
            if (_currentTarget != null && !_hasClassifiedCurrentTarget) {
                ClassifyTarget();
            }
        }
        
        /// <summary>
        /// Classifies the current target as hostile
        /// </summary>
        private void ClassifyTarget() {
            if (_currentTarget == null) {
                return;
            }
            
            _currentTarget.OnClassified(true); // Always classifying as hostile with trigger
            _hasClassifiedCurrentTarget = true;
            
            // Haptic feedback
            bool isCorrect = _currentTarget.Type == AvatarType.Hostile;
            StartCoroutine(PlayHapticFeedback(
                isCorrect ? _classificationHapticIntensity : _errorHapticIntensity,
                isCorrect ? _classificationHapticDuration : _errorHapticDuration,
                XRNode.RightHand
            ));
        }
        
        /// <summary>
        /// Called when X button is pressed
        /// </summary>
        private void OnXButtonPressed() {
            if (ScenarioManager.Instance != null) {
                if (ScenarioManager.Instance.CurrentState == ScenarioState.Idle) {
                    ScenarioManager.Instance.StartScenario();
                } else if (ScenarioManager.Instance.CurrentState == ScenarioState.Paused) {
                    ScenarioManager.Instance.ResumeScenario();
                }
            }
        }
        
        /// <summary>
        /// Called when Y button is pressed
        /// </summary>
        private void OnYButtonPressed() {
            ToggleUI();
        }
        
        /// <summary>
        /// Toggles the UI visibility
        /// </summary>
        private void ToggleUI() {
            _isUIVisible = !_isUIVisible;
            if (_leftControllerUI != null) {
                _leftControllerUI.SetActive(_isUIVisible);
            }
        }
        
        /// <summary>
        /// Called when Start button is pressed
        /// </summary>
        private void OnStartButtonPressed() {
            if (ScenarioManager.Instance != null) {
                ScenarioManager.Instance.StartScenario();
            }
            UpdateStatusText("Scenario Started");
        }
        
        /// <summary>
        /// Called when Pause button is pressed
        /// </summary>
        private void OnPauseButtonPressed() {
            if (ScenarioManager.Instance != null) {
                ScenarioManager.Instance.PauseScenario();
            }
            UpdateStatusText("Scenario Paused");
        }
        
        /// <summary>
        /// Called when Quit button is pressed
        /// </summary>
        private void OnQuitButtonPressed() {
            if (ScenarioManager.Instance != null) {
                ScenarioManager.Instance.StopScenario();
            }
            UpdateStatusText("Scenario Stopped");
            ToggleUI();
        }
        
        /// <summary>
        /// Updates the status text
        /// </summary>
        private void UpdateStatusText(string text) {
            if (_statusText != null) {
                _statusText.text = text;
            }
        }
        
        /// <summary>
        /// Plays haptic feedback on specified controller
        /// </summary>
        private IEnumerator PlayHapticFeedback(float intensity, float duration, XRNode node) {
            InputDevice device = InputDevices.GetDeviceAtXRNode(node);
            if (device.isValid) {
                HapticCapabilities capabilities;
                if (device.TryGetHapticCapabilities(out capabilities) && capabilities.supportsImpulse) {
                    device.SendHapticImpulse(0, intensity, duration);
                }
            }
            yield return new WaitForSeconds(duration);
        }
    }
}