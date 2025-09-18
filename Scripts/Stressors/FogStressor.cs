/*
 * Author: Tobias Sorg
 * Date: 2025-01-15
 * Description: Implements fog visual stressor for the DECIDE VR framework
 * License: GPLv3
 */

using UnityEngine;
using UnityEngine.Rendering;
using DECIDE.Stressors;

namespace DECIDE.Stressors.Implementations {
    /// <summary>
    /// Implements fog effect to reduce visibility
    /// </summary>
    public class FogStressor : MonoBehaviour, IStressor {
        [Header("Fog Settings")]
        [SerializeField] private float _minFogDensity = 0.01f;
        [SerializeField] private float _maxFogDensity = 0.15f;
        [SerializeField] private Color _fogColor = new Color(0.5f, 0.5f, 0.5f, 1f);
        [SerializeField] private FogMode _fogMode = FogMode.Exponential;
        
        // Interface implementation
        private string _name = "Fog";
        private float _intensity = 0.5f;
        private bool _isActive = false;
        private StressorParameters _parameters;
        
        // Internal state
        private bool _originalFogEnabled;
        private float _originalFogDensity;
        private Color _originalFogColor;
        private FogMode _originalFogMode;
        private float _currentFogDensity;
        private float _targetFogDensity;
        private float _fadeStartTime;
        
        // IStressor properties
        public string Name => _name;
        public float Intensity {
            get => _intensity;
            set => _intensity = Mathf.Clamp01(value);
        }
        public bool IsActive => _isActive;
        
        /// <summary>
        /// Initializes the fog stressor
        /// </summary>
        public void Initialize(StressorParameters parameters) {
            _parameters = parameters ?? new StressorParameters();
            _intensity = _parameters.intensity;
            
            // Store original fog settings
            _originalFogEnabled = RenderSettings.fog;
            _originalFogDensity = RenderSettings.fogDensity;
            _originalFogColor = RenderSettings.fogColor;
            _originalFogMode = RenderSettings.fogMode;
            
            if (_parameters.autoActivate) {
                Activate();
            }
        }
        
        /// <summary>
        /// Activates the fog stressor
        /// </summary>
        public void Activate() {
            if (_isActive) return;
            
            _isActive = true;
            RenderSettings.fog = true;
            RenderSettings.fogMode = _fogMode;
            RenderSettings.fogColor = _fogColor;
            
            _targetFogDensity = Mathf.Lerp(_minFogDensity, _maxFogDensity, _intensity);
            _fadeStartTime = Time.time;
            _currentFogDensity = RenderSettings.fogDensity;
        }
        
        /// <summary>
        /// Deactivates the fog stressor
        /// </summary>
        public void Deactivate() {
            if (!_isActive) return;
            
            _isActive = false;
            _targetFogDensity = _originalFogEnabled ? _originalFogDensity : 0f;
            _fadeStartTime = Time.time;
        }
        
        /// <summary>
        /// Updates the fog stressor
        /// </summary>
        public void UpdateStressor() {
            if (!_isActive && Mathf.Approximately(_currentFogDensity, _targetFogDensity)) {
                if (!_originalFogEnabled) {
                    RenderSettings.fog = false;
                }
                return;
            }
            
            // Update fog density based on intensity
            if (_isActive) {
                _targetFogDensity = Mathf.Lerp(_minFogDensity, _maxFogDensity, _intensity);
            }
            
            // Smooth transition
            float fadeTime = _isActive ? _parameters.fadeInTime : _parameters.fadeOutTime;
            float elapsed = Time.time - _fadeStartTime;
            float t = Mathf.Clamp01(elapsed / fadeTime);
            
            _currentFogDensity = Mathf.Lerp(_currentFogDensity, _targetFogDensity, t);
            RenderSettings.fogDensity = _currentFogDensity;
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
            // Restore original fog settings
            RenderSettings.fog = _originalFogEnabled;
            RenderSettings.fogDensity = _originalFogDensity;
            RenderSettings.fogColor = _originalFogColor;
            RenderSettings.fogMode = _originalFogMode;
        }
    }
}