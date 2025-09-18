/*
 * Author: Tobias Sorg
 * Date: 2025-01-15
 * Description: ScriptableObject for scenario configuration in the DECIDE VR framework
 * License: GPLv3
 */

using UnityEngine;
using System.Collections.Generic;

namespace DECIDE.Core {
    /// <summary>
    /// ScriptableObject containing all scenario configuration parameters
    /// </summary>
    [CreateAssetMenu(fileName = "ScenarioConfiguration", menuName = "DECIDE/Scenario Configuration", order = 1)]
    public class ScenarioConfiguration : ScriptableObject {
        [Header("Basic Settings")]
        [SerializeField] private string _scenarioName = "Default Scenario";
        [SerializeField] private float _duration = 300f; // 5 minutes default
        [SerializeField] private string _description = "";
        
        [Header("Avatar Spawning")]
        [SerializeField, Range(0f, 1f)] private float _hostileSpawnRate = 0.4f;
        [SerializeField, Range(0f, 1f)] private float _friendlySpawnRate = 0.4f;
        [SerializeField, Range(0f, 1f)] private float _unknownSpawnRate = 0.2f;
        [SerializeField] private int _maxSimultaneousAvatars = 10;
        [SerializeField] private float _minSpawnInterval = 2f;
        [SerializeField] private float _maxSpawnInterval = 5f;
        
        [Header("Difficulty")]
        [SerializeField] private DifficultyLevel _difficultyLevel = DifficultyLevel.Medium;
        [SerializeField] private float _difficultyMultiplier = 1f;
        [SerializeField] private bool _adaptiveDifficulty = false;
        
        [Header("Environment")]
        [SerializeField] private EnvironmentType _environmentType = EnvironmentType.Urban;
        [SerializeField] private TimeOfDay _timeOfDay = TimeOfDay.Day;
        [SerializeField] private WeatherCondition _weatherCondition = WeatherCondition.Clear;
        
        [Header("Stressor Settings")]
        [SerializeField] private List<StressorPreset> _enabledStressors = new List<StressorPreset>();
        [SerializeField] private bool _randomizeStressorActivation = false;
        [SerializeField] private float _stressorIntensityMultiplier = 1f;
        
        [Header("Metric Settings")]
        [SerializeField] private List<MetricPreset> _enabledMetrics = new List<MetricPreset>();
        [SerializeField] private float _metricSamplingRate = 10f; // Hz
        
        [Header("Playground Bounds")]
        [SerializeField] private Vector3 _playgroundCenter = Vector3.zero;
        [SerializeField] private Vector3 _playgroundSize = new Vector3(50f, 10f, 50f);
        
        [Header("Avatar Behavior")]
        [SerializeField] private float _avatarMoveSpeed = 2f;
        [SerializeField] private float _avatarRunSpeed = 5f;
        [SerializeField] private bool _enableAvatarPathfinding = true;
        [SerializeField] private float _avatarDecisionChangeInterval = 10f;
        
        // Properties
        public string scenarioName => _scenarioName;
        public float duration => _duration;
        public string description => _description;
        public float hostileSpawnRate => _hostileSpawnRate;
        public float friendlySpawnRate => _friendlySpawnRate;
        public float unknownSpawnRate => _unknownSpawnRate;
        public int maxSimultaneousAvatars => _maxSimultaneousAvatars;
        public float minSpawnInterval => _minSpawnInterval;
        public float maxSpawnInterval => _maxSpawnInterval;
        public DifficultyLevel difficultyLevel => _difficultyLevel;
        public float difficultyMultiplier => _difficultyMultiplier;
        public bool adaptiveDifficulty => _adaptiveDifficulty;
        public EnvironmentType environmentType => _environmentType;
        public TimeOfDay timeOfDay => _timeOfDay;
        public WeatherCondition weatherCondition => _weatherCondition;
        public List<StressorPreset> enabledStressors => _enabledStressors;
        public bool randomizeStressorActivation => _randomizeStressorActivation;
        public float stressorIntensityMultiplier => _stressorIntensityMultiplier;
        public List<MetricPreset> enabledMetrics => _enabledMetrics;
        public float metricSamplingRate => _metricSamplingRate;
        public Vector3 playgroundCenter => _playgroundCenter;
        public Vector3 playgroundSize => _playgroundSize;
        public float avatarMoveSpeed => _avatarMoveSpeed;
        public float avatarRunSpeed => _avatarRunSpeed;
        public bool enableAvatarPathfinding => _enableAvatarPathfinding;
        public float avatarDecisionChangeInterval => _avatarDecisionChangeInterval;
        
        /// <summary>
        /// Validates the configuration
        /// </summary>
        public bool Validate() {
            // Ensure spawn rates sum to 1
            float totalSpawnRate = _hostileSpawnRate + _friendlySpawnRate + _unknownSpawnRate;
            if (Mathf.Abs(totalSpawnRate - 1f) > 0.01f) {
                Debug.LogWarning($"Spawn rates don't sum to 1.0 (current: {totalSpawnRate}). Normalizing...");
                NormalizeSpawnRates();
            }
            
            // Validate duration
            if (_duration <= 0) {
                Debug.LogError("Scenario duration must be positive!");
                return false;
            }
            
            // Validate spawn intervals
            if (_minSpawnInterval >= _maxSpawnInterval) {
                Debug.LogError("Min spawn interval must be less than max spawn interval!");
                return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Normalizes spawn rates to sum to 1
        /// </summary>
        private void NormalizeSpawnRates() {
            float total = _hostileSpawnRate + _friendlySpawnRate + _unknownSpawnRate;
            if (total > 0) {
                _hostileSpawnRate /= total;
                _friendlySpawnRate /= total;
                _unknownSpawnRate /= total;
            } else {
                _hostileSpawnRate = 0.33f;
                _friendlySpawnRate = 0.33f;
                _unknownSpawnRate = 0.34f;
            }
        }
        
        /// <summary>
        /// Sets default values
        /// </summary>
        public void SetDefaults() {
            _scenarioName = "Default Scenario";
            _duration = 300f;
            _hostileSpawnRate = 0.4f;
            _friendlySpawnRate = 0.4f;
            _unknownSpawnRate = 0.2f;
            _maxSimultaneousAvatars = 10;
            _minSpawnInterval = 2f;
            _maxSpawnInterval = 5f;
            _difficultyLevel = DifficultyLevel.Medium;
            _difficultyMultiplier = 1f;
            _adaptiveDifficulty = false;
            _environmentType = EnvironmentType.Urban;
            _timeOfDay = TimeOfDay.Day;
            _weatherCondition = WeatherCondition.Clear;
            _avatarMoveSpeed = 2f;
            _avatarRunSpeed = 5f;
            _enableAvatarPathfinding = true;
            _avatarDecisionChangeInterval = 10f;
            _playgroundCenter = Vector3.zero;
            _playgroundSize = new Vector3(50f, 10f, 50f);
            _stressorIntensityMultiplier = 1f;
            _metricSamplingRate = 10f;
            
            // Add default stressors
            _enabledStressors.Clear();
            _enabledStressors.Add(new StressorPreset {
                stressorType = StressorType.Fog,
                intensity = 0.3f,
                autoActivate = false
            });
            
            // Add default metrics
            _enabledMetrics.Clear();
            _enabledMetrics.Add(new MetricPreset {
                metricType = MetricType.Classification,
                enabled = true
            });
            _enabledMetrics.Add(new MetricPreset {
                metricType = MetricType.ReactionTime,
                enabled = true
            });
        }
        
        /// <summary>
        /// Creates a copy of this configuration
        /// </summary>
        public ScenarioConfiguration Clone() {
            ScenarioConfiguration clone = CreateInstance<ScenarioConfiguration>();
            clone._scenarioName = _scenarioName + " (Copy)";
            clone._duration = _duration;
            clone._description = _description;
            clone._hostileSpawnRate = _hostileSpawnRate;
            clone._friendlySpawnRate = _friendlySpawnRate;
            clone._unknownSpawnRate = _unknownSpawnRate;
            clone._maxSimultaneousAvatars = _maxSimultaneousAvatars;
            clone._minSpawnInterval = _minSpawnInterval;
            clone._maxSpawnInterval = _maxSpawnInterval;
            clone._difficultyLevel = _difficultyLevel;
            clone._difficultyMultiplier = _difficultyMultiplier;
            clone._adaptiveDifficulty = _adaptiveDifficulty;
            clone._environmentType = _environmentType;
            clone._timeOfDay = _timeOfDay;
            clone._weatherCondition = _weatherCondition;
            clone._enabledStressors = new List<StressorPreset>(_enabledStressors);
            clone._randomizeStressorActivation = _randomizeStressorActivation;
            clone._stressorIntensityMultiplier = _stressorIntensityMultiplier;
            clone._enabledMetrics = new List<MetricPreset>(_enabledMetrics);
            clone._metricSamplingRate = _metricSamplingRate;
            clone._playgroundCenter = _playgroundCenter;
            clone._playgroundSize = _playgroundSize;
            clone._avatarMoveSpeed = _avatarMoveSpeed;
            clone._avatarRunSpeed = _avatarRunSpeed;
            clone._enableAvatarPathfinding = _enableAvatarPathfinding;
            clone._avatarDecisionChangeInterval = _avatarDecisionChangeInterval;
            return clone;
        }
    }
    
    /// <summary>
    /// Difficulty levels for scenarios
    /// </summary>
    public enum DifficultyLevel {
        Easy,
        Medium,
        Hard,
        Expert
    }
    
    /// <summary>
    /// Environment types
    /// </summary>
    public enum EnvironmentType {
        Urban,
        Desert,
        Forest,
        Industrial,
        Residential
    }
    
    /// <summary>
    /// Time of day settings
    /// </summary>
    public enum TimeOfDay {
        Dawn,
        Day,
        Dusk,
        Night
    }
    
    /// <summary>
    /// Weather conditions
    /// </summary>
    public enum WeatherCondition {
        Clear,
        Fog,
        Rain,
        Storm,
        Sandstorm
    }
    
    /// <summary>
    /// Stressor preset configuration
    /// </summary>
    [System.Serializable]
    public class StressorPreset {
        public StressorType stressorType;
        public float intensity = 0.5f;
        public bool autoActivate = false;
        public float activationDelay = 0f;
        public float duration = -1f; // -1 for infinite
    }
    
    /// <summary>
    /// Metric preset configuration
    /// </summary>
    [System.Serializable]
    public class MetricPreset {
        public MetricType metricType;
        public bool enabled = true;
        public float samplingRate = 10f;
    }
}