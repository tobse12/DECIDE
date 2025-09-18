/*
 * Author: Tobias Sorg
 * Date: 2025-01-15
 * Description: Implements distorted radio chatter stressor for the DECIDE VR framework
 * License: GPLv3
 */

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using DECIDE.Stressors;

namespace DECIDE.Stressors.Implementations {
    /// <summary>
    /// Implements distorted radio communications as audio distraction
    /// </summary>
    public class RadioChatterStressor : MonoBehaviour, IStressor {
        [Header("Radio Settings")]
        [SerializeField] private AudioSource _radioSource;
        [SerializeField] private float _minVolume = 0.2f;
        [SerializeField] private float _maxVolume = 0.6f;
        [SerializeField] private float _staticIntensity = 0.3f;
        
        [Header("Message Settings")]
        [SerializeField] private float _minMessageInterval = 5f;
        [SerializeField] private float _maxMessageInterval = 15f;
        [SerializeField] private bool _includeUrgentMessages = true;
        [SerializeField] private float _urgentMessageChance = 0.2f;
        
        [Header("Distortion Effects")]
        [SerializeField] private bool _useDistortion = true;
        [SerializeField] private AnimationCurve _distortionCurve;
        [SerializeField] private float _frequencyShiftRange = 200f;
        
        [Header("Spatial Audio")]
        [SerializeField] private bool _use3DAudio = true;
        [SerializeField] private float _minDistance = 1f;
        [SerializeField] private float _maxDistance = 10f;
        
        // Radio message templates
        private readonly string[] _normalMessages = {
            "Alpha team, report status, over.",
            "Roger that, moving to sector seven.",
            "Negative contact at grid reference two-three.",
            "Copy, maintain current position.",
            "Bravo six going dark.",
            "Requesting backup at checkpoint alpha.",
            "All units, be advised, situation normal.",
            "Charlie team in position, awaiting orders."
        };
        
        private readonly string[] _urgentMessages = {
            "Contact! Contact! Hostile spotted!",
            "Man down! Need immediate medical!",
            "Taking heavy fire! Request immediate support!",
            "IED detected! Clear the area!",
            "Lost visual on target! All units alert!",
            "Breach! Breach! They're inside the perimeter!"
        };
        
        private readonly string[] _staticPhrases = {
            "...bzzt... can't... krzzzt... position...",
            "...static... repeat last... bzzt...",
            "...breaking up... krzzt... say again...",
            "...interference... bzzt... unclear..."
        };
        
        // Interface implementation
        private string _name = "RadioChatter";
        private float _intensity = 0.5f;
        private bool _isActive = false;
        private StressorParameters _parameters;
        
        // Internal state
        private AudioClip _staticNoise;
        private AudioClip[] _generatedMessages;
        private Coroutine _chatterCoroutine;
        private AudioSource _staticSource;
        private List<AudioSource> _activeSources;
        
        // IStressor properties
        public string Name => _name;
        public float Intensity {
            get => _intensity;
            set => _intensity = Mathf.Clamp01(value);
        }
        public bool IsActive => _isActive;
        
        private void Awake() {
            SetupAudioSources();
            GenerateRadioSounds();
            _activeSources = new List<AudioSource>();
            
            // Default distortion curve
            if (_distortionCurve == null || _distortionCurve.length == 0) {
                _distortionCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
            }
        }
        
        /// <summary>
        /// Sets up audio sources for radio and static
        /// </summary>
        private void SetupAudioSources() {
            // Main radio source
            if (_radioSource == null) {
                GameObject radioObject = new GameObject("RadioSource");
                radioObject.transform.SetParent(transform);
                _radioSource = radioObject.AddComponent<AudioSource>();
            }
            
            _radioSource.spatialBlend = _use3DAudio ? 1f : 0f;
            _radioSource.minDistance = _minDistance;
            _radioSource.maxDistance = _maxDistance;
            _radioSource.rolloffMode = AudioRolloffMode.Linear;
            
            // Static noise source
            GameObject staticObject = new GameObject("StaticSource");
            staticObject.transform.SetParent(transform);
            _staticSource = staticObject.AddComponent<AudioSource>();
            _staticSource.spatialBlend = 0f;
            _staticSource.loop = true;
            _staticSource.volume = 0f;
        }
        
        /// <summary>
        /// Generates radio sounds and static noise
        /// </summary>
        private void GenerateRadioSounds() {
            // Generate static noise
            _staticNoise = GenerateStaticNoise();
            _staticSource.clip = _staticNoise;
            
            // Generate synthesized voice messages
            GenerateVoiceMessages();
        }
        
        /// <summary>
        /// Generates static noise audio
        /// </summary>
        private AudioClip GenerateStaticNoise() {
            int sampleRate = 44100;
            int seconds = 10;
            float[] samples = new float[sampleRate * seconds];
            
            for (int i = 0; i < samples.Length; i++) {
                // White noise
                samples[i] = Random.Range(-1f, 1f) * 0.3f;
                
                // Add some crackles
                if (Random.value < 0.001f) {
                    samples[i] = Random.Range(-1f, 1f);
                }
                
                // Radio frequency sweep
                float sweep = Mathf.Sin(2 * Mathf.PI * (100 + Random.Range(0, 500)) * i / sampleRate);
                samples[i] = samples[i] * 0.7f + sweep * 0.3f;
            }
            
            AudioClip clip = AudioClip.Create("RadioStatic", samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
        
        /// <summary>
        /// Generates synthesized voice messages
        /// </summary>
        private void GenerateVoiceMessages() {
            List<AudioClip> messages = new List<AudioClip>();
            
            // Generate normal messages
            foreach (string text in _normalMessages) {
                messages.Add(GenerateSynthesizedVoice(text, false));
            }
            
            // Generate urgent messages
            foreach (string text in _urgentMessages) {
                messages.Add(GenerateSynthesizedVoice(text, true));
            }
            
            _generatedMessages = messages.ToArray();
        }
        
        /// <summary>
        /// Generates a synthesized voice clip (simplified)
        /// </summary>
        private AudioClip GenerateSynthesizedVoice(string text, bool urgent) {
            int sampleRate = 44100;
            float duration = text.Length * 0.1f; // Approximate duration
            int sampleLength = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[sampleLength];
            
            // Simple voice synthesis (placeholder for actual TTS)
            float baseFrequency = urgent ? 250f : 200f;
            float modulation = urgent ? 50f : 20f;
            
            for (int i = 0; i < sampleLength; i++) {
                float t = (float)i / sampleRate;
                
                // Fundamental frequency
                float fundamental = Mathf.Sin(2 * Mathf.PI * baseFrequency * t);
                
                // Harmonics for voice-like quality
                float harmonic1 = Mathf.Sin(2 * Mathf.PI * baseFrequency * 2 * t) * 0.5f;
                float harmonic2 = Mathf.Sin(2 * Mathf.PI * baseFrequency * 3 * t) * 0.3f;
                
                // Modulation for speech patterns
                float mod = Mathf.Sin(2 * Mathf.PI * modulation * t);
                
                samples[i] = (fundamental + harmonic1 + harmonic2) * 0.3f * (1f + mod * 0.2f);
                
                // Add radio filter effect
                samples[i] = ApplyRadioFilter(samples[i], i, sampleRate);
                
                // Add urgency effect
                if (urgent) {
                    samples[i] *= 1f + Random.Range(-0.1f, 0.1f);
                }
            }
            
            AudioClip clip = AudioClip.Create($"RadioMessage_{text.GetHashCode()}", 
                                             sampleLength, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
        
        /// <summary>
        /// Applies radio filter effect to audio sample
        /// </summary>
        private float ApplyRadioFilter(float sample, int index, int sampleRate) {
            // Bandpass filter simulation (300Hz - 3400Hz typical for radio)
            // Simplified high-pass
            float highpassed = sample - (sample * 0.9f);
            
            // Add slight distortion
            if (Mathf.Abs(highpassed) > 0.7f) {
                highpassed = Mathf.Sign(highpassed) * 0.7f;
            }
            
            // Add compression
            highpassed *= 2f;
            highpassed = Mathf.Clamp(highpassed, -1f, 1f);
            
            return highpassed;
        }
        
        /// <summary>
        /// Initializes the radio chatter stressor
        /// </summary>
        public void Initialize(StressorParameters parameters) {
            _parameters = parameters ?? new StressorParameters();
            _intensity = _parameters.intensity;
            
            if (_parameters.autoActivate) {
                Activate();
            }
        }
        
        /// <summary>
        /// Activates the radio chatter stressor
        /// </summary>
        public void Activate() {
            if (_isActive) return;
            
            _isActive = true;
            
            // Start static noise
            if (_staticSource != null) {
                _staticSource.volume = _staticIntensity * _intensity * 0.5f;
                _staticSource.Play();
            }
            
            // Start chatter coroutine
            if (_chatterCoroutine != null) {
                StopCoroutine(_chatterCoroutine);
            }
            _chatterCoroutine = StartCoroutine(RadioChatterCoroutine());
        }
        
        /// <summary>
        /// Deactivates the radio chatter stressor
        /// </summary>
        public void Deactivate() {
            if (!_isActive) return;
            
            _isActive = false;
            
            // Stop static
            if (_staticSource != null) {
                _staticSource.Stop();
            }
            
            // Stop chatter
            if (_chatterCoroutine != null) {
                StopCoroutine(_chatterCoroutine);
                _chatterCoroutine = null;
            }
            
            // Stop all active messages
            foreach (var source in _activeSources) {
                if (source != null) {
                    source.Stop();
                    Destroy(source.gameObject);
                }
            }
            _activeSources.Clear();
        }
        
        /// <summary>
        /// Main radio chatter coroutine
        /// </summary>
        private IEnumerator RadioChatterCoroutine() {
            while (_isActive) {
                // Wait for next message
                float waitTime = Random.Range(_minMessageInterval, _maxMessageInterval) * (2f - _intensity);
                yield return new WaitForSeconds(waitTime);
                
                if (!_isActive) break;
                
                // Determine message type
                bool isUrgent = _includeUrgentMessages && Random.value < _urgentMessageChance * _intensity;
                
                // Play message
                yield return PlayRadioMessage(isUrgent);
            }
        }
        
        /// <summary>
        /// Plays a radio message
        /// </summary>
        private IEnumerator PlayRadioMessage(bool urgent) {
            // Select message
            AudioClip message = null;
            if (_generatedMessages != null && _generatedMessages.Length > 0) {
                int index = urgent ? 
                    Random.Range(_normalMessages.Length, _generatedMessages.Length) :
                    Random.Range(0, _normalMessages.Length);
                    
                if (index < _generatedMessages.Length) {
                    message = _generatedMessages[index];
                }
            }
            
            if (message == null) {
                yield break;
            }
            
            // Create temporary audio source for overlapping messages
            GameObject messageObject = new GameObject($"RadioMessage_{Time.time}");
            messageObject.transform.SetParent(transform);
            AudioSource messageSource = messageObject.AddComponent<AudioSource>();
            
            // Configure audio source
            messageSource.clip = message;
            messageSource.volume = Mathf.Lerp(_minVolume, _maxVolume, _intensity) * (urgent ? 1.2f : 1f);
            messageSource.spatialBlend = _use3DAudio ? 1f : 0f;
            messageSource.pitch = 1f + Random.Range(-0.1f, 0.1f); // Slight pitch variation
            
            // Position in 3D space
            if (_use3DAudio && Camera.main != null) {
                Vector3 randomDirection = Random.onUnitSphere;
                randomDirection.y = 0;
                messageSource.transform.position = Camera.main.transform.position + randomDirection * Random.Range(_minDistance, _maxDistance);
            }
            
            // Apply distortion
            if (_useDistortion) {
                StartCoroutine(ApplyDistortion(messageSource));
            }
            
            // Play message
            messageSource.Play();
            _activeSources.Add(messageSource);
            
            // Add radio beep at start
            PlayRadioBeep(messageSource.transform.position);
            
            // Wait for message to finish
            yield return new WaitForSeconds(message.length);
            
            // Cleanup
            _activeSources.Remove(messageSource);
            Destroy(messageObject);
        }
        
        /// <summary>
        /// Plays a radio beep sound
        /// </summary>
        private void PlayRadioBeep(Vector3 position) {
            // Generate beep
            int sampleRate = 44100;
            float duration = 0.2f;
            int sampleLength = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[sampleLength];
            
            for (int i = 0; i < sampleLength; i++) {
                float t = (float)i / sampleLength;
                float envelope = Mathf.Sin(Mathf.PI * t); // Fade in/out
                samples[i] = Mathf.Sin(2 * Mathf.PI * 1000 * i / sampleRate) * envelope * 0.3f;
            }
            
            AudioClip beep = AudioClip.Create("RadioBeep", sampleLength, 1, sampleRate, false);
            beep.SetData(samples, 0);
            
            AudioSource.PlayClipAtPoint(beep, position, 0.5f);
        }
        
        /// <summary>
        /// Applies distortion effects to audio source
        /// </summary>
        private IEnumerator ApplyDistortion(AudioSource source) {
            float originalPitch = source.pitch;
            
            while (source != null && source.isPlaying) {
                // Apply distortion based on curve
                float distortion = _distortionCurve.Evaluate(Random.value) * _intensity;
                
                // Pitch shifting
                source.pitch = originalPitch + Random.Range(-distortion * 0.2f, distortion * 0.2f);
                
                // Volume ducking
                source.volume *= (1f - distortion * 0.3f);
                
                yield return new WaitForSeconds(Random.Range(0.1f, 0.3f));
            }
        }
        
        /// <summary>
        /// Updates the radio chatter stressor
        /// </summary>
        public void UpdateStressor() {
            if (!_isActive) return;
            
            // Update static volume
            if (_staticSource != null) {
                _staticSource.volume = _staticIntensity * _intensity * 0.5f;
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
            if (_isActive) {
                Deactivate();
            }
        }
    }
}