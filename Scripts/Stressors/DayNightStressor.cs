/*
 * Author: Tobias Sorg
 * Date: 2025-01-15
 * Description: Implements day/night cycle changes for the DECIDE VR framework
 * License: GPLv3
 */

using UnityEngine;
using System.Collections;
using DECIDE.Stressors;

namespace DECIDE.Stressors.Implementations {
    /// <summary>
    /// Implements dynamic day/night cycle to affect visibility and atmosphere
    /// </summary>
    public class DayNightStressor : MonoBehaviour, IStressor {
        [Header("Cycle Settings")]
        [SerializeField] private float _fullCycleDuration = 120f; // 2 minutes for full cycle
        [SerializeField] private bool _startAtNight = false;
        [SerializeField] private AnimationCurve _lightIntensityCurve;
        
        [Header("Sun/Moon Settings")]
        [SerializeField] private Light _sunLight;
        [SerializeField] private float _sunIntensityDay = 1.2f;
        [SerializeField] private float _sunIntensityNight = 0f;
        [SerializeField] private float _moonIntensity = 0.3f;
        [SerializeField] private Vector3 _sunRotationAxis = new Vector3(1, 0.2f, 0);
        
        [Header("Sky Settings")]
        [SerializeField] private Gradient _skyColorGradient;
        [SerializeField] private Gradient _horizonColorGradient;
        [SerializeField] private Gradient _groundColorGradient;
        [SerializeField] private Gradient _fogColorGradient;
        
        [Header("Environmental Lighting")]
        [SerializeField] private bool _affectStreetLights = true;
        [SerializeField] private float _streetLightThreshold = 0.3f;
        
        [Header("Audio")]
        [SerializeField] private AudioClip _dayAmbience;
        [SerializeField] private AudioClip _nightAmbience;
        [SerializeField] private AudioSource _ambienceSource;
        
        // Interface implementation
        private string _name = "DayNightCycle";
        private float _intensity = 0.5f;
        private bool _isActive = false;
        private StressorParameters _parameters;
        
        // Internal state
        private float _currentTimeOfDay = 0.5f; // 0 = midnight, 0.5 = noon, 1 = midnight
        private Light _moonLight;
        private Light[] _streetLights;
        private Coroutine _cycleCoroutine;
        private float _cycleSpeed;
        
        // Original settings
        private float _originalSunIntensity;
        private Color _originalSkyColor;
        private Color _originalEquatorColor;
        private Color _originalGroundColor;
        private Color _originalFogColor;
        
        // IStressor properties
        public string Name => _name;
        public float Intensity {
            get => _intensity;
            set {
                _intensity = Mathf.Clamp01(value);
                UpdateCycleSpeed();
            }
        }
        public bool IsActive => _isActive;
        
        private void Awake() {
            SetupLights();
            SetupGradients();
            SetupAudio();
            FindStreetLights();
        }
        
        /// <summary>
        /// Sets up sun and moon lights
        /// </summary>
        private void SetupLights() {
            // Find or create sun light
            if (_sunLight == null) {
                _sunLight = RenderSettings.sun;
                if (_sunLight == null) {
                    GameObject sun = GameObject.Find("Directional Light");
                    if (sun != null) {
                        _sunLight = sun.GetComponent<Light>();
                    } else {
                        sun = new GameObject("Sun Light");
                        _sunLight = sun.AddComponent<Light>();
                        _sunLight.type = LightType.Directional;
                    }
                }
            }
            
            // Create moon light
            GameObject moon = new GameObject("Moon Light");
            moon.transform.SetParent(transform);
            _moonLight = moon.AddComponent<Light>();
            _moonLight.type = LightType.Directional;
            _moonLight.color = new Color(0.7f, 0.7f, 0.9f);
            _moonLight.intensity = 0f;
            _moonLight.shadows = LightShadows.Soft;
            
            // Store original settings
            _originalSunIntensity = _sunLight.intensity;
        }
        
        /// <summary>
        /// Sets up color gradients for the cycle
        /// </summary>
        private void SetupGradients() {
            // Default light intensity curve
            if (_lightIntensityCurve == null || _lightIntensityCurve.length == 0) {
                _lightIntensityCurve = AnimationCurve.EaseInOut(0f, 0f, 0.5f, 1f);
                _lightIntensityCurve.AddKey(1f, 0f);
            }
            
            // Sky color gradient (midnight -> dawn -> noon -> dusk -> midnight)
            if (_skyColorGradient == null || _skyColorGradient.colorKeys.Length == 0) {
                _skyColorGradient = new Gradient();
                GradientColorKey[] skyColors = new GradientColorKey[5];
                skyColors[0] = new GradientColorKey(new Color(0.05f, 0.05f, 0.1f), 0f);    // Midnight
                skyColors[1] = new GradientColorKey(new Color(0.4f, 0.3f, 0.5f), 0.25f);   // Dawn
                skyColors[2] = new GradientColorKey(new Color(0.5f, 0.7f, 0.9f), 0.5f);    // Noon
                skyColors[3] = new GradientColorKey(new Color(0.8f, 0.4f, 0.2f), 0.75f);   // Dusk
                skyColors[4] = new GradientColorKey(new Color(0.05f, 0.05f, 0.1f), 1f);    // Midnight
                
                GradientAlphaKey[] skyAlpha = new GradientAlphaKey[2];
                skyAlpha[0] = new GradientAlphaKey(1f, 0f);
                skyAlpha[1] = new GradientAlphaKey(1f, 1f);
                
                _skyColorGradient.SetKeys(skyColors, skyAlpha);
            }
            
            // Similar setup for other gradients
            SetupDefaultGradient(ref _horizonColorGradient, 
                new Color(0.1f, 0.1f, 0.15f),  // Night
                new Color(0.9f, 0.6f, 0.4f),   // Dawn/Dusk
                new Color(0.6f, 0.7f, 0.8f));  // Day
            
            SetupDefaultGradient(ref _groundColorGradient,
                new Color(0.02f, 0.02f, 0.03f), // Night
                new Color(0.3f, 0.2f, 0.1f),    // Dawn/Dusk
                new Color(0.2f, 0.2f, 0.2f));   // Day
            
            SetupDefaultGradient(ref _fogColorGradient,
                new Color(0.02f, 0.02f, 0.05f), // Night fog
                new Color(0.4f, 0.3f, 0.3f),    // Dawn/Dusk fog
                new Color(0.7f, 0.7f, 0.7f));   // Day fog
            
            // Store original ambient settings
            _originalSkyColor = RenderSettings.ambientSkyColor;
            _originalEquatorColor = RenderSettings.ambientEquatorColor;
            _originalGroundColor = RenderSettings.ambientGroundColor;
            _originalFogColor = RenderSettings.fogColor;
        }
        
        /// <summary>
        /// Sets up a default gradient
        /// </summary>
        private void SetupDefaultGradient(ref Gradient gradient, Color night, Color transition, Color day) {
            if (gradient == null || gradient.colorKeys.Length == 0) {
                gradient = new Gradient();
                GradientColorKey[] colors = new GradientColorKey[5];
                colors[0] = new GradientColorKey(night, 0f);
                colors[1] = new GradientColorKey(transition, 0.25f);
                colors[2] = new GradientColorKey(day, 0.5f);
                colors[3] = new GradientColorKey(transition, 0.75f);
                colors[4] = new GradientColorKey(night, 1f);
                
                GradientAlphaKey[] alpha = new GradientAlphaKey[2];
                alpha[0] = new GradientAlphaKey(1f, 0f);
                alpha[1] = new GradientAlphaKey(1f, 1f);
                
                gradient.SetKeys(colors, alpha);
            }
        }
        
        /// <summary>
        /// Sets up audio components
        /// </summary>
        private void SetupAudio() {
            if (_ambienceSource == null) {
                _ambienceSource = gameObject.AddComponent<AudioSource>();
            }
            _ambienceSource.loop = true;
            _ambienceSource.spatialBlend = 0f;
            _ambienceSource.volume = 0.3f;
            
            // Generate ambience if not provided
            if (_dayAmbience == null) {
                _dayAmbience = GenerateAmbience(true);
            }
            if (_nightAmbience == null) {
                _nightAmbience = GenerateAmbience(false);
            }
        }
        
        /// <summary>
        /// Generates basic ambience sounds
        /// </summary>
        private AudioClip GenerateAmbience(bool isDay) {
            int sampleRate = 44100;
            int seconds = 10;
            float[] samples = new float[sampleRate * seconds];
            
            for (int i = 0; i < samples.Length; i++) {
                if (isDay) {
                    // Day: birds chirping simulation
                    float t = (float)i / sampleRate;
                    samples[i] = 0;
                    
                    // Add bird-like chirps
                    if (i % (sampleRate / 2) < 1000) {
                        samples[i] += Mathf.Sin(2 * Mathf.PI * Random.Range(2000, 4000) * t) * 0.05f;
                    }
                } else {
                    // Night: crickets simulation
                    samples[i] = Random.Range(-0.02f, 0.02f);
                    if (i % 100 == 0) {
                        samples[i] += Mathf.Sin(2 * Mathf.PI * 1000 * i / sampleRate) * 0.03f;
                    }
                }
            }
            
            AudioClip clip = AudioClip.Create(isDay ? "DayAmbience" : "NightAmbience", 
                                             samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
        
        /// <summary>
        /// Finds all street lights in the scene
        /// </summary>
        private void FindStreetLights() {
            GameObject[] lamps = GameObject.FindGameObjectsWithTag("StreetLight");
            if (lamps.Length == 0) {
                // Try finding by name
                GameObject[] allLamps = GameObject.FindObjectsOfType<GameObject>();
                System.Collections.Generic.List<Light> lights = new System.Collections.Generic.List<Light>();
                
                foreach (var obj in allLamps) {
                    if (obj.name.Contains("Lamp") || obj.name.Contains("StreetLight")) {
                        Light light = obj.GetComponentInChildren<Light>();
                        if (light != null) {
                            lights.Add(light);
                        }
                    }
                }
                
                _streetLights = lights.ToArray();
            } else {
                System.Collections.Generic.List<Light> lights = new System.Collections.Generic.List<Light>();
                foreach (var lamp in lamps) {
                    Light light = lamp.GetComponentInChildren<Light>();
                    if (light != null) {
                        lights.Add(light);
                    }
                }
                _streetLights = lights.ToArray();
            }
        }
        
        /// <summary>
        /// Initializes the day/night stressor
        /// </summary>
        public void Initialize(StressorParameters parameters) {
            _parameters = parameters ?? new StressorParameters();
            _intensity = _parameters.intensity;
            
            _currentTimeOfDay = _startAtNight ? 0f : 0.5f;
            UpdateCycleSpeed();
            
            if (_parameters.autoActivate) {
                Activate();
            }
        }
        
        /// <summary>
        /// Updates the cycle speed based on intensity
        /// </summary>
        private void UpdateCycleSpeed() {
            // Higher intensity = faster cycle
            float speedMultiplier = 0.5f + (_intensity * 2f);
            _cycleSpeed = (1f / _fullCycleDuration) * speedMultiplier;
        }
        
        /// <summary>
        /// Activates the day/night stressor
        /// </summary>
        public void Activate() {
            if (_isActive) return;
            
            _isActive = true;
            
            if (_cycleCoroutine != null) {
                StopCoroutine(_cycleCoroutine);
            }
            _cycleCoroutine = StartCoroutine(DayNightCycle());
        }
        
        /// <summary>
        /// Deactivates the day/night stressor
        /// </summary>
        public void Deactivate() {
            if (!_isActive) return;
            
            _isActive = false;
            
            if (_cycleCoroutine != null) {
                StopCoroutine(_cycleCoroutine);
                _cycleCoroutine = null;
            }
            
            // Restore original settings
            RestoreOriginalLighting();
        }
        
        /// <summary>
        /// Main day/night cycle coroutine
        /// </summary>
        private IEnumerator DayNightCycle() {
            while (_isActive) {
                // Update time of day
                _currentTimeOfDay += _cycleSpeed * Time.deltaTime;
                if (_currentTimeOfDay > 1f) {
                    _currentTimeOfDay -= 1f;
                }
                
                UpdateLighting();
                UpdateAmbience();
                UpdateStreetLights();
                
                yield return null;
            }
        }
        
        /// <summary>
        /// Updates all lighting based on time of day
        /// </summary>
        private void UpdateLighting() {
            // Sun rotation (0 = midnight pointing down, 0.5 = noon pointing down)
            float sunAngle = (_currentTimeOfDay - 0.25f) * 360f;
            _sunLight.transform.rotation = Quaternion.Euler(sunAngle, 30f, 0f);
            
            // Moon rotation (opposite of sun)
            float moonAngle = sunAngle + 180f;
            _moonLight.transform.rotation = Quaternion.Euler(moonAngle, -30f, 0f);
            
            // Light intensities
            float dayIntensity = _lightIntensityCurve.Evaluate(_currentTimeOfDay);
            _sunLight.intensity = Mathf.Lerp(_sunIntensityNight, _sunIntensityDay, dayIntensity);
            _moonLight.intensity = Mathf.Lerp(_moonIntensity, 0f, dayIntensity);
            
            // Sun color (warmer at dawn/dusk)
            float colorTemp = Mathf.Abs(_currentTimeOfDay - 0.5f) * 2f;
            _sunLight.color = Color.Lerp(Color.white, new Color(1f, 0.8f, 0.6f), colorTemp);
            
            // Ambient lighting
            RenderSettings.ambientSkyColor = _skyColorGradient.Evaluate(_currentTimeOfDay);
            RenderSettings.ambientEquatorColor = _horizonColorGradient.Evaluate(_currentTimeOfDay);
            RenderSettings.ambientGroundColor = _groundColorGradient.Evaluate(_currentTimeOfDay);
            
            // Fog
            if (RenderSettings.fog) {
                RenderSettings.fogColor = _fogColorGradient.Evaluate(_currentTimeOfDay);
                
                // More fog at night
                float baseFogDensity = RenderSettings.fogDensity;
                RenderSettings.fogDensity = baseFogDensity * (1f + (1f - dayIntensity) * 0.5f);
            }
        }
        
        /// <summary>
        /// Updates ambient sounds
        /// </summary>
        private void UpdateAmbience() {
            if (_ambienceSource == null) return;
            
            float dayIntensity = _lightIntensityCurve.Evaluate(_currentTimeOfDay);
            
            // Crossfade between day and night ambience
            if (dayIntensity > 0.5f && _ambienceSource.clip != _dayAmbience) {
                _ambienceSource.clip = _dayAmbience;
                if (!_ambienceSource.isPlaying) {
                    _ambienceSource.Play();
                }
            } else if (dayIntensity <= 0.5f && _ambienceSource.clip != _nightAmbience) {
                _ambienceSource.clip = _nightAmbience;
                if (!_ambienceSource.isPlaying) {
                    _ambienceSource.Play();
                }
            }
            
            _ambienceSource.volume = 0.3f * _intensity;
        }
        
        /// <summary>
        /// Updates street light states
        /// </summary>
        private void UpdateStreetLights() {
            if (!_affectStreetLights || _streetLights == null) return;
            
            float dayIntensity = _lightIntensityCurve.Evaluate(_currentTimeOfDay);
            bool lightsOn = dayIntensity < _streetLightThreshold;
            
            foreach (var light in _streetLights) {
                if (light != null) {
                    light.enabled = lightsOn;
                }
            }
        }
        
        /// <summary>
        /// Restores original lighting settings
        /// </summary>
        private void RestoreOriginalLighting() {
            if (_sunLight != null) {
                _sunLight.intensity = _originalSunIntensity;
                _sunLight.color = Color.white;
                _sunLight.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            }
            
            if (_moonLight != null) {
                _moonLight.intensity = 0f;
            }
            
            RenderSettings.ambientSkyColor = _originalSkyColor;
            RenderSettings.ambientEquatorColor = _originalEquatorColor;
            RenderSettings.ambientGroundColor = _originalGroundColor;
            RenderSettings.fogColor = _originalFogColor;
            
            // Turn street lights off (assuming day default)
            if (_streetLights != null) {
                foreach (var light in _streetLights) {
                    if (light != null) {
                        light.enabled = false;
                    }
                }
            }
        }
        
        /// <summary>
        /// Updates the stressor
        /// </summary>
        public void UpdateStressor() {
            // Cycle speed is controlled by intensity
            UpdateCycleSpeed();
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
            UpdateCycleSpeed();
        }
        
        private void OnDestroy() {
            if (_isActive) {
                Deactivate();
            }
            
            if (_moonLight != null) {
                Destroy(_moonLight.gameObject);
            }
        }
    }
}