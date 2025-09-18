/*
 * Author: Tobias Sorg
 * Date: 2025-01-15
 * Description: Event system for the DECIDE VR framework scenario management
 * License: GPLv3
 */

using System;
using UnityEngine;

namespace DECIDE.Events {
    /// <summary>
    /// Central event system for scenario-related events
    /// </summary>
    public static class ScenarioEvents {
        // Core scenario events
        public static event Action<ScenarioStartedEventData> OnScenarioStarted;
        public static event Action<ScenarioEndedEventData> OnScenarioEnded;
        public static event Action<ScenarioPausedEventData> OnScenarioPaused;
        public static event Action<ScenarioResumedEventData> OnScenarioResumed;
        
        // Avatar-related events
        public static event Action<AvatarSpawnedEventData> OnAvatarSpawned;
        public static event Action<AvatarDespawnedEventData> OnAvatarDespawned;
        public static event Action<AvatarClassifiedEventData> OnAvatarClassified;
        public static event Action<AvatarTargetedEventData> OnAvatarTargeted;
        
        // Stressor events
        public static event Action<StressorActivatedEventData> OnStressorActivated;
        public static event Action<StressorDeactivatedEventData> OnStressorDeactivated;
        
        // Metric events
        public static event Action<MetricRecordedEventData> OnMetricRecorded;
        
        // Scenario state events
        public static event Action<ScenarioConfigurationChangedEventData> OnScenarioConfigurationChanged;
        
        // Trigger methods
        public static void TriggerScenarioStarted(ScenarioStartedEventData data) {
            OnScenarioStarted?.Invoke(data);
        }
        
        public static void TriggerScenarioEnded(ScenarioEndedEventData data) {
            OnScenarioEnded?.Invoke(data);
        }
        
        public static void TriggerScenarioPaused(ScenarioPausedEventData data) {
            OnScenarioPaused?.Invoke(data);
        }
        
        public static void TriggerScenarioResumed(ScenarioResumedEventData data) {
            OnScenarioResumed?.Invoke(data);
        }
        
        public static void TriggerAvatarSpawned(AvatarSpawnedEventData data) {
            OnAvatarSpawned?.Invoke(data);
        }
        
        public static void TriggerAvatarDespawned(AvatarDespawnedEventData data) {
            OnAvatarDespawned?.Invoke(data);
        }
        
        public static void TriggerAvatarClassified(AvatarClassifiedEventData data) {
            OnAvatarClassified?.Invoke(data);
        }
        
        public static void TriggerAvatarTargeted(AvatarTargetedEventData data) {
            OnAvatarTargeted?.Invoke(data);
        }
        
        public static void TriggerStressorActivated(StressorActivatedEventData data) {
            OnStressorActivated?.Invoke(data);
        }
        
        public static void TriggerStressorDeactivated(StressorDeactivatedEventData data) {
            OnStressorDeactivated?.Invoke(data);
        }
        
        public static void TriggerMetricRecorded(MetricRecordedEventData data) {
            OnMetricRecorded?.Invoke(data);
        }
        
        public static void TriggerScenarioConfigurationChanged(ScenarioConfigurationChangedEventData data) {
            OnScenarioConfigurationChanged?.Invoke(data);
        }
        
        /// <summary>
        /// Clears all event subscriptions
        /// </summary>
        public static void ClearAllSubscriptions() {
            OnScenarioStarted = null;
            OnScenarioEnded = null;
            OnScenarioPaused = null;
            OnScenarioResumed = null;
            OnAvatarSpawned = null;
            OnAvatarDespawned = null;
            OnAvatarClassified = null;
            OnAvatarTargeted = null;
            OnStressorActivated = null;
            OnStressorDeactivated = null;
            OnMetricRecorded = null;
            OnScenarioConfigurationChanged = null;
        }
    }
    
    // Event data classes
    [System.Serializable]
    public class ScenarioStartedEventData {
        public string scenarioName;
        public float duration;
        public DateTime startTime;
    }
    
    [System.Serializable]
    public class ScenarioEndedEventData {
        public string scenarioName;
        public float elapsedTime;
        public DateTime endTime;
        public int totalClassifications;
        public int correctClassifications;
    }
    
    [System.Serializable]
    public class ScenarioPausedEventData {
        public float timeAtPause;
        public DateTime pauseTime;
    }
    
    [System.Serializable]
    public class ScenarioResumedEventData {
        public float pauseDuration;
        public DateTime resumeTime;
    }
    
    [System.Serializable]
    public class AvatarSpawnedEventData {
        public GameObject avatar;
        public string avatarType; // Hostile, Friendly, Unknown
        public Vector3 spawnPosition;
        public int avatarId;
    }
    
    [System.Serializable]
    public class AvatarDespawnedEventData {
        public int avatarId;
        public string reason; // "left_playground", "classified", "timeout"
    }
    
    [System.Serializable]
    public class AvatarClassifiedEventData {
        public int avatarId;
        public string actualType;
        public string classifiedAs;
        public bool isCorrect;
        public float reactionTime;
        public Vector3 avatarPosition;
        public Vector3 playerPosition;
    }
    
    [System.Serializable]
    public class AvatarTargetedEventData {
        public int avatarId;
        public string avatarType;
        public float targetStartTime;
    }
    
    [System.Serializable]
    public class StressorActivatedEventData {
        public string stressorName;
        public float intensity;
    }
    
    [System.Serializable]
    public class StressorDeactivatedEventData {
        public string stressorName;
    }
    
    [System.Serializable]
    public class MetricRecordedEventData {
        public string metricName;
        public object value;
        public DateTime timestamp;
    }
    
    [System.Serializable]
    public class ScenarioConfigurationChangedEventData {
        public string parameterName;
        public object oldValue;
        public object newValue;
    }
}