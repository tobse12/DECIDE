/*
 * Author: Tobias Sorg
 * Date: 2025-01-15
 * Description: Implements environmental chaos effects for the DECIDE VR framework
 * License: GPLv3
 */

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using DECIDE.Stressors;

namespace DECIDE.Stressors.Implementations {
    /// <summary>
    /// Creates environmental chaos through explosions, smoke, debris, and alarms
    /// </summary>
    public class EnvironmentalChaosStressor : MonoBehaviour, IStressor {
        [Header("Explosion Settings")]
        [SerializeField] private float _minExplosionInterval = 10f;
        [SerializeField] private float _maxExplosionInterval = 30f;
        [SerializeField] private float _explosionRadius = 10f;
        [SerializeField] private float _explosionForce = 500f;
        [SerializeField] private float _explosionDuration = 2f;
        
        [Header("Smoke Effects")]
        [SerializeField] private bool _enableSmoke = true;
        [SerializeField] private int _maxSmokeColumns = 5;
        [SerializeField] private float _smokeDuration = 30f;
        [SerializeField] private Color _smokeColor = new Color(0.3f, 0.3f, 0.3f, 0.8f);
        
        [Header("Debris")]
        [SerializeField] private bool _enableDebris = true;
        [SerializeField] private int _debrisCount = 20;
        [SerializeField] private float _debrisVelocity = 10f;
        [SerializeField] private float _debrisLifetime = 5f;
        
        [Header("Alarms")]
        [SerializeField] private bool _enableAlarms = true;
        [SerializeField] private float _alarmVolume = 0.5f;
        [SerializeField] private float _sirenFrequency = 2f;
        
        [Header("Environmental Effects")]
        [SerializeField] private bool _flickerLights = true;
        [SerializeField] private bool _breakWindows = true;
        [SerializeField] private float _screenShakeIntensity = 0.3f;
        
        [Header("Audio")]
        [SerializeField] private AudioClip[] _explosionSounds;
        [SerializeField] private AudioClip _alarmSound;
        [SerializeField] private AudioClip _debrisSound;
        [SerializeField] private AudioClip _glassBreakSound;
        
        // Interface implementation
        private string _name = "EnvironmentalChaos";
        private float _intensity = 0.5f;
        private bool _isActive = false;
        private StressorParameters _parameters;
        
        // Internal state
        private Coroutine _chaosCoroutine;
        private List<GameObject> _activeSmokeEffects;
        private List<GameObject> _activeDebris;
        private AudioSource _alarmSource;
        private Light[] _sceneLights;
        private float _originalLightIntensity;
        
        // IStressor properties
        public string Name => _name;
        public float Intensity {
            get => _intensity;
            set => _intensity = Mathf.Clamp01(value);
        }
        public bool IsActive => _isActive;
        
        private void Awake() {
            _activeSmokeEffects = new List<GameObject>();
            _activeDebris = new List<GameObject>();
            
            SetupAudio();
            FindSceneLights();
            GenerateSounds();
        }
        
        /// <summary>
        /// Sets up audio sources
        /// </summary>
        private void SetupAudio() {
            _alarmSource = gameObject.AddComponent<AudioSource>();
            _alarmSource.loop = true;
            _alarmSource.spatialBlend = 0f;
            _alarmSource.volume = 0f;
        }
        
        /// <summary>
        /// Finds all lights in the scene
        /// </summary>
        private void FindSceneLights() {
            _sceneLights = FindObjectsOfType<Light>();
            if (_sceneLights.Length > 0) {
                _originalLightIntensity = _sceneLights[0].intensity;
            }
        }
        
        /// <summary>
        /// Generates sound effects if not provided
        /// </summary>
        private void GenerateSounds() {
            // Generate explosion sounds if not provided
            if (_explosionSounds == null || _explosionSounds.Length == 0) {
                _explosionSounds = new AudioClip[3];
                for (int i = 0; i < 3; i++) {
                    _explosionSounds[i] = GenerateExplosionSound(i);
                }
            }
            
            if (_alarmSound == null) {
                _alarmSound = GenerateAlarmSound();
            }
            
            if (_debrisSound == null) {
                _debrisSound = GenerateDebrisSound();
            }
            
            if (_glassBreakSound == null) {
                _glassBreakSound = GenerateGlassBreakSound();
            }
        }
        
        /// <summary>
        /// Generates an explosion sound
        /// </summary>
        private AudioClip GenerateExplosionSound(int variant) {
            int sampleRate = 44100;
            float duration = 2f;
            int sampleLength = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[sampleLength];
            
            for (int i = 0; i < sampleLength; i++) {
                float t = (float)i / sampleLength;
                
                // Initial blast
                if (t < 0.1f) {
                    samples[i] = Random.Range(-1f, 1f) * (1f - t * 10f);
                }
                // Rumble
                else {
                    float rumble = Mathf.Sin(2 * Mathf.PI * Random.Range(20f, 60f) * t);
                    samples[i] = rumble * Mathf.Exp(-t * 2f) * 0.5f;
                    samples[i] += Random.Range(-0.1f, 0.1f) * Mathf.Exp(-t * 3f);
                }
            }
            
            AudioClip clip = AudioClip.Create($"Explosion_{variant}", sampleLength, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
        
        /// <summary>
        /// Generates an alarm sound
        /// </summary>
        private AudioClip GenerateAlarmSound() {
            int sampleRate = 44100;
            int sampleLength = sampleRate * 2; // 2 second loop
            float[] samples = new float[sampleLength];
            
            for (int i = 0; i < sampleLength; i++) {
                float t = (float)i / sampleRate;
                
                // Two-tone siren
                float frequency = (t % 1f < 0.5f) ? 800f : 600f;
                samples[i] = Mathf.Sin(2 * Mathf.PI * frequency * t) * 0.3f;
            }
            
            AudioClip clip = AudioClip.Create("AlarmSound", sampleLength, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
        
        /// <summary>
        /// Generates debris falling sound
        /// </summary>
        private AudioClip GenerateDebrisSound() {
            int sampleRate = 44100;
            float duration = 1f;
            int sampleLength = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[sampleLength];
            
            for (int i = 0; i < sampleLength; i++) {
                float t = (float)i / sampleLength;
                
                // Multiple impacts
                samples[i] = 0;
                for (int j = 0; j < 5; j++) {
                    float impactTime = j * 0.2f;
                    if (Mathf.Abs(t - impactTime) < 0.05f) {
                        samples[i] += Random.Range(-0.5f, 0.5f) * Mathf.Exp(-Mathf.Abs(t - impactTime) * 50f);
                    }
                }
            }
            
            AudioClip clip = AudioClip.Create("DebrisSound", sampleLength, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
        
        /// <summary>
        /// Generates glass breaking sound
        /// </summary>
        private AudioClip GenerateGlassBreakSound() {
            int sampleRate = 44100;
            float duration = 0.5f;
            int sampleLength = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[sampleLength];
            
            for (int i = 0; i < sampleLength; i++) {
                float t = (float)i / sampleLength;
                
                // High frequency crash
                float frequency = Random.Range(3000f, 6000f);
                samples[i] = Mathf.Sin(2 * Mathf.PI * frequency * t) * Mathf.Exp(-t * 10f) * 0.5f;
                samples[i] += Random.Range(-0.3f, 0.3f) * Mathf.Exp(-t * 5f);
            }
            
            AudioClip clip = AudioClip.Create("GlassBreak", sampleLength, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
        
        /// <summary>
        /// Initializes the environmental chaos stressor
        /// </summary>
        public void Initialize(StressorParameters parameters) {
            _parameters = parameters ?? new StressorParameters();
            _intensity = _parameters.intensity;
            
            if (_parameters.autoActivate) {
                Activate();
            }
        }
        
        /// <summary>
        /// Activates the environmental chaos stressor
        /// </summary>
        public void Activate() {
            if (_isActive) return;
            
            _isActive = true;
            
            // Start alarms
            if (_enableAlarms && _alarmSound != null) {
                _alarmSource.clip = _alarmSound;
                _alarmSource.volume = _alarmVolume * _intensity;
                _alarmSource.Play();
            }
            
            // Start chaos coroutine
            if (_chaosCoroutine != null) {
                StopCoroutine(_chaosCoroutine);
            }
            _chaosCoroutine = StartCoroutine(ChaosCoroutine());
            
            // Start light flickering
            if (_flickerLights) {
                StartCoroutine(FlickerLights());
            }
        }
        
        /// <summary>
        /// Deactivates the environmental chaos stressor
        /// </summary>
        public void Deactivate() {
            if (!_isActive) return;
            
            _isActive = false;
            
            // Stop alarms
            if (_alarmSource != null && _alarmSource.isPlaying) {
                _alarmSource.Stop();
            }
            
            // Stop coroutines
            if (_chaosCoroutine != null) {
                StopCoroutine(_chaosCoroutine);
                _chaosCoroutine = null;
            }
            
            // Clean up effects
            CleanupEffects();
            
            // Restore lights
            RestoreLights();
        }
        
        /// <summary>
        /// Main chaos generation coroutine
        /// </summary>
        private IEnumerator ChaosCoroutine() {
            while (_isActive) {
                // Wait for next event
                float waitTime = Random.Range(_minExplosionInterval, _maxExplosionInterval) * (2f - _intensity);
                yield return new WaitForSeconds(waitTime);
                
                if (!_isActive) break;
                
                // Trigger random chaos event
                int eventType = Random.Range(0, 3);
                switch (eventType) {
                    case 0:
                        yield return TriggerExplosion();
                        break;
                    case 1:
                        if (_enableSmoke) CreateSmokeColumn();
                        break;
                    case 2:
                        if (_enableDebris) CreateDebrisShower();
                        break;
                }
            }
        }
        
        /// <summary>
        /// Triggers an explosion effect
        /// </summary>
        private IEnumerator TriggerExplosion() {
            // Determine explosion position
            Vector3 explosionPos = GetRandomExplosionPosition();
            
            // Play explosion sound
            if (_explosionSounds != null && _explosionSounds.Length > 0) {
                AudioClip sound = _explosionSounds[Random.Range(0, _explosionSounds.Length)];
                AudioSource.PlayClipAtPoint(sound, explosionPos, 1f);
            }
            
            // Create visual explosion
            GameObject explosion = CreateExplosionVisual(explosionPos);
            
            // Camera shake
            if (Camera.main != null) {
                StartCoroutine(ShakeCamera(_screenShakeIntensity * _intensity, _explosionDuration));
            }
            
            // Apply explosion force to nearby objects
            ApplyExplosionForce(explosionPos);
            
            // Break nearby windows
            if (_breakWindows) {
                BreakNearbyWindows(explosionPos);
            }
            
            // Create smoke after explosion
            if (_enableSmoke) {
                yield return new WaitForSeconds(0.5f);
                CreateSmokeAtPosition(explosionPos);
            }
            
            // Cleanup explosion visual after duration
            yield return new WaitForSeconds(_explosionDuration);
            if (explosion != null) {
                Destroy(explosion);
            }
        }
        
        /// <summary>
        /// Gets a random position for explosion
        /// </summary>
        private Vector3 GetRandomExplosionPosition() {
            if (Camera.main != null) {
                Vector3 playerPos = Camera.main.transform.position;
                Vector3 randomOffset = Random.onUnitSphere * Random.Range(10f, 30f);
                randomOffset.y = Random.Range(0f, 5f);
                return playerPos + randomOffset;
            }
            return Vector3.zero;
        }
        
        /// <summary>
        /// Creates explosion visual effect
        /// </summary>
        private GameObject CreateExplosionVisual(Vector3 position) {
            GameObject explosion = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            explosion.name = "Explosion";
            explosion.transform.position = position;
            explosion.transform.localScale = Vector3.one * 0.1f;
            
            // Remove collider
            Destroy(explosion.GetComponent<Collider>());
            
            // Setup material
            Renderer renderer = explosion.GetComponent<Renderer>();
            Material mat = new Material(Shader.Find("Unlit/Color"));
            mat.color = new Color(1f, 0.5f, 0f, 0.8f);
            renderer.material = mat;
            
            // Animate expansion
            StartCoroutine(AnimateExplosion(explosion));
            
            return explosion;
        }
        
        /// <summary>
        /// Animates explosion expansion
        /// </summary>
        private IEnumerator AnimateExplosion(GameObject explosion) {
            float elapsed = 0f;
            float duration = 0.5f;
            
            while (elapsed < duration) {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                
                // Expand
                explosion.transform.localScale = Vector3.one * Mathf.Lerp(0.1f, _explosionRadius, t);
                
                // Fade
                Renderer renderer = explosion.GetComponent<Renderer>();
                if (renderer != null) {
                    Color color = renderer.material.color;
                    color.a = 1f - t;
                    renderer.material.color = color;
                }
                
                yield return null;
            }
        }
        
        /// <summary>
        /// Applies explosion force to nearby objects
        /// </summary>
        private void ApplyExplosionForce(Vector3 explosionPos) {
            Collider[] colliders = Physics.OverlapSphere(explosionPos, _explosionRadius);
            
            foreach (Collider col in colliders) {
                Rigidbody rb = col.GetComponent<Rigidbody>();
                if (rb != null) {
                    rb.AddExplosionForce(_explosionForce * _intensity, explosionPos, _explosionRadius);
                }
            }
        }
        
        /// <summary>
        /// Creates a smoke column
        /// </summary>
        private void CreateSmokeColumn() {
            if (_activeSmokeEffects.Count >= _maxSmokeColumns) {
                // Remove oldest smoke
                GameObject oldSmoke = _activeSmokeEffects[0];
                _activeSmokeEffects.RemoveAt(0);
                Destroy(oldSmoke);
            }
            
            Vector3 position = GetRandomExplosionPosition();
            position.y = 0;
            
            CreateSmokeAtPosition(position);
        }
        
        /// <summary>
        /// Creates smoke at specific position
        /// </summary>
        private void CreateSmokeAtPosition(Vector3 position) {
            GameObject smoke = new GameObject("SmokeColumn");
            smoke.transform.position = position;
            
            ParticleSystem particles = smoke.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.startLifetime = 10f;
            main.startSpeed = 2f;
            main.startSize = 3f;
            main.startColor = _smokeColor;
            main.maxParticles = 500;
            
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 25f;
            shape.radius = 1f;
            
            var velocityOverLifetime = particles.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(3f);
            
            var emission = particles.emission;
            emission.rateOverTime = 50;
            
            _activeSmokeEffects.Add(smoke);
            
            // Auto-destroy after duration
            Destroy(smoke, _smokeDuration);
        }
        
        /// <summary>
        /// Creates a debris shower
        /// </summary>
        private void CreateDebrisShower() {
            Vector3 origin = GetRandomExplosionPosition();
            origin.y += 10f;
            
            for (int i = 0; i < _debrisCount * _intensity; i++) {
                CreateDebrisPiece(origin);
            }
            
            // Play debris sound
            if (_debrisSound != null) {
                AudioSource.PlayClipAtPoint(_debrisSound, origin, 0.7f);
            }
        }
        
        /// <summary>
        /// Creates a single debris piece
        /// </summary>
        private void CreateDebrisPiece(Vector3 origin) {
            GameObject debris = GameObject.CreatePrimitive(
                (PrimitiveType)Random.Range(0, 4) // Random primitive
            );
            debris.name = "Debris";
            debris.transform.position = origin + Random.insideUnitSphere * 2f;
            debris.transform.localScale = Vector3.one * Random.Range(0.1f, 0.5f);
            debris.transform.rotation = Random.rotation;
            
            // Add physics
            Rigidbody rb = debris.AddComponent<Rigidbody>();
            rb.mass = Random.Range(0.5f, 2f);
            Vector3 randomVelocity = Random.insideUnitSphere * _debrisVelocity;
            randomVelocity.y = Mathf.Abs(randomVelocity.y) * -1; // Fall down
            rb.velocity = randomVelocity;
            rb.angularVelocity = Random.insideUnitSphere * 10f;
            
            // Material
            Renderer renderer = debris.GetComponent<Renderer>();
            renderer.material.color = new Color(
                Random.Range(0.2f, 0.4f),
                Random.Range(0.2f, 0.4f),
                Random.Range(0.2f, 0.4f)
            );
            
            _activeDebris.Add(debris);
            
            // Auto-destroy
            Destroy(debris, _debrisLifetime);
        }
        
        /// <summary>
        /// Breaks nearby windows
        /// </summary>
        private void BreakNearbyWindows(Vector3 explosionPos) {
            // Find windows (objects with "Window" in name)
            GameObject[] windows = GameObject.FindObjectsOfType<GameObject>();
            
            foreach (var obj in windows) {
                if (obj.name.Contains("Window") && 
                    Vector3.Distance(obj.transform.position, explosionPos) < _explosionRadius) {
                    
                    // Play glass break sound
                    if (_glassBreakSound != null) {
                        AudioSource.PlayClipAtPoint(_glassBreakSound, obj.transform.position, 0.5f);
                    }
                    
                    // Create glass shards
                    for (int i = 0; i < 5; i++) {
                        GameObject shard = GameObject.CreatePrimitive(PrimitiveType.Quad);
                        shard.name = "GlassShard";
                        shard.transform.position = obj.transform.position;
                        shard.transform.localScale = Vector3.one * 0.2f;
                        
                        Rigidbody rb = shard.AddComponent<Rigidbody>();
                        rb.velocity = Random.insideUnitSphere * 5f;
                        
                        Destroy(shard, 3f);
                    }
                    
                    // Hide original window
                    obj.SetActive(false);
                }
            }
        }
        
        /// <summary>
        /// Flickers scene lights
        /// </summary>
        private IEnumerator FlickerLights() {
            while (_isActive && _flickerLights) {
                if (_sceneLights != null) {
                    foreach (var light in _sceneLights) {
                        if (light != null && Random.value < 0.1f * _intensity) {
                            StartCoroutine(FlickerLight(light));
                        }
                    }
                }
                
                yield return new WaitForSeconds(Random.Range(1f, 5f));
            }
        }
        
        /// <summary>
        /// Flickers a single light
        /// </summary>
        private IEnumerator FlickerLight(Light light) {
            float originalIntensity = light.intensity;
            
            for (int i = 0; i < Random.Range(3, 7); i++) {
                light.intensity = Random.Range(0f, originalIntensity);
                yield return new WaitForSeconds(Random.Range(0.05f, 0.2f));
            }
            
            light.intensity = originalIntensity;
        }
        
        /// <summary>
        /// Shakes the camera
        /// </summary>
        private IEnumerator ShakeCamera(float intensity, float duration) {
            Transform cameraTransform = Camera.main.transform;
            Vector3 originalPosition = cameraTransform.localPosition;
            
            float elapsed = 0f;
            while (elapsed < duration) {
                elapsed += Time.deltaTime;
                
                float shake = (1f - elapsed / duration) * intensity;
                cameraTransform.localPosition = originalPosition + Random.insideUnitSphere * shake;
                
                yield return null;
            }
            
            cameraTransform.localPosition = originalPosition;
        }
        
        /// <summary>
        /// Cleans up all active effects
        /// </summary>
        private void CleanupEffects() {
            // Clean smoke
            foreach (var smoke in _activeSmokeEffects) {
                if (smoke != null) Destroy(smoke);
            }
            _activeSmokeEffects.Clear();
            
            // Clean debris
            foreach (var debris in _activeDebris) {
                if (debris != null) Destroy(debris);
            }
            _activeDebris.Clear();
        }
        
        /// <summary>
        /// Restores lights to original state
        /// </summary>
        private void RestoreLights() {
            if (_sceneLights != null) {
                foreach (var light in _sceneLights) {
                    if (light != null) {
                        light.intensity = _originalLightIntensity;
                    }
                }
            }
        }
        
        /// <summary>
        /// Updates the stressor
        /// </summary>
        public void UpdateStressor() {
            // Update alarm volume
            if (_alarmSource != null && _alarmSource.isPlaying) {
                _alarmSource.volume = _alarmVolume * _intensity;
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