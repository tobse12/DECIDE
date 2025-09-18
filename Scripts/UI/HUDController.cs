/*
 * Author: Tobias Sorg
 * Date: 2025-01-15
 * Description: HUD controller for displaying information in VR for the DECIDE framework
 * License: GPLv3
 */

using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using DECIDE.Core;
using DECIDE.Events;

namespace DECIDE.UI {
    /// <summary>
    /// Manages the heads-up display in VR
    /// </summary>
    public class HUDController : MonoBehaviour {
        [Header("HUD Canvas")]
        [SerializeField] private Canvas _hudCanvas;
        [SerializeField] private float _hudDistance = 2f;
        [SerializeField] private float _hudHeight = 0.5f;
        [SerializeField] private bool _followHeadRotation = true;
        [SerializeField] private float _followSpeed = 5f;
        
        [Header("Timer Display")]
        [SerializeField] private Text _timerText;
        [SerializeField] private Image _timerProgressBar;
        [SerializeField] private Color _timerNormalColor = Color.green;
        [SerializeField] private Color _timerWarningColor = Color.yellow;
        [SerializeField] private Color _timerCriticalColor = Color.red;
        [SerializeField] private float _warningThreshold = 60f; // 1 minute
        [SerializeField] private float _criticalThreshold = 30f; // 30 seconds
        
        [Header("Stats Display")]
        [SerializeField] private Text _totalClassificationsText;
        [SerializeField] private Text _accuracyText;
        [SerializeField] private Text _streakText;
        [SerializeField] private Text _scoreText;
        
        [Header("Stressor Indicators")]
        [SerializeField] private GameObject _stressorPanel;
        [SerializeField] private GameObject _stressorIndicatorPrefab;
        [SerializeField] private Transform _stressorContainer;
        
        [Header("Notifications")]
        [SerializeField] private GameObject _notificationPanel;
        [SerializeField] private Text _notificationText;
        [SerializeField] private float _notificationDuration = 3f;
        [SerializeField] private AnimationCurve _notificationFadeCurve;
        
        [Header("Placeholder Elements")]
        [SerializeField] private Text _placeholder1Text;
        [SerializeField] private Text _placeholder2Text;
        
        // Internal state
        private Transform _cameraTransform;
        private float _totalDuration;
        private int _totalClassifications = 0;
        private int _correctClassifications = 0;
        private int _currentStreak = 0;
        private int _bestStreak = 0;
        private float _currentScore = 0f;
        private Coroutine _notificationCoroutine;
        
        private void Awake() {
            _cameraTransform = Camera.main?.transform;
            
            if (_hudCanvas != null && _cameraTransform != null) {
                SetupHUD();
            }
        }
        
        private void OnEnable() {
            // Subscribe to events
            ScenarioEvents.OnScenarioStarted += HandleScenarioStarted;
            ScenarioEvents.OnScenarioEnded += HandleScenarioEnded;
            ScenarioEvents.OnAvatarClassified += HandleAvatarClassified;
            ScenarioEvents.OnStressorActivated += HandleStressorActivated;
            ScenarioEvents.OnStressorDeactivated += HandleStressorDeactivated;
        }
        
        private void OnDisable() {
            // Unsubscribe from events
            ScenarioEvents.OnScenarioStarted -= HandleScenarioStarted;
            ScenarioEvents.OnScenarioEnded -= HandleScenarioEnded;
            ScenarioEvents.OnAvatarClassified -= HandleAvatarClassified;
            ScenarioEvents.OnStressorActivated -= HandleStressorActivated;
            ScenarioEvents.OnStressorDeactivated -= HandleStressorDeactivated;
        }
        
        private void Update() {
            if (_followHeadRotation && _hudCanvas != null && _cameraTransform != null) {
                UpdateHUDPosition();
            }
        }
        
        /// <summary>
        /// Sets up the HUD canvas
        /// </summary>
        private void SetupHUD() {
            if (_hudCanvas.renderMode != RenderMode.WorldSpace) {
                _hudCanvas.renderMode = RenderMode.WorldSpace;
            }
            
            // Set initial position
            UpdateHUDPosition();
            
            // Initialize displays
            ResetDisplays();
            
            // Hide notification panel initially
            if (_notificationPanel != null) {
                _notificationPanel.SetActive(false);
            }
        }
        
        /// <summary>
        /// Updates HUD position to follow head movement
        /// </summary>
        private void UpdateHUDPosition() {
            Vector3 targetPosition = _cameraTransform.position + _cameraTransform.forward * _hudDistance;
            targetPosition.y = _cameraTransform.position.y + _hudHeight;
            
            _hudCanvas.transform.position = Vector3.Lerp(
                _hudCanvas.transform.position,
                targetPosition,
                Time.deltaTime * _followSpeed
            );
            
            // Look at camera but maintain upright orientation
            Vector3 lookDirection = _hudCanvas.transform.position - _cameraTransform.position;
            lookDirection.y = 0;
            if (lookDirection != Vector3.zero) {
                Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
                _hudCanvas.transform.rotation = Quaternion.Slerp(
                    _hudCanvas.transform.rotation,
                    targetRotation,
                    Time.deltaTime * _followSpeed
                );
            }
        }
        
        /// <summary>
        /// Updates the timer display
        /// </summary>
        public void UpdateTimer(float remainingTime) {
            if (_timerText != null) {
                int minutes = Mathf.FloorToInt(remainingTime / 60f);
                int seconds = Mathf.FloorToInt(remainingTime % 60f);
                _timerText.text = $"{minutes:00}:{seconds:00}";
                
                // Update color based on remaining time
                if (remainingTime <= _criticalThreshold) {
                    _timerText.color = _timerCriticalColor;
                } else if (remainingTime <= _warningThreshold) {
                    _timerText.color = _timerWarningColor;
                } else {
                    _timerText.color = _timerNormalColor;
                }
            }
            
            if (_timerProgressBar != null && _totalDuration > 0) {
                float progress = remainingTime / _totalDuration;
                _timerProgressBar.fillAmount = progress;
                
                // Update progress bar color
                if (remainingTime <= _criticalThreshold) {
                    _timerProgressBar.color = _timerCriticalColor;
                } else if (remainingTime <= _warningThreshold) {
                    _timerProgressBar.color = _timerWarningColor;
                } else {
                    _timerProgressBar.color = _timerNormalColor;
                }
            }
        }
        
        /// <summary>
        /// Updates classification statistics
        /// </summary>
        private void UpdateStats() {
            if (_totalClassificationsText != null) {
                _totalClassificationsText.text = $"Classifications: {_totalClassifications}";
            }
            
            if (_accuracyText != null) {
                float accuracy = _totalClassifications > 0 ? 
                    (float)_correctClassifications / _totalClassifications * 100f : 0f;
                _accuracyText.text = $"Accuracy: {accuracy:F1}%";
            }
            
            if (_streakText != null) {
                _streakText.text = $"Streak: {_currentStreak} (Best: {_bestStreak})";
            }
            
            if (_scoreText != null) {
                _scoreText.text = $"Score: {_currentScore:F0}";
            }
        }
        
        /// <summary>
        /// Shows a notification message
        /// </summary>
        public void ShowNotification(string message, NotificationType type = NotificationType.Info) {
            if (_notificationPanel == null || _notificationText == null) {
                return;
            }
            
            // Stop any existing notification
            if (_notificationCoroutine != null) {
                StopCoroutine(_notificationCoroutine);
            }
            
            _notificationCoroutine = StartCoroutine(ShowNotificationCoroutine(message, type));
        }
        
        /// <summary>
        /// Coroutine for showing notifications
        /// </summary>
        private IEnumerator ShowNotificationCoroutine(string message, NotificationType type) {
            _notificationPanel.SetActive(true);
            _notificationText.text = message;
            
            // Set color based on type
            switch (type) {
                case NotificationType.Success:
                    _notificationText.color = Color.green;
                    break;
                case NotificationType.Warning:
                    _notificationText.color = Color.yellow;
                    break;
                case NotificationType.Error:
                    _notificationText.color = Color.red;
                    break;
                default:
                    _notificationText.color = Color.white;
                    break;
            }
            
            // Fade in
            CanvasGroup canvasGroup = _notificationPanel.GetComponent<CanvasGroup>();
            if (canvasGroup == null) {
                canvasGroup = _notificationPanel.AddComponent<CanvasGroup>();
            }
            
            float elapsed = 0f;
            while (elapsed < _notificationDuration) {
                elapsed += Time.deltaTime;
                float t = elapsed / _notificationDuration;
                
                // Use fade curve if available
                if (_notificationFadeCurve != null && _notificationFadeCurve.length > 0) {
                    canvasGroup.alpha = _notificationFadeCurve.Evaluate(t);
                } else {
                    // Simple fade out in last 25% of duration
                    if (t > 0.75f) {
                        canvasGroup.alpha = 1f - ((t - 0.75f) / 0.25f);
                    } else {
                        canvasGroup.alpha = 1f;
                    }
                }
                
                yield return null;
            }
            
            _notificationPanel.SetActive(false);
            _notificationCoroutine = null;
        }
        
        /// <summary>
        /// Adds a stressor indicator to the HUD
        /// </summary>
        private void AddStressorIndicator(string stressorName, float intensity) {
            if (_stressorIndicatorPrefab == null || _stressorContainer == null) {
                return;
            }
            
            GameObject indicator = Instantiate(_stressorIndicatorPrefab, _stressorContainer);
            indicator.name = $"Stressor_{stressorName}";
            
            // Configure indicator
            Text text = indicator.GetComponentInChildren<Text>();
            if (text != null) {
                text.text = stressorName;
            }
            
            Image image = indicator.GetComponent<Image>();
            if (image != null) {
                Color color = Color.Lerp(Color.yellow, Color.red, intensity);
                color.a = 0.7f;
                image.color = color;
            }
        }
        
        /// <summary>
        /// Removes a stressor indicator from the HUD
        /// </summary>
        private void RemoveStressorIndicator(string stressorName) {
            if (_stressorContainer == null) {
                return;
            }
            
            Transform indicator = _stressorContainer.Find($"Stressor_{stressorName}");
            if (indicator != null) {
                Destroy(indicator.gameObject);
            }
        }
        
        /// <summary>
        /// Updates placeholder text fields
        /// </summary>
        public void UpdatePlaceholder1(string text) {
            if (_placeholder1Text != null) {
                _placeholder1Text.text = text;
            }
        }
        
        public void UpdatePlaceholder2(string text) {
            if (_placeholder2Text != null) {
                _placeholder2Text.text = text;
            }
        }
        
        /// <summary>
        /// Resets all displays to initial state
        /// </summary>
        private void ResetDisplays() {
            _totalClassifications = 0;
            _correctClassifications = 0;
            _currentStreak = 0;
            _bestStreak = 0;
            _currentScore = 0f;
            
            UpdateStats();
            
            if (_placeholder1Text != null) {
                _placeholder1Text.text = "Ready";
            }
            
            if (_placeholder2Text != null) {
                _placeholder2Text.text = "---";
            }
            
            // Clear stressor indicators
            if (_stressorContainer != null) {
                foreach (Transform child in _stressorContainer) {
                    Destroy(child.gameObject);
                }
            }
        }
        
        // Event handlers
        private void HandleScenarioStarted(ScenarioStartedEventData data) {
            _totalDuration = data.duration;
            ResetDisplays();
            ShowNotification("Scenario Started", NotificationType.Success);
        }
        
        private void HandleScenarioEnded(ScenarioEndedEventData data) {
            ShowNotification($"Scenario Complete! Accuracy: {(float)data.correctClassifications / data.totalClassifications * 100f:F1}%", 
                           NotificationType.Success);
        }
        
        private void HandleAvatarClassified(AvatarClassifiedEventData data) {
            _totalClassifications++;
            
            if (data.isCorrect) {
                _correctClassifications++;
                _currentStreak++;
                _bestStreak = Mathf.Max(_bestStreak, _currentStreak);
                _currentScore += 100f * (1f + _currentStreak * 0.1f); // Bonus for streak
                
                ShowNotification("Correct!", NotificationType.Success);
            } else {
                _currentStreak = 0;
                _currentScore = Mathf.Max(0, _currentScore - 50f);
                
                ShowNotification($"Incorrect! Was: {data.actualType}", NotificationType.Error);
            }
            
            UpdateStats();
        }
        
        private void HandleStressorActivated(StressorActivatedEventData data) {
            AddStressorIndicator(data.stressorName, data.intensity);
        }
        
        private void HandleStressorDeactivated(StressorDeactivatedEventData data) {
            RemoveStressorIndicator(data.stressorName);
        }
    }
    
    /// <summary>
    /// Notification types for HUD messages
    /// </summary>
    public enum NotificationType {
        Info,
        Success,
        Warning,
        Error
    }
}