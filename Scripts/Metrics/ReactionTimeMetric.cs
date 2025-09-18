/*
 * Author: Tobias Sorg
 * Date: 2025-01-15
 * Description: Tracks reaction time for avatar classifications in the DECIDE VR framework
 * License: GPLv3
 */

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DECIDE.Metrics;
using DECIDE.Events;

namespace DECIDE.Metrics.Implementations {
    /// <summary>
    /// Measures the time between avatar spawn and classification
    /// </summary>
    public class ReactionTimeMetric : MonoBehaviour, IMetric {
        [Header("Reaction Time Settings")]
        [SerializeField] private bool _trackPerAvatarType = true;
        [SerializeField] private float _outlierThreshold = 10f; // Seconds
        [SerializeField] private bool _excludeOutliers = false;
        [SerializeField] private int _movingAverageWindow = 10;
        
        // Interface implementation
        private string _name = "ReactionTime";
        private bool _isRecording = false;
        private MetricParameters _parameters;
        
        // Data storage
        private List<ReactionTimeData> _reactionTimes;
        private Dictionary<int, float> _avatarSpawnTimes;
        private Queue<float> _recentReactionTimes;
        private float _totalReactionTime = 0f;
        private int _measurementCount = 0;
        
        // Statistics
        private float _minReactionTime = float.MaxValue;
        private float _maxReactionTime = 0f;
        private float _averageReactionTime = 0f;
        private float _movingAverage = 0f;
        
        // IMetric properties
        public string Name => _name;
        public bool IsRecording => _isRecording;
        
        private void Awake() {
            _reactionTimes = new List<ReactionTimeData>();
            _avatarSpawnTimes = new Dictionary<int, float>();
            _recentReactionTimes = new Queue<float>(_movingAverageWindow);
        }
        
        private void OnEnable() {
            // Subscribe to events
            ScenarioEvents.OnAvatarSpawned += HandleAvatarSpawned;
            ScenarioEvents.OnAvatarClassified += HandleAvatarClassified;
            ScenarioEvents.OnAvatarDespawned += HandleAvatarDespawned;
        }
        
        private void OnDisable() {
            // Unsubscribe from events
            ScenarioEvents.OnAvatarSpawned -= HandleAvatarSpawned;
            ScenarioEvents.OnAvatarClassified -= HandleAvatarClassified;
            ScenarioEvents.OnAvatarDespawned -= HandleAvatarDespawned;
        }
        
        /// <summary>
        /// Initializes the metric
        /// </summary>
        public void Initialize(MetricParameters parameters) {
            _parameters = parameters ?? new MetricParameters();
            
            if (_parameters.autoStart) {
                StartRecording();
            }
        }
        
        /// <summary>
        /// Starts recording reaction time data
        /// </summary>
        public void StartRecording() {
            _isRecording = true;
            _avatarSpawnTimes.Clear();
        }
        
        /// <summary>
        /// Stops recording reaction time data
        /// </summary>
        public void StopRecording() {
            _isRecording = false;
        }
        
        /// <summary>
        /// Resets all collected data
        /// </summary>
        public void Reset() {
            _reactionTimes.Clear();
            _avatarSpawnTimes.Clear();
            _recentReactionTimes.Clear();
            _totalReactionTime = 0f;
            _measurementCount = 0;
            _minReactionTime = float.MaxValue;
            _maxReactionTime = 0f;
            _averageReactionTime = 0f;
            _movingAverage = 0f;
        }
        
        /// <summary>
        /// Records a data point
        /// </summary>
        public void RecordDataPoint(object data) {
            if (!_isRecording) return;
            
            if (data is float reactionTime) {
                RecordReactionTime(reactionTime, "Unknown");
            }
        }
        
        /// <summary>
        /// Records a reaction time measurement
        /// </summary>
        private void RecordReactionTime(float reactionTime, string avatarType, int avatarId = -1) {
            // Check for outliers
            if (_excludeOutliers && reactionTime > _outlierThreshold) {
                return;
            }
            
            var dataPoint = new ReactionTimeData {
                timestamp = System.DateTime.Now,
                avatarId = avatarId,
                avatarType = avatarType,
                reactionTime = reactionTime,
                isOutlier = reactionTime > _outlierThreshold
            };
            
            _reactionTimes.Add(dataPoint);
            
            // Update statistics
            _totalReactionTime += reactionTime;
            _measurementCount++;
            _averageReactionTime = _totalReactionTime / _measurementCount;
            
            if (reactionTime < _minReactionTime) {
                _minReactionTime = reactionTime;
            }
            if (reactionTime > _maxReactionTime) {
                _maxReactionTime = reactionTime;
            }
            
            // Update moving average
            _recentReactionTimes.Enqueue(reactionTime);
            if (_recentReactionTimes.Count > _movingAverageWindow) {
                _recentReactionTimes.Dequeue();
            }
            _movingAverage = _recentReactionTimes.Average();
        }
        
        /// <summary>
        /// Updates the metric (called each frame when recording)
        /// </summary>
        public void UpdateMetric() {
            // Check for timed-out avatars
            if (!_isRecording) return;
            
            float currentTime = Time.time;
            List<int> timedOutAvatars = new List<int>();
            
            foreach (var kvp in _avatarSpawnTimes) {
                if (currentTime - kvp.Value > _outlierThreshold) {
                    timedOutAvatars.Add(kvp.Key);
                }
            }
            
            // Remove timed out avatars
            foreach (int avatarId in timedOutAvatars) {
                _avatarSpawnTimes.Remove(avatarId);
            }
        }
        
        /// <summary>
        /// Gets the current metric data
        /// </summary>
        public Dictionary<string, object> GetData() {
            var data = new Dictionary<string, object> {
                ["totalMeasurements"] = _measurementCount,
                ["averageReactionTime"] = _averageReactionTime,
                ["minReactionTime"] = _minReactionTime == float.MaxValue ? 0f : _minReactionTime,
                ["maxReactionTime"] = _maxReactionTime,
                ["movingAverage"] = _movingAverage,
                ["currentTrackedAvatars"] = _avatarSpawnTimes.Count
            };
            
            if (_trackPerAvatarType) {
                data["reactionTimeByType"] = GetReactionTimesByType();
            }
            
            // Add percentiles
            if (_reactionTimes.Count > 0) {
                var sortedTimes = _reactionTimes
                    .Where(r => !r.isOutlier)
                    .Select(r => r.reactionTime)
                    .OrderBy(t => t)
                    .ToList();
                
                if (sortedTimes.Count > 0) {
                    data["percentile50"] = GetPercentile(sortedTimes, 0.5f);
                    data["percentile90"] = GetPercentile(sortedTimes, 0.9f);
                    data["percentile95"] = GetPercentile(sortedTimes, 0.95f);
                }
            }
            
            return data;
        }
        
        /// <summary>
        /// Gets reaction times grouped by avatar type
        /// </summary>
        private Dictionary<string, object> GetReactionTimesByType() {
            var result = new Dictionary<string, object>();
            
            var groupedData = _reactionTimes.GroupBy(r => r.avatarType);
            
            foreach (var group in groupedData) {
                var times = group.Select(r => r.reactionTime).ToList();
                if (times.Count > 0) {
                    result[group.Key] = new {
                        average = times.Average(),
                        min = times.Min(),
                        max = times.Max(),
                        count = times.Count
                    };
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Calculates percentile value
        /// </summary>
        private float GetPercentile(List<float> sortedValues, float percentile) {
            if (sortedValues.Count == 0) return 0f;
            
            int index = Mathf.FloorToInt(sortedValues.Count * percentile);
            index = Mathf.Clamp(index, 0, sortedValues.Count - 1);
            
            return sortedValues[index];
        }
        
        /// <summary>
        /// Analyzes and returns metric results
        /// </summary>
        public MetricAnalysisResult Analyze() {
            var result = new MetricAnalysisResult {
                metricName = _name,
                sampleCount = _measurementCount
            };
            
            if (_reactionTimes.Count > 0) {
                var validTimes = _reactionTimes
                    .Where(r => !r.isOutlier)
                    .Select(r => r.reactionTime)
                    .ToList();
                
                if (validTimes.Count > 0) {
                    result.mean = validTimes.Average();
                    result.median = CalculateMedian(validTimes);
                    result.standardDeviation = CalculateStandardDeviation(validTimes);
                    result.min = validTimes.Min();
                    result.max = validTimes.Max();
                    
                    // Additional analysis
                    result.additionalData["outlierCount"] = _reactionTimes.Count(r => r.isOutlier);
                    result.additionalData["improvementTrend"] = AnalyzeImprovementTrend();
                    result.additionalData["consistency"] = CalculateConsistency(validTimes);
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Analyzes if reaction times are improving over time
        /// </summary>
        private float AnalyzeImprovementTrend() {
            if (_reactionTimes.Count < 10) return 0f;
            
            // Compare first third with last third
            int thirdSize = _reactionTimes.Count / 3;
            
            float firstThirdAvg = _reactionTimes
                .Take(thirdSize)
                .Average(r => r.reactionTime);
            
            float lastThirdAvg = _reactionTimes
                .Skip(_reactionTimes.Count - thirdSize)
                .Average(r => r.reactionTime);
            
            // Negative value means improvement (faster reaction times)
            return (lastThirdAvg - firstThirdAvg) / firstThirdAvg;
        }
        
        /// <summary>
        /// Calculates consistency score (lower is more consistent)
        /// </summary>
        private float CalculateConsistency(List<float> times) {
            if (times.Count < 2) return 0f;
            
            float mean = times.Average();
            float variance = times.Sum(t => Mathf.Pow(t - mean, 2)) / (times.Count - 1);
            float coefficientOfVariation = Mathf.Sqrt(variance) / mean;
            
            return coefficientOfVariation;
        }
        
        /// <summary>
        /// Calculates median value
        /// </summary>
        private float CalculateMedian(List<float> values) {
            if (values.Count == 0) return 0f;
            
            var sorted = values.OrderBy(v => v).ToList();
            int middle = sorted.Count / 2;
            
            if (sorted.Count % 2 == 0) {
                return (sorted[middle - 1] + sorted[middle]) / 2f;
            }
            
            return sorted[middle];
        }
        
        /// <summary>
        /// Calculates standard deviation
        /// </summary>
        private float CalculateStandardDeviation(List<float> values) {
            if (values.Count <= 1) return 0f;
            
            float mean = values.Average();
            float sumSquares = values.Sum(v => Mathf.Pow(v - mean, 2));
            return Mathf.Sqrt(sumSquares / (values.Count - 1));
        }
        
        /// <summary>
        /// Updates metric parameters at runtime
        /// </summary>
        public void UpdateParameters(MetricParameters parameters) {
            _parameters = parameters;
        }
        
        // Event handlers
        private void HandleAvatarSpawned(AvatarSpawnedEventData data) {
            if (!_isRecording) return;
            
            _avatarSpawnTimes[data.avatarId] = Time.time;
        }
        
        private void HandleAvatarClassified(AvatarClassifiedEventData data) {
            if (!_isRecording) return;
            
            if (_avatarSpawnTimes.TryGetValue(data.avatarId, out float spawnTime)) {
                float reactionTime = Time.time - spawnTime;
                RecordReactionTime(reactionTime, data.actualType, data.avatarId);
                _avatarSpawnTimes.Remove(data.avatarId);
            }
        }
        
        private void HandleAvatarDespawned(AvatarDespawnedEventData data) {
            if (!_isRecording) return;
            
            // Remove from tracking if avatar despawned without classification
            _avatarSpawnTimes.Remove(data.avatarId);
        }
        
        /// <summary>
        /// Reaction time data structure
        /// </summary>
        [System.Serializable]
        private class ReactionTimeData {
            public System.DateTime timestamp;
            public int avatarId;
            public string avatarType;
            public float reactionTime;
            public bool isOutlier;
        }
    }
}