/*
 * Author: Tobias Sorg
 * Date: 2025-01-15
 * Description: Implements time pressure through countdowns and urgency indicators for the DECIDE VR framework
 * License: GPLv3
 */

using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using DECIDE.Stressors;
using DECIDE.UI;

namespace DECIDE.Stressors.Implementations {
    /// <summary>
    /// Creates time pressure through visual and audio countdowns
    /// </summary>
    public class TimePressureStressor : MonoBehaviour, IStressor {
        [Header("Timer Settings")]
        [SerializeField] private float _countdownDuration = 30f;
        [SerializeField] private float _warningThreshold = 10f;
        [SerializeField] private float _criticalThreshold = 5f;
        [SerializeField] private bool _showMilliseconds = true;
        [SerializeField] private bool _accelerateTime = false;
        [SerializeField] private float _timeAcceleration = 1.5f;
        
        [Header("Visual Indicators")]
        [SerializeField] private bool _showCountdownTimer = true;
        [SerializeField] private bool _flashScreen = true;
        [SerializeField] private bool _pulseUI = true;
        [SerializeField] private Color _normalColor = Color.green;
        [SerializeField] private Color _warningColor = Color.yellow;
        [SerializeField] private Color _criticalColor = Color.red;
        
        [Header("Audio")]
        [SerializeField] private bool _enableTickSound = true;
        [SerializeField] private float _tickVolume = 0.5f;
        [SerializeField] private bool _enableVoiceCountdown = true;
        [SerializeField] private AudioClip _tickSound;
        [SerializeField] private AudioClip _alarmSound;
        [SerializeField] private AudioClip[] _voiceNumbers;
        
        [Header("Heart Rate Simulation")]
        [SerializeField] private bool _simulateHeartbeat = true;
        [SerializeField] private float _baseHeartRate = 70f;
        [SerializeField] private float _maxHeartRate = 160f;
        [SerializeField] private AnimationCurve _heartRateCurve;
        
        [Header("Additional Pressure")]
        [SerializeField] private bool _showDeadlineMessages = true;
        [SerializeField] private string[] _pressureMessages = {
            "HURRY UP!", "TIME IS RUNNING OUT!", "FASTER!", "MOVE!", "CLOCK IS TICKING!"
        };
        
        // Interface implementation
        private string _name = "TimePressure";
        private float _intensity = 0.5f;
        private bool _isActive = false;
        private StressorParameters _parameters;
        
        // UI Components
        private Canvas _timerCanvas;
        private Text _timerText;
        private Image _screenFlash;
        private Image _heartbeatIndicator;
        
        // Internal state
        private float _currentTime;
        private Coroutine _pressureCoroutine;
        private AudioSource _tickSource;
        private AudioSource _heartbeatSource;
        private bool _inCountdown = false;
        
        // IStressor properties
        public string Name => _name;
        public float Intensity {
            get => _intensity;
            set => _intensity = Mathf.Clamp01(value);
        }
        public bool IsActive => _isActive;
        
        private void Awake() {
            SetupUI();
            SetupAudio();
            GenerateSounds();
            SetupHeartRateCurve();
        }
        
        /// <summary>
        /// Sets up UI components
        /// </summary>
        private void SetupUI() {
            // Create timer canvas
            GameObject canvasObject = new GameObject("TimePressureCanvas");
            canvasObject.transform.SetParent(transform);
            _timerCanvas = canvasObject.AddComponent<Canvas>();
            _timerCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _timerCanvas.sortingOrder = 100;
            
            // Create timer text
            GameObject timerObject = new GameObject("TimerText");
            timerObject.transform.SetParent(canvasObject.transform);
            _timerText = timerObject.AddComponent<Text>();
            _timerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _timerText.fontSize = 48;
            _timerText.alignment = TextAnchor.MiddleCenter;
            _timerText.color = _normalColor;
            
            RectTransform timerRect = timerObject.GetComponent<RectTransform>();
            timerRect.anchorMin = new Vector2(0.5f, 0.9f);
            timerRect.anchorMax = new Vector2(0.5f, 0.9f);
            timerRect.sizeDelta = new Vector2(400, 100);
            timerRect.anchoredPosition = Vector2.zero;
            
            // Create screen flash overlay
            GameObject flashObject = new GameObject("ScreenFlash");
            flashObject.transform.SetParent(canvasObject.transform);
            _screenFlash = flashObject.AddComponent<Image>();
            _screenFlash.color = new Color(1f, 0f, 0f, 0f);
            
            RectTransform flashRect = flashObject.GetComponent<RectTransform>();
            flashRect.anchorMin = Vector2.zero;
            flashRect.anchorMax = Vector2.one;
            flashRect.sizeDelta = Vector2.zero;
            flashRect.anchoredPosition = Vector2.zero;
            
            // Create heartbeat indicator
            GameObject heartObject = new GameObject("HeartbeatIndicator");
            heartObject.transform.SetParent(canvasObject.transform);
            _heartbeatIndicator = heartObject.AddComponent<Image>();
            _heartbeatIndicator.color = new Color(1f, 0f, 0f, 0.5f);
            
            RectTransform heartRect = heartObject.GetComponent<RectTransform>();
            heartRect.anchorMin = new Vector2(0.1f, 0.1f);
            heartRect.anchorMax = new Vector2(0.1f, 0.1f);
            heartRect.sizeDelta = new Vector2(50, 50);
            heartRect.anchoredPosition = Vector2.zero;
            
            // Start hidden
            canvasObject.SetActive(false);
        }
        
        /// <summary>
        /// Sets up audio sources
        /// </summary>
        private void SetupAudio() {
            // Tick sound source
            GameObject tickObject = new GameObject("TickSource");
            tickObject.transform.SetParent(transform);
            _tickSource = tickObject.AddComponent<AudioSource>();
            _tickSource.spatialBlend = 0f;
            _tickSource.loop = false;
            
            // Heartbeat source
            GameObject heartObject = new GameObject("HeartbeatSource");
            heartObject.transform.SetParent(transform);
            _heartbeatSource = heartObject.AddComponent<AudioSource>();
            _heartbeatSource.spatialBlend = 0f;
            _heartbeatSource.loop = true;
        }
        
        /// <summary>
        /// Generates sound effects
        /// </summary>
        private void GenerateSounds() {
            if (_tickSound == null) {
                _tickSound = GenerateTickSound();
            }
            
            if (_alarmSound == null) {
                _alarmSound = GenerateAlarmSound();
            }
            
            // Generate voice numbers if not provided
            if (_voiceNumbers == null || _voiceNumbers.Length < 10) {
                _voiceNumbers = new AudioClip[10];
                for (int i = 0; i < 10; i++) {
                    _voiceNumbers[i] = GenerateVoiceNumber(i);
                }
            }
            
            // Generate heartbeat
            _heartbeatSource.clip = GenerateHeartbeatSound();
        }
        
        /// <summary>
        /// Generates tick sound
        /// </summary>
        private AudioClip GenerateTickSound() {
            int sampleRate = 44100;
            float duration = 0.1f;
            int sampleLength = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[sampleLength];
            
            for (int i = 0; i < sampleLength; i++) {
                float t = (float)i / sampleLength;
                samples[i] = Mathf.Sin(2 * Mathf.PI * 1000f * t) * Mathf.Exp(-t * 20f) * 0.5f;
            }
            
            AudioClip clip = AudioClip.Create("TickSound", sampleLength, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
        
        /// <summary>
        /// Generates alarm sound
        /// </summary>
        private AudioClip GenerateAlarmSound() {
            int sampleRate = 44100;
            float duration = 0.5f;
            int sampleLength = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[sampleLength];
            
            for (int i = 0; i < sampleLength; i++) {
                float t = (float)i / sampleLength;
                float freq = Mathf.Lerp(800f, 1200f, t);
                samples[i] = Mathf.Sin(2 * Mathf.PI * freq * t) * 0.7f;
            }
            
            AudioClip clip = AudioClip.Create("AlarmSound", sampleLength, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
        
        /// <summary>
        /// Generates voice number
        /// </summary>
        private AudioClip GenerateVoiceNumber(int number) {
            int sampleRate = 44100;
            float duration = 0.5f;
            int sampleLength = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[sampleLength];
            
            // Simple voice synthesis for numbers
            float baseFreq = 200f - (number * 10f); // Different pitch for each number
            
            for (int i = 0; i < sampleLength; i++) {
                float t = (float)i / sampleLength;
                
                // Voice formants
                samples[i] = Mathf.Sin(2 * Mathf.PI * baseFreq * t);
                samples[i] += Mathf.Sin(2 * Mathf.PI * baseFreq * 2f * t) * 0.5f;
                samples[i] += Mathf.Sin(2 * Mathf.PI * baseFreq * 3f * t) * 0.3f;
                
                // Envelope
                float envelope = Mathf.Sin(Mathf.PI * t);
                samples[i] *= envelope * 0.3f;
            }
            
            AudioClip clip = AudioClip.Create($"Number_{number}", sampleLength, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
        
        /// <summary>
        /// Generates heartbeat sound
        /// </summary>
        private AudioClip GenerateHeartbeatSound() {
            int sampleRate = 44100;
            float duration = 1f;
            int sampleLength = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[sampleLength];
            
            // Two beats per second (lub-dub)
            for (int beat = 0; beat < 2; beat++) {
                int lubStart = beat * sampleRate / 2;
                int dubStart = lubStart + sampleRate / 8;
                
                // Lub (first heart sound)
                for (int i = 0; i < 1000 && lubStart + i < sampleLength; i++) {
                    float t = (float)i / 1000;
                    samples[lubStart + i] = Mathf.Sin(2 * Mathf.PI * 40f * t) * Mathf.Exp(-t * 10f) * 0.3f;
                }
                
                // Dub (second heart sound)
                for (int i = 0; i < 800 && dubStart + i < sampleLength; i++) {
                    float t = (float)i / 800;
                    samples[dubStart + i] = Mathf.Sin(2 * Mathf.PI * 50f * t) * Mathf.Exp(-t * 12f) * 0.25f;
                }
            }
            
            AudioClip clip = AudioClip.Create("Heartbeat", sampleLength, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
        
        /// <summary>
        /// Sets up heart rate curve
        /// </summary>
        private void SetupHeartRateCurve() {
            if (_heartRateCurve == null || _heartRateCurve.length == 0) {
                _heartRateCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            }
        }
        
        /// <summary>
        /// Initializes the time pressure stressor
        /// </summary>
        public void Initialize(StressorParameters parameters) {
            _parameters = parameters ?? new StressorParameters();
            _intensity = _parameters.intensity;
            
            if (_parameters.autoActivate) {
                Activate();
            }
        }
        
        /// <summary>
        /// Activates the time pressure stressor
        /// </summary>
        public void Activate() {
            if (_isActive) return;
            
            _isActive = true;
            _inCountdown = false;
            
            // Show UI
            if (_timerCanvas != null) {
                _timerCanvas.gameObject.SetActive(_showCountdownTimer);
            }
            
            // Start pressure coroutine
            if (_pressureCoroutine != null) {
                StopCoroutine(_pressureCoroutine);
            }
            _pressureCoroutine = StartCoroutine(PressureCoroutine());
            
            // Start heartbeat
            if (_simulateHeartbeat && _heartbeatSource != null) {
                _heartbeatSource.volume = 0.2f * _intensity;
                _heartbeatSource.Play();
            }
        }
        
        /// <summary>
        /// Deactivates the time pressure stressor
        /// </summary>
        public void Deactivate() {
            if (!_isActive) return;
            
            _isActive = false;
            _inCountdown = false;
            
            // Hide UI
            if (_timerCanvas != null) {
                _timerCanvas.gameObject.SetActive(false);
            }
            
            // Stop coroutine
            if (_pressureCoroutine != null) {
                StopCoroutine(_pressureCoroutine);
                _pressureCoroutine = null;
            }
            
            // Stop sounds
            if (_tickSource != null) {
                _tickSource.Stop();
            }
            if (_heartbeatSource != null) {
                _heartbeatSource.Stop();
            }
        }
        
        /// <summary>
        /// Main pressure coroutine
        /// </summary>
        private IEnumerator PressureCoroutine() {
            while (_isActive) {
                // Wait before starting countdown
                float waitTime = Random.Range(10f, 30f) * (2f - _intensity);
                yield return new WaitForSeconds(waitTime);
                
                if (!_isActive) break;
                
                // Start countdown
                yield return StartCountdown();
            }
        }
        
        /// <summary>
        /// Starts a countdown sequence
        /// </summary>
        private IEnumerator StartCountdown() {
            _inCountdown = true;
            _currentTime = _countdownDuration * (1.5f - _intensity * 0.5f);
            
            // Show pressure message
            if (_showDeadlineMessages) {
                ShowPressureMessage();
            }
            
            // Play alarm to signal countdown start
            if (_alarmSound != null) {
                AudioSource.PlayClipAtPoint(_alarmSound, Camera.main.transform.position, 0.5f);
            }
            
            float timeMultiplier = _accelerateTime ? _timeAcceleration : 1f;
            
            while (_currentTime > 0 && _inCountdown && _isActive) {
                _currentTime -= Time.deltaTime * timeMultiplier;
                
                // Update timer display
                UpdateTimerDisplay();
                
                // Update timer color
                UpdateTimerColor();
                
                // Tick sound
                if (_enableTickSound) {
                    PlayTickSound();
                }
                
                // Voice countdown for last 10 seconds
                if (_enableVoiceCountdown && _currentTime <= 10f && _currentTime > 0) {
                    PlayVoiceCountdown();
                }
                
                // Screen effects
                if (_flashScreen && _currentTime < _criticalThreshold) {
                    UpdateScreenFlash();
                }
                
                // Heartbeat
                if (_simulateHeartbeat) {
                    UpdateHeartbeat();
                }
                
                yield return null;
            }
            
            // Countdown finished
            _inCountdown = false;
            
            // Play final alarm
            if (_currentTime <= 0 && _alarmSound != null) {
                AudioSource.PlayClipAtPoint(_alarmSound, Camera.main.transform.position, 1f);
            }
            
            // Reset visuals
            ResetVisuals();
        }
        
        /// <summary>
        /// Updates the timer display
        /// </summary>
        private void UpdateTimerDisplay() {
            if (_timerText == null) return;
            
            if (_showMilliseconds) {
                _timerText.text = $"{Mathf.FloorToInt(_currentTime):00}:{Mathf.FloorToInt((_currentTime % 1f) * 100):00}";
            } else {
                _timerText.text = $"{Mathf.FloorToInt(_currentTime):00}";
            }
            
            // Pulse effect
            if (_pulseUI && _currentTime < _warningThreshold) {
                float pulse = Mathf.Sin(Time.time * Mathf.Lerp(2f, 10f, 1f - _currentTime / _warningThreshold));
                _timerText.transform.localScale = Vector3.one * (1f + pulse * 0.1f);
            }
        }
        
        /// <summary>
        /// Updates timer color based on time remaining
        /// </summary>
        private void UpdateTimerColor() {
            if (_timerText == null) return;
            
            if (_currentTime < _criticalThreshold) {
                _timerText.color = _criticalColor;
            } else if (_currentTime < _warningThreshold) {
                _timerText.color = _warningColor;
            } else {
                _timerText.color = _normalColor;
            }
        }
        
        /// <summary>
        /// Plays tick sound
        /// </summary>
        private void PlayTickSound() {
            if (_tickSource == null || _tickSound == null) return;
            
            // Increase tick rate as time runs out
            float tickInterval = _currentTime < _criticalThreshold ? 0.5f : 1f;
            
            if (!_tickSource.isPlaying) {
                _tickSource.clip = _tickSound;
                _tickSource.volume = _tickVolume * _intensity;
                _tickSource.pitch = _currentTime < _criticalThreshold ? 1.2f : 1f;
                _tickSource.Play();
            }
        }
        
        /// <summary>
        /// Plays voice countdown
        /// </summary>
        private void PlayVoiceCountdown() {
            int seconds = Mathf.FloorToInt(_currentTime);
            if (seconds < 10 && seconds >= 0 && _voiceNumbers != null && seconds < _voiceNumbers.Length) {
                // Check if we just hit a new second
                if (Mathf.Approximately(_currentTime % 1f, 0f)) {
                    AudioSource.PlayClipAtPoint(_voiceNumbers[seconds], Camera.main.transform.position, 0.7f);
                }
            }
        }
        
        /// <summary>
        /// Updates screen flash effect
        /// </summary>
        private void UpdateScreenFlash() {
            if (_screenFlash == null) return;
            
            float flashIntensity = (1f - _currentTime / _criticalThreshold) * 0.3f;
            float flash = Mathf.Sin(Time.time * 10f);
            _screenFlash.color = new Color(1f, 0f, 0f, flashIntensity * (flash + 1f) / 2f);
        }
        
        /// <summary>
        /// Updates heartbeat simulation
        /// </summary>
        private void UpdateHeartbeat() {
            if (_heartbeatSource == null) return;
            
            // Calculate heart rate based on time remaining
            float stressLevel = 1f - (_currentTime / _countdownDuration);
            float heartRate = Mathf.Lerp(_baseHeartRate, _maxHeartRate, _heartRateCurve.Evaluate(stressLevel) * _intensity);
            
            // Adjust playback speed
            _heartbeatSource.pitch = heartRate / _baseHeartRate;
            _heartbeatSource.volume = 0.2f + (stressLevel * 0.3f * _intensity);
            
            // Update visual indicator
            if (_heartbeatIndicator != null) {
                float beat = Mathf.Sin(Time.time * heartRate / 30f);
                _heartbeatIndicator.transform.localScale = Vector3.one * (1f + beat * 0.2f);
                _heartbeatIndicator.color = new Color(1f, 0f, 0f, 0.3f + beat * 0.2f);
            }
        }
        
        /// <summary>
        /// Shows a pressure message
        /// </summary>
        private void ShowPressureMessage() {
            if (_pressureMessages == null || _pressureMessages.Length == 0) return;
            
            string message = _pressureMessages[Random.Range(0, _pressureMessages.Length)];
            
            // Could integrate with HUD system
            HUDController hud = FindObjectOfType<HUDController>();
            if (hud != null) {
                hud.ShowNotification(message, NotificationType.Warning);
            }
        }
        
        /// <summary>
        /// Resets visual effects
        /// </summary>
        private void ResetVisuals() {
            if (_timerText != null) {
                _timerText.text = "";
                _timerText.transform.localScale = Vector3.one;
            }
            
            if (_screenFlash != null) {
                _screenFlash.color = new Color(1f, 0f, 0f, 0f);
            }
            
            if (_heartbeatIndicator != null) {
                _heartbeatIndicator.transform.localScale = Vector3.one;
            }
        }
        
        /// <summary>
        /// Updates the stressor
        /// </summary>
        public void UpdateStressor() {
            // Intensity affects countdown frequency and speed
        }
        
        /// <summary>
        /// Gets current parameters
        /// </summary>
        public StressorParameters GetParameters() {
            return _parameters;
        }
        
        /// <summary>
        /// Updates parameters at runtime
        /// </summary>
        public void UpdateParameters(StressorParameters parameters) {
            _parameters = parameters;
            _intensity = _parameters.intensity;
        }
        
        private void OnDestroy() {
            if (_isActive) {
                Deactivate();
            }
        }
    }
}