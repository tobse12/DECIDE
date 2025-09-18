/*
 * Author: Tobias Sorg
 * Date: 2025-01-15
 * Description: Implements decision conflict through avatar type switching for the DECIDE VR framework
 * License: GPLv3
 */

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using DECIDE.Stressors;
using DECIDE.Core;
using DECIDE.Avatars;
using DECIDE.Events;

namespace DECIDE.Stressors.Implementations {
    /// <summary>
    /// Makes avatars change their appearance/type during runtime to create decision conflicts
    /// </summary>
    public class DecisionConflictStressor : MonoBehaviour, IStressor {
        [Header("Conflict Settings")]
        [SerializeField] private float _minSwitchInterval = 5f;
        [SerializeField] private float _maxSwitchInterval = 15f;
        [SerializeField] private int _maxSimultaneousSwitches = 2;
        [SerializeField] private float _switchDuration = 0.5f;
        
        [Header("Switch Types")]
        [SerializeField] private bool _allowHostileToFriendly = true;
        [SerializeField] private bool _allowFriendlyToHostile = true;
        [SerializeField] private bool _allowUnknownSwitches = true;
        [SerializeField] private float _warningTime = 1f; // Visual warning before switch
        
        [Header("Visual Effects")]
        [SerializeField] private bool _useVisualWarning = true;
        [SerializeField] private Color _warningColor = Color.yellow;
        [SerializeField] private float _blinkFrequency = 5f;
        [SerializeField] private Material _transitionMaterial;
        
        [Header("Audio")]
        [SerializeField] private AudioClip _switchSound;
        [SerializeField] private AudioClip _warningSound;
        [SerializeField] private float _soundVolume = 0.5f;
        
        // Interface implementation
        private string _name = "DecisionConflict";
        private float _intensity = 0.5f;
        private bool _isActive = false;
        private StressorParameters _parameters;
        
        // Internal state
        private Coroutine _conflictCoroutine;
        private List<AvatarController> _switchingAvatars;
        private Dictionary<int, Coroutine> _activeSwitches;
        private float _nextSwitchTime;
        
        // IStressor properties
        public string Name => _name;
        public float Intensity {
            get => _intensity;
            set => _intensity = Mathf.Clamp01(value);
        }
        public bool IsActive => _isActive;
        
        private void Awake() {
            _switchingAvatars = new List<AvatarController>();
            _activeSwitches = new Dictionary<int, Coroutine>();
            GenerateSounds();
        }
        
        /// <summary>
        /// Generates sound effects if not provided
        /// </summary>
        private void GenerateSounds() {
            if (_switchSound == null) {
                _switchSound = GenerateTransitionSound();
            }
            if (_warningSound == null) {
                _warningSound = GenerateWarningSound();
            }
        }
        
        /// <summary>
        /// Generates a transition sound effect
        /// </summary>
        private AudioClip GenerateTransitionSound() {
            int sampleRate = 44100;
            float duration = 0.5f;
            int sampleLength = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[sampleLength];
            
            for (int i = 0; i < sampleLength; i++) {
                float t = (float)i / sampleLength;
                
                // Swoosh effect
                float frequency = Mathf.Lerp(500f, 2000f, t);
                samples[i] = Mathf.Sin(2 * Mathf.PI * frequency * t) * (1f - t) * 0.5f;
                
                // Add some noise
                samples[i] += Random.Range(-0.1f, 0.1f) * (1f - t);
            }
            
            AudioClip clip = AudioClip.Create("SwitchSound", sampleLength, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
        
        /// <summary>
        /// Generates a warning sound effect
        /// </summary>
        private AudioClip GenerateWarningSound() {
            int sampleRate = 44100;
            float duration = 0.3f;
            int sampleLength = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[sampleLength];
            
            for (int i = 0; i < sampleLength; i++) {
                float t = (float)i / sampleLength;
                
                // Two-tone warning
                float frequency = i < sampleLength / 2 ? 800f : 1200f;
                float envelope = Mathf.Sin(Mathf.PI * t);
                samples[i] = Mathf.Sin(2 * Mathf.PI * frequency * i / sampleRate) * envelope * 0.3f;
            }
            
            AudioClip clip = AudioClip.Create("WarningSound", sampleLength, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
        
        /// <summary>
        /// Initializes the decision conflict stressor
        /// </summary>
        public void Initialize(StressorParameters parameters) {
            _parameters = parameters ?? new StressorParameters();
            _intensity = _parameters.intensity;
            
            if (_parameters.autoActivate) {
                Activate();
            }
        }
        
        /// <summary>
        /// Activates the decision conflict stressor
        /// </summary>
        public void Activate() {
            if (_isActive) return;
            
            _isActive = true;
            _nextSwitchTime = Time.time + Random.Range(_minSwitchInterval, _maxSwitchInterval);
            
            // Enable appearance changing for avatars
            EnableAvatarSwitching(true);
            
            if (_conflictCoroutine != null) {
                StopCoroutine(_conflictCoroutine);
            }
            _conflictCoroutine = StartCoroutine(ConflictCoroutine());
        }
        
        /// <summary>
        /// Deactivates the decision conflict stressor
        /// </summary>
        public void Deactivate() {
            if (!_isActive) return;
            
            _isActive = false;
            
            // Disable appearance changing
            EnableAvatarSwitching(false);
            
            // Stop all active switches
            foreach (var kvp in _activeSwitches) {
                if (kvp.Value != null) {
                    StopCoroutine(kvp.Value);
                }
            }
            _activeSwitches.Clear();
            _switchingAvatars.Clear();
            
            if (_conflictCoroutine != null) {
                StopCoroutine(_conflictCoroutine);
                _conflictCoroutine = null;
            }
        }
        
        /// <summary>
        /// Enables or disables avatar switching capability
        /// </summary>
        private void EnableAvatarSwitching(bool enable) {
            // Find scenario manager
            ScenarioManager scenarioManager = ScenarioManager.Instance;
            if (scenarioManager == null) return;
            
            // This would need to be implemented in the actual avatar spawning
            // For now, we'll handle it through direct avatar manipulation
        }
        
        /// <summary>
        /// Main conflict generation coroutine
        /// </summary>
        private IEnumerator ConflictCoroutine() {
            while (_isActive) {
                // Wait for next switch time
                float waitTime = Random.Range(_minSwitchInterval, _maxSwitchInterval) * (2f - _intensity);
                yield return new WaitForSeconds(waitTime);
                
                if (!_isActive) break;
                
                // Find available avatars to switch
                List<AvatarController> availableAvatars = FindSwitchableAvatars();
                
                if (availableAvatars.Count > 0) {
                    // Select avatars to switch
                    int switchCount = Mathf.Min(
                        Random.Range(1, _maxSimultaneousSwitches + 1),
                        availableAvatars.Count
                    );
                    
                    for (int i = 0; i < switchCount; i++) {
                        int randomIndex = Random.Range(0, availableAvatars.Count);
                        AvatarController avatar = availableAvatars[randomIndex];
                        availableAvatars.RemoveAt(randomIndex);
                        
                        // Start switch coroutine
                        int avatarId = avatar.GetInstanceID();
                        if (!_activeSwitches.ContainsKey(avatarId)) {
                            _activeSwitches[avatarId] = StartCoroutine(SwitchAvatarType(avatar));
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// Finds avatars that can be switched
        /// </summary>
        private List<AvatarController> FindSwitchableAvatars() {
            List<AvatarController> availableAvatars = new List<AvatarController>();
            
            // Get all active avatars from scenario
            ScenarioManager scenarioManager = ScenarioManager.Instance;
            if (scenarioManager == null) return availableAvatars;
            
            // Find all avatar controllers in scene
            AvatarController[] allAvatars = FindObjectsOfType<AvatarController>();
            
            foreach (var avatar in allAvatars) {
                // Check if avatar is active and not already switching
                if (avatar.gameObject.activeInHierarchy && 
                    !_switchingAvatars.Contains(avatar) &&
                    !avatar.HasBeenTargeted) // Don't switch if being targeted
                {
                    // Check if switch type is allowed
                    bool canSwitch = false;
                    
                    switch (avatar.Type) {
                        case AvatarType.Hostile:
                            canSwitch = _allowHostileToFriendly;
                            break;
                        case AvatarType.Friendly:
                            canSwitch = _allowFriendlyToHostile;
                            break;
                        case AvatarType.Unknown:
                            canSwitch = _allowUnknownSwitches;
                            break;
                    }
                    
                    if (canSwitch) {
                        availableAvatars.Add(avatar);
                    }
                }
            }
            
            return availableAvatars;
        }
        
        /// <summary>
        /// Switches an avatar's type with warning
        /// </summary>
        private IEnumerator SwitchAvatarType(AvatarController avatar) {
            if (avatar == null) yield break;
            
            _switchingAvatars.Add(avatar);
            int avatarId = avatar.GetInstanceID();
            AvatarType originalType = avatar.Type;
            
            // Visual warning phase
            if (_useVisualWarning && _warningTime > 0) {
                // Play warning sound
                if (_warningSound != null) {
                    AudioSource.PlayClipAtPoint(_warningSound, avatar.transform.position, _soundVolume);
                }
                
                // Blink effect
                Renderer renderer = avatar.GetComponentInChildren<Renderer>();
                Material originalMaterial = null;
                if (renderer != null) {
                    originalMaterial = renderer.material;
                }
                
                float warningElapsed = 0f;
                while (warningElapsed < _warningTime) {
                    if (avatar == null) yield break;
                    
                    warningElapsed += Time.deltaTime;
                    
                    // Blinking effect
                    if (renderer != null) {
                        float blink = Mathf.Sin(warningElapsed * _blinkFrequency * 2 * Mathf.PI);
                        renderer.material.color = Color.Lerp(Color.white, _warningColor, (blink + 1f) / 2f);
                    }
                    
                    yield return null;
                }
                
                // Restore original material
                if (renderer != null && originalMaterial != null) {
                    renderer.material = originalMaterial;
                }
            }
            
            // Perform the switch
            if (avatar != null) {
                // Determine new type
                AvatarType newType = GetSwitchedType(originalType);
                
                // Play switch sound
                if (_switchSound != null) {
                    AudioSource.PlayClipAtPoint(_switchSound, avatar.transform.position, _soundVolume);
                }
                
                // Transition effect
                yield return TransitionEffect(avatar, _switchDuration);
                
                // Change avatar type
                if (avatar != null) {
                    avatar.ChangeAvatarType();
                    
                    // Log event
                    Debug.Log($"Avatar {avatarId} switched from {originalType} to {newType}");
                    
                    // Trigger custom event if needed
                    TriggerConflictEvent(avatarId, originalType, newType);
                }
            }
            
            // Cleanup
            _switchingAvatars.Remove(avatar);
            _activeSwitches.Remove(avatarId);
        }
        
        /// <summary>
        /// Determines the new type after switch
        /// </summary>
        private AvatarType GetSwitchedType(AvatarType currentType) {
            List<AvatarType> possibleTypes = new List<AvatarType>();
            
            switch (currentType) {
                case AvatarType.Hostile:
                    if (_allowHostileToFriendly) {
                        possibleTypes.Add(AvatarType.Friendly);
                        if (_allowUnknownSwitches) possibleTypes.Add(AvatarType.Unknown);
                    }
                    break;
                    
                case AvatarType.Friendly:
                    if (_allowFriendlyToHostile) {
                        possibleTypes.Add(AvatarType.Hostile);
                        if (_allowUnknownSwitches) possibleTypes.Add(AvatarType.Unknown);
                    }
                    break;
                    
                case AvatarType.Unknown:
                    if (_allowUnknownSwitches) {
                        if (_allowFriendlyToHostile) possibleTypes.Add(AvatarType.Hostile);
                        if (_allowHostileToFriendly) possibleTypes.Add(AvatarType.Friendly);
                    }
                    break;
            }
            
            if (possibleTypes.Count > 0) {
                return possibleTypes[Random.Range(0, possibleTypes.Count)];
            }
            
            // If no switch is possible, return a different type anyway
            return currentType == AvatarType.Hostile ? AvatarType.Friendly : AvatarType.Hostile;
        }
        
        /// <summary>
        /// Creates a transition effect during type switch
        /// </summary>
        private IEnumerator TransitionEffect(AvatarController avatar, float duration) {
            if (avatar == null) yield break;
            
            Renderer renderer = avatar.GetComponentInChildren<Renderer>();
            if (renderer == null) yield break;
            
            Material originalMaterial = renderer.material;
            
            // Use transition material if available
            if (_transitionMaterial != null) {
                renderer.material = _transitionMaterial;
            }
            
            float elapsed = 0f;
            while (elapsed < duration) {
                if (avatar == null) yield break;
                
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                
                // Dissolve or fade effect
                if (renderer.material.HasProperty("_Dissolve")) {
                    renderer.material.SetFloat("_Dissolve", Mathf.PingPong(t * 2f, 1f));
                } else {
                    // Simple fade
                    Color color = renderer.material.color;
                    color.a = Mathf.PingPong(t * 2f, 1f);
                    renderer.material.color = color;
                }
                
                yield return null;
            }
            
            // Restore material (avatar controller will update to new type material)
            if (renderer != null) {
                renderer.material = originalMaterial;
            }
        }
        
        /// <summary>
        /// Triggers a conflict event for metrics
        /// </summary>
        private void TriggerConflictEvent(int avatarId, AvatarType oldType, AvatarType newType) {
            // This could trigger a custom event for metrics tracking
            // For now, we'll just log it
            var eventData = new Dictionary<string, object> {
                ["avatarId"] = avatarId,
                ["oldType"] = oldType.ToString(),
                ["newType"] = newType.ToString(),
                ["timestamp"] = System.DateTime.Now
            };
            
            // Could send to MetricsManager
            MetricsManager metricsManager = MetricsManager.Instance;
            if (metricsManager != null) {
                metricsManager.RecordDataPoint("DecisionConflict", eventData);
            }
        }
        
        /// <summary>
        /// Updates the decision conflict stressor
        /// </summary>
        public void UpdateStressor() {
            // Intensity affects switch frequency and count
            // Handled in the coroutine
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