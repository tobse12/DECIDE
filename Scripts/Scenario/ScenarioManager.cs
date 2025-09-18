/*
 * Author: Tobias Sorg
 * Date: 2025-01-15
 * Description: Main scenario manager for the DECIDE VR framework
 * License: GPLv3
 */

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DECIDE.Events;
using DECIDE.Avatars;
using DECIDE.UI;

namespace DECIDE.Core {
    /// <summary>
    /// Manages scenario execution, avatar spawning, and overall game flow
    /// </summary>
    public class ScenarioManager : MonoBehaviour {
        [Header("Scenario Configuration")]
        [SerializeField] private ScenarioConfiguration _configuration;
        [SerializeField] private Transform _playerTransform;
        [SerializeField] private GameObject _playgroundBounds;
        
        [Header("Avatar Spawning")]
        [SerializeField] private AvatarPool _avatarPool;
        [SerializeField] private Transform[] _spawnPoints;
        [SerializeField] private float _minSpawnInterval = 2f;
        [SerializeField] private float _maxSpawnInterval = 5f;
        
        [Header("UI References")]
        [SerializeField] private HUDController _hudController;
        
        // State management
        private ScenarioState _currentState = ScenarioState.Idle;
        private float _scenarioTimer;
        private float _nextSpawnTime;
        private int _totalClassifications;
        private int _correctClassifications;
        private int _missedClassifications;
        private List<ActiveAvatar> _activeAvatars;
        private Coroutine _scenarioCoroutine;
        
        // Properties
        public ScenarioState CurrentState => _currentState;
        public float RemainingTime => Mathf.Max(0, _configuration.duration - _scenarioTimer);
        public float ElapsedTime => _scenarioTimer;
        public ScenarioConfiguration Configuration => _configuration;
        
        // Singleton pattern
        private static ScenarioManager _instance;
        public static ScenarioManager Instance {
            get {
                if (_instance == null) {
                    _instance = FindObjectOfType<ScenarioManager>();
                }
                return _instance;
            }
        }
        
        private void Awake() {
            if (_instance != null && _instance != this) {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            
            _activeAvatars = new List<ActiveAvatar>();
            LoadConfiguration();
        }
        
        private void OnEnable() {
            // Subscribe to events
            ScenarioEvents.OnAvatarClassified += HandleAvatarClassified;
        }
        
        private void OnDisable() {
            // Unsubscribe from events
            ScenarioEvents.OnAvatarClassified -= HandleAvatarClassified;
        }
        
        /// <summary>
        /// Starts the scenario
        /// </summary>
        public void StartScenario() {
            if (_currentState != ScenarioState.Idle && _currentState != ScenarioState.Paused) {
                Debug.LogWarning("Cannot start scenario in current state: " + _currentState);
                return;
            }
            
            if (_currentState == ScenarioState.Idle) {
                ResetScenario();
            }
            
            _currentState = ScenarioState.Running;
            _scenarioCoroutine = StartCoroutine(RunScenario());
            
            ScenarioEvents.TriggerScenarioStarted(new ScenarioStartedEventData {
                scenarioName = _configuration.scenarioName,
                duration = _configuration.duration,
                startTime = DateTime.Now
            });
        }
        
        /// <summary>
        /// Pauses the scenario
        /// </summary>
        public void PauseScenario() {
            if (_currentState != ScenarioState.Running) {
                return;
            }
            
            _currentState = ScenarioState.Paused;
            Time.timeScale = 0f;
            
            ScenarioEvents.TriggerScenarioPaused(new ScenarioPausedEventData {
                timeAtPause = _scenarioTimer,
                pauseTime = DateTime.Now
            });
        }
        
        /// <summary>
        /// Resumes the scenario
        /// </summary>
        public void ResumeScenario() {
            if (_currentState != ScenarioState.Paused) {
                return;
            }
            
            _currentState = ScenarioState.Running;
            Time.timeScale = 1f;
            
            ScenarioEvents.TriggerScenarioResumed(new ScenarioResumedEventData {
                pauseDuration = 0f, // Calculate based on pause time
                resumeTime = DateTime.Now
            });
        }
        
        /// <summary>
        /// Stops the scenario
        /// </summary>
        public void StopScenario() {
            if (_currentState == ScenarioState.Idle) {
                return;
            }
            
            if (_scenarioCoroutine != null) {
                StopCoroutine(_scenarioCoroutine);
            }
            
            _currentState = ScenarioState.Completed;
            Time.timeScale = 1f;
            
            // Despawn all avatars
            DespawnAllAvatars();
            
            ScenarioEvents.TriggerScenarioEnded(new ScenarioEndedEventData {
                scenarioName = _configuration.scenarioName,
                elapsedTime = _scenarioTimer,
                endTime = DateTime.Now,
                totalClassifications = _totalClassifications,
                correctClassifications = _correctClassifications
            });
            
            _currentState = ScenarioState.Idle;
        }
        
        /// <summary>
        /// Main scenario execution coroutine
        /// </summary>
        private IEnumerator RunScenario() {
            _scenarioTimer = 0f;
            _nextSpawnTime = Time.time + UnityEngine.Random.Range(_minSpawnInterval, _maxSpawnInterval);
            
            while (_scenarioTimer < _configuration.duration && _currentState == ScenarioState.Running) {
                // Update timer
                _scenarioTimer += Time.deltaTime;
                
                // Update HUD
                if (_hudController != null) {
                    _hudController.UpdateTimer(RemainingTime);
                }
                
                // Handle avatar spawning
                if (Time.time >= _nextSpawnTime) {
                    SpawnAvatar();
                    _nextSpawnTime = Time.time + UnityEngine.Random.Range(_minSpawnInterval, _maxSpawnInterval);
                }
                
                // Check avatar boundaries
                CheckAvatarBoundaries();
                
                yield return null;
            }
            
            // Scenario completed
            StopScenario();
        }
        
        /// <summary>
        /// Spawns a new avatar
        /// </summary>
        private void SpawnAvatar() {
            if (_spawnPoints.Length == 0 || _avatarPool == null) {
                return;
            }
            
            // Select random spawn point
            Transform spawnPoint = _spawnPoints[UnityEngine.Random.Range(0, _spawnPoints.Length)];
            
            // Determine avatar type based on configuration
            AvatarType type = DetermineAvatarType();
            
            // Get avatar from pool
            GameObject avatar = _avatarPool.GetAvatar(type);
            if (avatar == null) {
                return;
            }
            
            avatar.transform.position = spawnPoint.position;
            avatar.transform.rotation = spawnPoint.rotation;
            
            // Register active avatar
            ActiveAvatar activeAvatar = new ActiveAvatar {
                gameObject = avatar,
                avatarId = avatar.GetInstanceID(),
                type = type,
                spawnTime = Time.time
            };
            _activeAvatars.Add(activeAvatar);
            
            // Trigger event
            ScenarioEvents.TriggerAvatarSpawned(new AvatarSpawnedEventData {
                avatar = avatar,
                avatarType = type.ToString(),
                spawnPosition = spawnPoint.position,
                avatarId = activeAvatar.avatarId
            });
        }
        
        /// <summary>
        /// Determines the type of avatar to spawn based on configuration
        /// </summary>
        private AvatarType DetermineAvatarType() {
            float random = UnityEngine.Random.Range(0f, 1f);
            
            if (random < _configuration.hostileSpawnRate) {
                return AvatarType.Hostile;
            } else if (random < _configuration.hostileSpawnRate + _configuration.friendlySpawnRate) {
                return AvatarType.Friendly;
            } else {
                return AvatarType.Unknown;
            }
        }
        
        /// <summary>
        /// Checks if avatars are within playground boundaries
        /// </summary>
        private void CheckAvatarBoundaries() {
            if (_playgroundBounds == null) {
                return;
            }
            
            Collider boundsCollider = _playgroundBounds.GetComponent<Collider>();
            if (boundsCollider == null) {
                return;
            }
            
            for (int i = _activeAvatars.Count - 1; i >= 0; i--) {
                ActiveAvatar avatar = _activeAvatars[i];
                if (!boundsCollider.bounds.Contains(avatar.gameObject.transform.position)) {
                    DespawnAvatar(avatar, "left_playground");
                }
            }
        }
        
        /// <summary>
        /// Despawns a specific avatar
        /// </summary>
        public void DespawnAvatar(ActiveAvatar avatar, string reason) {
            ScenarioEvents.TriggerAvatarDespawned(new AvatarDespawnedEventData {
                avatarId = avatar.avatarId,
                reason = reason
            });
            
            _activeAvatars.Remove(avatar);
            _avatarPool.ReturnAvatar(avatar.gameObject);
        }
        
        /// <summary>
        /// Despawns all active avatars
        /// </summary>
        private void DespawnAllAvatars() {
            foreach (var avatar in _activeAvatars) {
                _avatarPool.ReturnAvatar(avatar.gameObject);
            }
            _activeAvatars.Clear();
        }
        
        /// <summary>
        /// Handles avatar classification events
        /// </summary>
        private void HandleAvatarClassified(AvatarClassifiedEventData data) {
            _totalClassifications++;
            if (data.isCorrect) {
                _correctClassifications++;
            }
            
            // Find and despawn the classified avatar
            ActiveAvatar avatar = _activeAvatars.Find(a => a.avatarId == data.avatarId);
            if (avatar != null) {
                DespawnAvatar(avatar, "classified");
            }
        }
        
        /// <summary>
        /// Resets the scenario to initial state
        /// </summary>
        private void ResetScenario() {
            _scenarioTimer = 0f;
            _totalClassifications = 0;
            _correctClassifications = 0;
            _missedClassifications = 0;
            DespawnAllAvatars();
        }
        
        /// <summary>
        /// Loads scenario configuration
        /// </summary>
        private void LoadConfiguration() {
            if (_configuration == null) {
                _configuration = ScriptableObject.CreateInstance<ScenarioConfiguration>();
                _configuration.SetDefaults();
            }
        }
        
        /// <summary>
        /// Updates scenario configuration at runtime
        /// </summary>
        public void UpdateConfiguration(ScenarioConfiguration newConfig) {
            var oldConfig = _configuration;
            _configuration = newConfig;
            
            ScenarioEvents.TriggerScenarioConfigurationChanged(new ScenarioConfigurationChangedEventData {
                parameterName = "ScenarioConfiguration",
                oldValue = oldConfig,
                newValue = newConfig
            });
        }
        
        /// <summary>
        /// Gets active avatar by ID
        /// </summary>
        public ActiveAvatar GetActiveAvatar(int avatarId) {
            return _activeAvatars.Find(a => a.avatarId == avatarId);
        }
    }
    
    /// <summary>
    /// Scenario execution states
    /// </summary>
    public enum ScenarioState {
        Idle,
        Running,
        Paused,
        Completed
    }
    
    /// <summary>
    /// Active avatar data container
    /// </summary>
    [System.Serializable]
    public class ActiveAvatar {
        public GameObject gameObject;
        public int avatarId;
        public AvatarType type;
        public float spawnTime;
    }
}