/*
 * Author: Tobias Sorg
 * Date: 2025-01-15
 * Description: Implements earthquake/camera shake stressor for the DECIDE VR framework
 * License: GPLv3
 */

using UnityEngine;
using System.Collections;
using DECIDE.Stressors;

namespace DECIDE.Stressors.Implementations {
    /// <summary>
    /// Implements earthquake effect with camera shaking
    /// </summary>
    public class EarthquakeStressor : MonoBehaviour, IStressor {
        [Header("Shake Settings")]
        [SerializeField] private float _minShakeIntensity = 0.01f;
        [SerializeField] private float _maxShakeIntensity = 0.2f;
        [SerializeField] private float _minShakeFrequency = 5f;
        [SerializeField] private float _maxShakeFrequency = 15f;
        [SerializeField] private AnimationCurve _shakePattern;
        [SerializeField] private bool _shakePosition = true;
        [SerializeField] private bool _shakeRotation = true;
        
        [Header("Audio")]
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _earthquakeSound;
        [SerializeField] private float _minVolume = 0.1f;
        [SerializeField] private float _maxVolume = 0.5f;
        
        // Interface implementation
        private string _name = "Earthquake";
        private float _intensity = 0.5f;
        private bool _isActive = false;
        private StressorParameters _parameters;
        
        // Internal state
        private Transform _cameraTransform;
        private Vector3 _originalCameraPosition;
        private Quaternion _originalCameraRotation;
        private Coroutine _shakeCoroutine;
        private float _currentShakeIntensity;
        private float _shakeTime;
        
        // IStressor properties
        public string Name => _name;
        public float Intensity {
            get => _intensity;
            set => _intensity = Mathf.Clamp01(value);
        }
        public bool IsActive => _isActive;
        
        private void Awake() {
            // Find VR camera
            _cameraTransform = Camera.main?.transform;
            
            // Setup audio source
            if (_audioSource == null) {
                _audioSource = gameObject.AddComponent<AudioSource>();
            }
            _audioSource.loop = true;
            _audioSource.clip = _earthquakeSound;
            
            // Default shake pattern
            if (_shakePattern == null || _shakePattern.length == 0) {
                _shakePattern = AnimationCurve.Linear(0f, 1f, 1f, 1f);
            }
        }
        
        /// <summary>
        /// Initializes the earthquake stressor
        /// </summary>
        public void Initialize(StressorParameters parameters) {
            _parameters = parameters ?? new StressorParameters();
            _intensity = _parameters.intensity;
            
            if (_cameraTransform != null) {
                _originalCameraPosition = _cameraTransform.localPosition;
                _originalCameraRotation = _cameraTransform.localRotation;
            }
            
            if (_parameters.autoActivate) {
                Activate();
            }
        }
        
        /// <summary>
        /// Activates the earthquake stressor
        /// </summary>
        public void Activate() {
            if (_isActive || _cameraTransform == null) return;
            
            _isActive = true;
            _shakeTime = 0f;
            
            // Start shake coroutine
            if (_shakeCoroutine != null) {
                StopCoroutine(_shakeCoroutine);
            }
            _shakeCoroutine = StartCoroutine(ShakeCoroutine());
            
            // Start audio
            if (_audioSource != null && _earthquakeSound != null) {
                _audioSource.volume = Mathf.Lerp(_minVolume, _maxVolume, _intensity);
                _audioSource.Play();
            }
        }
        
        /// <summary>
        /// Deactivates the earthquake stressor
        /// </summary>
        public void Deactivate() {
            if (!_isActive) return;
            
            _isActive = false;
            
            // Stop shake coroutine
            if (_shakeCoroutine != null) {
                StopCoroutine(_shakeCoroutine);
                _shakeCoroutine = null;
            }
            
            // Restore camera position
            if (_cameraTransform != null) {
                StartCoroutine(RestoreCameraPosition());
            }
            
            // Stop audio
            if (_audioSource != null && _audioSource.isPlaying) {
                StartCoroutine(FadeOutAudio());
            }
        }
        
        /// <summary>
        /// Main shake coroutine
        /// </summary>
        private IEnumerator ShakeCoroutine() {
            while (_isActive) {
                _shakeTime += Time.deltaTime;
                
                // Calculate current shake intensity
                float patternValue = _shakePattern.Evaluate((_shakeTime * Mathf.Lerp(_minShakeFrequency, _maxShakeFrequency, _intensity)) % 1f);
                _currentShakeIntensity = Mathf.Lerp(_minShakeIntensity, _maxShakeIntensity, _intensity * patternValue);
                
                // Apply shake
                if (_shakePosition) {
                    Vector3 shakeOffset = new Vector3(
                        Random.Range(-1f, 1f) * _currentShakeIntensity,
                        Random.Range(-1f, 1f) * _currentShakeIntensity * 0.5f,
                        Random.Range(-1f, 1f) * _currentShakeIntensity * 0.3f
                    );
                    _cameraTransform.localPosition = _originalCameraPosition + shakeOffset;
                }
                
                if (_shakeRotation) {
                    Quaternion shakeRotation = Quaternion.Euler(
                        Random.Range(-1f, 1f) * _currentShakeIntensity * 10f,
                        Random.Range(-1f, 1f) * _currentShakeIntensity * 10f,
                        Random.Range(-1f, 1f) * _currentShakeIntensity * 5f
                    );
                    _cameraTransform.localRotation = _originalCameraRotation * shakeRotation;
                }
                
                yield return null;
            }
        }
        
        /// <summary>
        /// Restores camera to original position
        /// </summary>
        private IEnumerator RestoreCameraPosition() {
            float elapsed = 0f;
            float duration = _parameters.fadeOutTime;
            
            Vector3 startPos = _cameraTransform.localPosition;
            Quaternion startRot = _cameraTransform.localRotation;
            
            while (elapsed < duration) {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                
                _cameraTransform.localPosition = Vector3.Lerp(startPos, _originalCameraPosition, t);
                _cameraTransform.localRotation = Quaternion.Slerp(startRot, _originalCameraRotation, t);
                
                yield return null;
            }
            
            _cameraTransform.localPosition = _originalCameraPosition;
            _cameraTransform.localRotation = _originalCameraRotation;
        }
        
        /// <summary>
        /// Fades out earthquake audio
        /// </summary>
        private IEnumerator FadeOutAudio() {
            float startVolume = _audioSource.volume;
            float elapsed = 0f;
            float duration = _parameters.fadeOutTime;
            
            while (elapsed < duration) {
                elapsed += Time.deltaTime;
                _audioSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / duration);
                yield return null;
            }
            
            _audioSource.Stop();
            _audioSource.volume = startVolume;
        }
        
        /// <summary>
        /// Updates the earthquake stressor
        /// </summary>
        public void UpdateStressor() {
            if (!_isActive) return;
            
            // Update audio volume based on intensity
            if (_audioSource != null && _audioSource.isPlaying) {
                _audioSource.volume = Mathf.Lerp(_minVolume, _maxVolume, _intensity);
            }
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
            // Stop all coroutines
            StopAllCoroutines();
            
            // Restore camera
            if (_cameraTransform != null) {
                _cameraTransform.localPosition = _originalCameraPosition;
                _cameraTransform.localRotation = _originalCameraRotation;
            }
        }
    }
}