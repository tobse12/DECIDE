/*
 * Author: Tobias Sorg
 * Date: 2025-01-15
 * Description: Tracks classification performance metrics in the DECIDE VR framework
 * License: GPLv3
 */

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DECIDE.Metrics;
using DECIDE.Events;

namespace DECIDE.Metrics.Implementations {
    /// <summary>
    /// Tracks classification accuracy, errors, and related metrics
    /// </summary>
    public class ClassificationMetric : MonoBehaviour, IMetric {
        [Header("Classification Settings")]
        [SerializeField] private bool _trackConfusionMatrix = true;
        [SerializeField] private bool _trackSpatialData = true;
        [SerializeField] private float _missedClassificationTimeout = 10f;
        
        // Interface implementation
        private string _name = "Classification";
        private bool _isRecording = false;
        private MetricParameters _parameters;
        
        // Data storage
        private List<ClassificationData> _classifications;
        private Dictionary<string, Dictionary<string, int>> _confusionMatrix;
        private int _totalCorrect = 0;
        private int _totalIncorrect = 0;
        private int _totalMissed = 0;
        private int _consecutiveErrors = 0;
        private int _maxConsecutiveErrors = 0;
        private float _lastClassificationTime = 0f;
        
        // IMetric properties
        public string Name => _name;
        public bool IsRecording => _isRecording;
        
        private void Awake() {
            _classifications = new List<ClassificationData>();
            InitializeConfusionMatrix();
        }
        
        private void OnEnable() {
            // Subscribe to events
            ScenarioEvents.OnAvatarClassified += HandleAvatarClassified;
            ScenarioEvents.OnAvatarDespawned += HandleAvatarDespawned;
        }
        
        private void OnDisable() {
            // Unsubscribe from events
            ScenarioEvents.OnAvatarClassified -= HandleAvatarClassified;
            ScenarioEvents.OnAvatarDespawned -= HandleAvatarDespawned;
        }
        
        /// <summary>
        /// Initializes the confusion matrix
        /// </summary>
        private void InitializeConfusionMatrix() {
            _confusionMatrix = new Dictionary<string, Dictionary<string, int>>();
            string[] types = { "Hostile", "Friendly", "Unknown" };
            
            foreach (string actual in types) {
                _confusionMatrix[actual] = new Dictionary<string, int>();
                foreach (string predicted in new[] { "Hostile", "NonHostile" }) {
                    _confusionMatrix[actual][predicted] = 0;
                }
            }
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
        /// Starts recording metric data
        /// </summary>
        public void StartRecording() {
            _isRecording = true;
            _lastClassificationTime = Time.time;
        }
        
        /// <summary>
        /// Stops recording metric data
        /// </summary>
        public void StopRecording() {
            _isRecording = false;
        }
        
        /// <summary>
        /// Resets all metric data
        /// </summary>
        public void Reset() {
            _classifications.Clear();
            InitializeConfusionMatrix();
            _totalCorrect = 0;
            _totalIncorrect = 0;
            _totalMissed = 0;
            _consecutiveErrors = 0;
            _maxConsecutiveErrors = 0;
            _lastClassificationTime = Time.time;
        }
        
        /// <summary>
        /// Records a classification data point
        /// </summary>
        public void RecordDataPoint(object data) {
            if (!_isRecording) return;
            
            if (data is AvatarClassifiedEventData classData) {
                RecordClassification(classData);
            }
        }
        
        /// <summary>
        /// Records a classification
        /// </summary>
        private void RecordClassification(AvatarClassifiedEventData data) {
            var classification = new ClassificationData {
                timestamp = System.DateTime.Now,
                avatarId = data.avatarId,
                actualType = data.actualType,
                classifiedAs = data.classifiedAs,
                isCorrect = data.isCorrect,
                reactionTime = data.reactionTime,
                timeSinceLastClassification = Time.time - _lastClassificationTime,
                avatarPosition = data.avatarPosition,
                playerPosition = data.playerPosition,
                distance = Vector3.Distance(data.avatarPosition, data.playerPosition)
            };
            
            _classifications.Add(classification);
            
            // Update statistics
            if (data.isCorrect) {
                _totalCorrect++;
                _consecutiveErrors = 0;
            } else {
                _totalIncorrect++;
                _consecutiveErrors++;
                _maxConsecutiveErrors = Mathf.Max(_maxConsecutiveErrors, _consecutiveErrors);
            }
            
            // Update confusion matrix
            if (_trackConfusionMatrix) {
                string predicted = data.classifiedAs == "Hostile" ? "Hostile" : "NonHostile";
                _confusionMatrix[data.actualType][predicted]++;
            }
            
            _lastClassificationTime = Time.time;
        }
        
        /// <summary>
        /// Handles avatar despawn events
        /// </summary>
        private void HandleAvatarDespawned(AvatarDespawnedEventData data) {
            if (!_isRecording) return;
            
            // Check if avatar was despawned without being classified
            if (data.reason == "left_playground" || data.reason == "timeout") {
                _totalMissed++;
            }
        }
        
        /// <summary>
        /// Handles avatar classification events
        /// </summary>
        private void HandleAvatarClassified(AvatarClassifiedEventData data) {
            RecordDataPoint(data);
        }
        
        /// <summary>
        /// Updates the metric (called each frame when recording)
        /// </summary>
        public void UpdateMetric() {
            // Check for missed classifications based on timeout
            // This could be expanded to track specific avatars
        }
        
        /// <summary>
        /// Gets the current metric data
        /// </summary>
        public Dictionary<string, object> GetData() {
            var data = new Dictionary<string, object> {
                ["totalClassifications"] = _classifications.Count,
                ["totalCorrect"] = _totalCorrect,
                ["totalIncorrect"] = _totalIncorrect,
                ["totalMissed"] = _totalMissed,
                ["accuracy"] = _classifications.Count > 0 ? (float)_totalCorrect / _classifications.Count : 0f,
                ["consecutiveErrors"] = _consecutiveErrors,
                ["maxConsecutiveErrors"] = _maxConsecutiveErrors
            };
            
            if (_trackConfusionMatrix) {
                data["confusionMatrix"] = _confusionMatrix;
            }
            
            if (_classifications.Count > 0) {
                data["averageReactionTime"] = _classifications.Average(c => c.reactionTime);
                data["averageDistance"] = _classifications.Average(c => c.distance);
            }
            
            return data;
        }
        
        /// <summary>
        /// Analyzes and returns metric results
        /// </summary>
        public MetricAnalysisResult Analyze() {
            var result = new MetricAnalysisResult {
                metricName = _name,
                sampleCount = _classifications.Count
            };
            
            if (_classifications.Count > 0) {
                var reactionTimes = _classifications.Select(c => c.reactionTime).ToList();
                result.mean = reactionTimes.Average();
                result.median = CalculateMedian(reactionTimes);
                result.standardDeviation = CalculateStandardDeviation(reactionTimes);
                result.min = reactionTimes.Min();
                result.max = reactionTimes.Max();
                
                // Additional analysis
                result.additionalData["accuracy"] = (float)_totalCorrect / _classifications.Count;
                result.additionalData["precision"] = CalculatePrecision();
                result.additionalData["recall"] = CalculateRecall();
                result.additionalData["f1Score"] = CalculateF1Score();
                
                // Type-specific accuracy
                result.additionalData["hostileAccuracy"] = CalculateTypeAccuracy("Hostile");
                result.additionalData["friendlyAccuracy"] = CalculateTypeAccuracy("Friendly");
                result.additionalData["unknownAccuracy"] = CalculateTypeAccuracy("Unknown");
                
                // Spatial analysis
                if (_trackSpatialData) {
                    result.additionalData["averageClassificationDistance"] = _classifications.Average(c => c.distance);
                    result.additionalData["classificationsByDistance"] = AnalyzeByDistance();
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Calculates precision (true positives / (true positives + false positives))
        /// </summary>
        private float CalculatePrecision() {
            if (!_trackConfusionMatrix) return 0f;
            
            int truePositives = _confusionMatrix["Hostile"]["Hostile"];
            int falsePositives = _confusionMatrix["Friendly"]["Hostile"] + _confusionMatrix["Unknown"]["Hostile"];
            
            if (truePositives + falsePositives == 0) return 0f;
            return (float)truePositives / (truePositives + falsePositives);
        }
        
        /// <summary>
        /// Calculates recall (true positives / (true positives + false negatives))
        /// </summary>
        private float CalculateRecall() {
            if (!_trackConfusionMatrix) return 0f;
            
            int truePositives = _confusionMatrix["Hostile"]["Hostile"];
            int falseNegatives = _confusionMatrix["Hostile"]["NonHostile"];
            
            if (truePositives + falseNegatives == 0) return 0f;
            return (float)truePositives / (truePositives + falseNegatives);
        }
        
        /// <summary>
        /// Calculates F1 score
        /// </summary>
        private float CalculateF1Score() {
            float precision = CalculatePrecision();
            float recall = CalculateRecall();
            
            if (precision + recall == 0) return 0f;
            return 2 * (precision * recall) / (precision + recall);
        }
        
        /// <summary>
        /// Calculates accuracy for a specific avatar type
        /// </summary>
        private float CalculateTypeAccuracy(string type) {
            var typeClassifications = _classifications.Where(c => c.actualType == type).ToList();
            if (typeClassifications.Count == 0) return 0f;
            
            int correct = typeClassifications.Count(c => c.isCorrect);
            return (float)correct / typeClassifications.Count;
        }
        
        /// <summary>
        /// Analyzes classifications by distance
        /// </summary>
        private Dictionary<string, float> AnalyzeByDistance() {
            var result = new Dictionary<string, float>();
            
            var closeRange = _classifications.Where(c => c.distance < 10f).ToList();
            var midRange = _classifications.Where(c => c.distance >= 10f && c.distance < 20f).ToList();
            var longRange = _classifications.Where(c => c.distance >= 20f).ToList();
            
            result["closeRangeAccuracy"] = closeRange.Count > 0 ? 
                (float)closeRange.Count(c => c.isCorrect) / closeRange.Count : 0f;
            result["midRangeAccuracy"] = midRange.Count > 0 ? 
                (float)midRange.Count(c => c.isCorrect) / midRange.Count : 0f;
            result["longRangeAccuracy"] = longRange.Count > 0 ? 
                (float)longRange.Count(c => c.isCorrect) / longRange.Count : 0f;
            
            return result;
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
            float sumSquares = values.Sum(v => (v - mean) * (v - mean));
            return Mathf.Sqrt(sumSquares / (values.Count - 1));
        }
        
        /// <summary>
        /// Updates metric parameters at runtime
        /// </summary>
        public void UpdateParameters(MetricParameters parameters) {
            _parameters = parameters;
        }
        
        /// <summary>
        /// Classification data structure
        /// </summary>
        [System.Serializable]
        private class ClassificationData {
            public System.DateTime timestamp;
            public int avatarId;
            public string actualType;
            public string classifiedAs;
            public bool isCorrect;
            public float reactionTime;
            public float timeSinceLastClassification;
            public Vector3 avatarPosition;
            public Vector3 playerPosition;
            public float distance;
        }
    }
}