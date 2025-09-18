/*
 * Author: Tobias Sorg
 * Date: 2025-01-15
 * Description: Manages all metric implementations and data collection in the DECIDE VR framework
 * License: GPLv3
 */

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DECIDE.Events;
using DECIDE.Metrics;
using DECIDE.Logging;

namespace DECIDE.Core {
    /// <summary>
    /// Central manager for all metric implementations and data collection
    /// </summary>
    public class MetricsManager : MonoBehaviour {
        [Header("Metric Configuration")]
        [SerializeField] private List<MetricConfiguration> _metricConfigurations;
        [SerializeField] private float _dataCollectionInterval = 0.1f;
        
        [Header("Logging")]
        [SerializeField] private bool _enableFileLogging = true;
        [SerializeField] private bool _enableNetworkLogging = true;
        [SerializeField] private string _logFilePath = "DECIDE_Metrics";
        [SerializeField] private string _networkEndpoint = "localhost";
        [SerializeField] private int _networkPort = 8080;
        
        // Active metrics
        private Dictionary<string, IMetric> _metrics;
        private List<IDataLogger> _loggers;
        private float _nextCollectionTime;
        
        // Singleton pattern
        private static MetricsManager _instance;
        public static MetricsManager Instance {
            get {
                if (_instance == null) {
                    _instance = FindObjectOfType<MetricsManager>();
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
            
            _metrics = new Dictionary<string, IMetric>();
            _loggers = new List<IDataLogger>();
            
            InitializeMetrics();
            InitializeLoggers();
        }
        
        private void OnEnable() {
            // Subscribe to events
            ScenarioEvents.OnScenarioStarted += HandleScenarioStarted;
            ScenarioEvents.OnScenarioEnded += HandleScenarioEnded;
            ScenarioEvents.OnAvatarClassified += HandleAvatarClassified;
            ScenarioEvents.OnAvatarTargeted += HandleAvatarTargeted;
        }
        
        private void OnDisable() {
            // Unsubscribe from events
            ScenarioEvents.OnScenarioStarted -= HandleScenarioStarted;
            ScenarioEvents.OnScenarioEnded -= HandleScenarioEnded;
            ScenarioEvents.OnAvatarClassified -= HandleAvatarClassified;
            ScenarioEvents.OnAvatarTargeted -= HandleAvatarTargeted;
        }
        
        private void Update() {
            // Update active metrics
            foreach (var metric in _metrics.Values) {
                if (metric.IsRecording) {
                    metric.UpdateMetric();
                }
            }
            
            // Collect data at intervals
            if (Time.time >= _nextCollectionTime) {
                CollectAndLogData();
                _nextCollectionTime = Time.time + _dataCollectionInterval;
            }
        }
        
        /// <summary>
        /// Initializes all configured metrics
        /// </summary>
        private void InitializeMetrics() {
            foreach (var config in _metricConfigurations) {
                IMetric metric = CreateMetric(config.metricType);
                if (metric != null) {
                    metric.Initialize(config.parameters);
                    _metrics[metric.Name] = metric;
                }
            }
        }
        
        /// <summary>
        /// Creates a metric instance based on type
        /// </summary>
        private IMetric CreateMetric(MetricType type) {
            GameObject metricObject = new GameObject($"Metric_{type}");
            metricObject.transform.SetParent(transform);
            
            switch (type) {
                case MetricType.Classification:
                    return metricObject.AddComponent<ClassificationMetric>();
                case MetricType.ConsecutiveErrors:
                    return metricObject.AddComponent<ConsecutiveErrorsMetric>();
                case MetricType.ReactionTime:
                    return metricObject.AddComponent<ReactionTimeMetric>();
                case MetricType.TargetingLatency:
                    return metricObject.AddComponent<TargetingLatencyMetric>();
                case MetricType.DecisionTime:
                    return metricObject.AddComponent<DecisionTimeMetric>();
                case MetricType.HeartRate:
                    return metricObject.AddComponent<HeartRateMetric>();
                case MetricType.HeadMovement:
                    return metricObject.AddComponent<HeadMovementMetric>();
                case MetricType.ControllerMovement:
                    return metricObject.AddComponent<ControllerMovementMetric>();
                case MetricType.GazeTracking:
                    return metricObject.AddComponent<GazeTrackingMetric>();
                case MetricType.StressLevel:
                    return metricObject.AddComponent<StressLevelMetric>();
                case MetricType.CognitiveLoad:
                    return metricObject.AddComponent<CognitiveLoadMetric>();
                case MetricType.SpatialAwareness:
                    return metricObject.AddComponent<SpatialAwarenessMetric>();
                default:
                    Debug.LogWarning($"Unknown metric type: {type}");
                    Destroy(metricObject);
                    return null;
            }
        }
        
        /// <summary>
        /// Initializes data loggers
        /// </summary>
        private void InitializeLoggers() {
            if (_enableFileLogging) {
                GameObject fileLoggerObject = new GameObject("FileLogger");
                fileLoggerObject.transform.SetParent(transform);
                var fileLogger = fileLoggerObject.AddComponent<FileDataLogger>();
                fileLogger.Initialize(_logFilePath);
                _loggers.Add(fileLogger);
            }
            
            if (_enableNetworkLogging) {
                GameObject networkLoggerObject = new GameObject("NetworkLogger");
                networkLoggerObject.transform.SetParent(transform);
                var networkLogger = networkLoggerObject.AddComponent<NetworkDataLogger>();
                networkLogger.Initialize(_networkEndpoint, _networkPort);
                _loggers.Add(networkLogger);
            }
        }
        
        /// <summary>
        /// Starts recording for a specific metric
        /// </summary>
        public void StartMetricRecording(string metricName) {
            if (_metrics.TryGetValue(metricName, out IMetric metric)) {
                metric.StartRecording();
            }
        }
        
        /// <summary>
        /// Stops recording for a specific metric
        /// </summary>
        public void StopMetricRecording(string metricName) {
            if (_metrics.TryGetValue(metricName, out IMetric metric)) {
                metric.StopRecording();
            }
        }
        
        /// <summary>
        /// Starts recording for all metrics
        /// </summary>
        public void StartAllMetrics() {
            foreach (var metric in _metrics.Values) {
                metric.StartRecording();
            }
        }
        
        /// <summary>
        /// Stops recording for all metrics
        /// </summary>
        public void StopAllMetrics() {
            foreach (var metric in _metrics.Values) {
                metric.StopRecording();
            }
        }
        
        /// <summary>
        /// Records a data point for a specific metric
        /// </summary>
        public void RecordDataPoint(string metricName, object data) {
            if (_metrics.TryGetValue(metricName, out IMetric metric)) {
                metric.RecordDataPoint(data);
                
                ScenarioEvents.TriggerMetricRecorded(new MetricRecordedEventData {
                    metricName = metricName,
                    value = data,
                    timestamp = System.DateTime.Now
                });
            }
        }
        
        /// <summary>
        /// Gets current data from all metrics
        /// </summary>
        public Dictionary<string, Dictionary<string, object>> GetAllMetricData() {
            var allData = new Dictionary<string, Dictionary<string, object>>();
            
            foreach (var kvp in _metrics) {
                allData[kvp.Key] = kvp.Value.GetData();
            }
            
            return allData;
        }
        
        /// <summary>
        /// Gets analysis results from all metrics
        /// </summary>
        public Dictionary<string, MetricAnalysisResult> GetAllAnalysisResults() {
            var results = new Dictionary<string, MetricAnalysisResult>();
            
            foreach (var kvp in _metrics) {
                results[kvp.Key] = kvp.Value.Analyze();
            }
            
            return results;
        }
        
        /// <summary>
        /// Collects and logs current metric data
        /// </summary>
        private void CollectAndLogData() {
            var data = GetAllMetricData();
            var timestamp = System.DateTime.Now;
            
            var logEntry = new MetricLogEntry {
                timestamp = timestamp,
                sessionId = System.Guid.NewGuid().ToString(),
                scenarioName = ScenarioManager.Instance?.Configuration?.scenarioName ?? "Unknown",
                elapsedTime = ScenarioManager.Instance?.ElapsedTime ?? 0f,
                metrics = data,
                activeStressors = StressManager.Instance?.GetActiveStressors()
                    .Select(s => new StressorInfo { name = s.Name, intensity = s.Intensity })
                    .ToList() ?? new List<StressorInfo>()
            };
            
            // Log to all configured loggers
            foreach (var logger in _loggers) {
                logger.LogData(logEntry);
            }
        }
        
        /// <summary>
        /// Resets all metrics
        /// </summary>
        public void ResetAllMetrics() {
            foreach (var metric in _metrics.Values) {
                metric.Reset();
            }
        }
        
        /// <summary>
        /// Updates metric parameters at runtime
        /// </summary>
        public void UpdateMetricParameters(string metricName, MetricParameters parameters) {
            if (_metrics.TryGetValue(metricName, out IMetric metric)) {
                metric.UpdateParameters(parameters);
            }
        }
        
        // Event handlers
        private void HandleScenarioStarted(ScenarioStartedEventData data) {
            ResetAllMetrics();
            StartAllMetrics();
        }
        
        private void HandleScenarioEnded(ScenarioEndedEventData data) {
            StopAllMetrics();
            
            // Generate final analysis
            var analysisResults = GetAllAnalysisResults();
            
            // Log final results
            var finalReport = new FinalAnalysisReport {
                timestamp = System.DateTime.Now,
                scenarioName = data.scenarioName,
                duration = data.elapsedTime,
                totalClassifications = data.totalClassifications,
                correctClassifications = data.correctClassifications,
                analysisResults = analysisResults
            };
            
            foreach (var logger in _loggers) {
                logger.LogFinalReport(finalReport);
            }
        }
        
        private void HandleAvatarClassified(AvatarClassifiedEventData data) {
            RecordDataPoint("Classification", data);
            RecordDataPoint("ReactionTime", data.reactionTime);
        }
        
        private void HandleAvatarTargeted(AvatarTargetedEventData data) {
            RecordDataPoint("TargetingLatency", data);
        }
    }
    
    /// <summary>
    /// Metric configuration container
    /// </summary>
    [System.Serializable]
    public class MetricConfiguration {
        public MetricType metricType;
        public MetricParameters parameters;
    }
    
    /// <summary>
    /// Available metric types
    /// </summary>
    public enum MetricType {
        Classification,
        ConsecutiveErrors,
        ReactionTime,
        TargetingLatency,
        DecisionTime,
        HeartRate,
        HeadMovement,
        ControllerMovement,
        GazeTracking,
        StressLevel,
        CognitiveLoad,
        SpatialAwareness
    }
    
    /// <summary>
    /// Metric log entry structure
    /// </summary>
    [System.Serializable]
    public class MetricLogEntry {
        public System.DateTime timestamp;
        public string sessionId;
        public string scenarioName;
        public float elapsedTime;
        public Dictionary<string, Dictionary<string, object>> metrics;
        public List<StressorInfo> activeStressors;
    }
    
    /// <summary>
    /// Stressor information for logging
    /// </summary>
    [System.Serializable]
    public class StressorInfo {
        public string name;
        public float intensity;
    }
    
    /// <summary>
    /// Final analysis report structure
    /// </summary>
    [System.Serializable]
    public class FinalAnalysisReport {
        public System.DateTime timestamp;
        public string scenarioName;
        public float duration;
        public int totalClassifications;
        public int correctClassifications;
        public Dictionary<string, MetricAnalysisResult> analysisResults;
    }
}