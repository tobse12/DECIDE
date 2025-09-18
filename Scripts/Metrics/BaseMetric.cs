/*
 * Author: Tobias Sorg
 * Date: 2025-01-18
 * Summary: Base implementation for all metrics in the DECIDE VR Framework. Provides common 
 *          functionality for data recording, event logging, and metric analysis.
 * License: GPLv3
 */

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DECIDE.Metrics
{
    /// <summary>
    /// Base implementation of the IMetric interface
    /// </summary>
    public abstract class BaseMetric : MonoBehaviour, IMetric
    {
        #region Protected Fields
        
        protected bool isTracking = false;
        protected float trackingStartTime;
        protected float trackingEndTime;
        protected MetricParameters parameters;
        
        // Data storage
        protected List<Dictionary<string, object>> rawDataPoints = new List<Dictionary<string, object>>();
        protected List<string> eventLog = new List<string>();
        
        // Sampling control
        protected float lastSampleTime;
        protected float samplingInterval;
        
        #endregion
        
        #region Properties
        
        public string Name => MetricName;
        public abstract string MetricName { get; }
        
        public bool IsRecording => isTracking;
        
        public float RecordingDuration => isTracking ? 
            Time.time - trackingStartTime : 
            trackingEndTime - trackingStartTime;
        
        #endregion
        
        #region IMetric Implementation
        
        public virtual void Initialize(MetricParameters parameters)
        {
            this.parameters = parameters ?? new MetricParameters();
            samplingInterval = 1f / this.parameters.samplingRate;
            
            if (this.parameters.autoStart)
            {
                StartRecording();
            }
        }
        
        public virtual void StartRecording()
        {
            if (isTracking) return;
            
            isTracking = true;
            trackingStartTime = Time.time;
            lastSampleTime = Time.time;
            
            LogEvent($"{MetricName} started recording");
            OnStartTracking();
        }
        
        public virtual void StopRecording()
        {
            if (!isTracking) return;
            
            isTracking = false;
            trackingEndTime = Time.time;
            
            LogEvent($"{MetricName} stopped recording");
            OnStopTracking();
        }
        
        public virtual void Reset()
        {
            isTracking = false;
            rawDataPoints.Clear();
            eventLog.Clear();
            trackingStartTime = 0;
            trackingEndTime = 0;
            lastSampleTime = 0;
            
            OnReset();
        }
        
        public virtual void RecordDataPoint(object data)
        {
            if (!isTracking) return;
            
            var dataPoint = new Dictionary<string, object>
            {
                ["timestamp"] = Time.time,
                ["relativeTime"] = Time.time - trackingStartTime,
                ["data"] = data
            };
            
            rawDataPoints.Add(dataPoint);
            
            if (parameters.logRawData)
            {
                Debug.Log($"[{MetricName}] Data recorded: {data}");
            }
        }
        
        public virtual void UpdateMetric()
        {
            if (!isTracking) return;
            
            // Check sampling rate
            if (Time.time - lastSampleTime >= samplingInterval)
            {
                lastSampleTime = Time.time;
                OnUpdateMetric();
            }
        }
        
        public virtual Dictionary<string, object> GetData()
        {
            var data = new Dictionary<string, object>
            {
                ["metricName"] = MetricName,
                ["recordingDuration"] = RecordingDuration,
                ["sampleCount"] = rawDataPoints.Count,
                ["startTime"] = trackingStartTime,
                ["endTime"] = trackingEndTime,
                ["isRecording"] = isTracking,
                ["events"] = eventLog.ToList()
            };
            
            // Add derived metric data
            var metricData = GetMetricData();
            foreach (var kvp in metricData)
            {
                data[kvp.Key] = kvp.Value;
            }
            
            return data;
        }
        
        public virtual MetricAnalysisResult Analyze()
        {
            var result = new MetricAnalysisResult
            {
                metricName = MetricName,
                sampleCount = rawDataPoints.Count
            };
            
            // Perform metric-specific analysis
            PerformAnalysis(result);
            
            return result;
        }
        
        public virtual void UpdateParameters(MetricParameters parameters)
        {
            this.parameters = parameters;
            samplingInterval = 1f / this.parameters.samplingRate;
            OnParametersUpdated();
        }
        
        #endregion
        
        #region Unity Lifecycle
        
        protected virtual void Awake()
        {
            // Initialize with default parameters if not already initialized
            if (parameters == null)
            {
                Initialize(new MetricParameters());
            }
        }
        
        protected virtual void Update()
        {
            UpdateMetric();
        }
        
        protected virtual void OnDestroy()
        {
            if (isTracking)
            {
                StopRecording();
            }
        }
        
        #endregion
        
        #region Protected Virtual Methods
        
        /// <summary>
        /// Called when tracking starts
        /// </summary>
        protected virtual void OnStartTracking()
        {
            // Override in derived classes
        }
        
        /// <summary>
        /// Called when tracking stops
        /// </summary>
        protected virtual void OnStopTracking()
        {
            // Override in derived classes
        }
        
        /// <summary>
        /// Called when metric is reset
        /// </summary>
        protected virtual void OnReset()
        {
            // Override in derived classes
        }
        
        /// <summary>
        /// Called on each update cycle when sampling rate allows
        /// </summary>
        protected virtual void OnUpdateMetric()
        {
            // Override in derived classes
        }
        
        /// <summary>
        /// Called when parameters are updated
        /// </summary>
        protected virtual void OnParametersUpdated()
        {
            // Override in derived classes
        }
        
        /// <summary>
        /// Returns metric-specific data
        /// </summary>
        protected virtual Dictionary<string, object> GetMetricData()
        {
            return new Dictionary<string, object>();
        }
        
        /// <summary>
        /// Performs metric-specific analysis
        /// </summary>
        protected virtual void PerformAnalysis(MetricAnalysisResult result)
        {
            // Override in derived classes
        }
        
        #endregion
        
        #region Protected Helper Methods
        
        /// <summary>
        /// Logs an event with timestamp
        /// </summary>
        protected void LogEvent(string eventDescription)
        {
            string logEntry = $"[{Time.time - trackingStartTime:F3}s] {eventDescription}";
            eventLog.Add(logEntry);
            
            if (parameters != null && parameters.logRawData)
            {
                Debug.Log($"[{MetricName}] {logEntry}");
            }
        }
        
        /// <summary>
        /// Calculates mean of a numeric list
        /// </summary>
        protected float CalculateMean(List<float> values)
        {
            if (values == null || values.Count == 0) return 0;
            return values.Average();
        }
        
        /// <summary>
        /// Calculates median of a numeric list
        /// </summary>
        protected float CalculateMedian(List<float> values)
        {
            if (values == null || values.Count == 0) return 0;
            
            var sorted = values.OrderBy(v => v).ToList();
            int middle = sorted.Count / 2;
            
            if (sorted.Count % 2 == 0)
            {
                return (sorted[middle - 1] + sorted[middle]) / 2f;
            }
            
            return sorted[middle];
        }
        
        /// <summary>
        /// Calculates standard deviation of a numeric list
        /// </summary>
        protected float CalculateStandardDeviation(List<float> values)
        {
            if (values == null || values.Count <= 1) return 0;
            
            float mean = values.Average();
            float sumOfSquaredDifferences = values.Sum(v => Mathf.Pow(v - mean, 2));
            return Mathf.Sqrt(sumOfSquaredDifferences / (values.Count - 1));
        }
        
        /// <summary>
        /// Calculates variance of a numeric list
        /// </summary>
        protected float CalculateVariance(List<float> values)
        {
            if (values == null || values.Count <= 1) return 0;
            
            float mean = values.Average();
            return values.Sum(v => Mathf.Pow(v - mean, 2)) / (values.Count - 1);
        }
        
        /// <summary>
        /// Gets percentile value from a list
        /// </summary>
        protected float GetPercentile(List<float> values, float percentile)
        {
            if (values == null || values.Count == 0) return 0;
            
            var sorted = values.OrderBy(v => v).ToList();
            int index = (int)Math.Ceiling(percentile / 100f * sorted.Count) - 1;
            index = Mathf.Clamp(index, 0, sorted.Count - 1);
            
            return sorted[index];
        }
        
        #endregion
        
        #region Public Helper Methods
        
        /// <summary>
        /// Exports metric data to JSON string
        /// </summary>
        public string ExportToJson()
        {
            return JsonUtility.ToJson(GetData());
        }
        
        /// <summary>
        /// Gets a summary of the metric
        /// </summary>
        public virtual string GetSummary()
        {
            return $"{MetricName}: {rawDataPoints.Count} samples over {RecordingDuration:F2}s";
        }
        
        #endregion
    }
    
    /// <summary>
    /// Base class for metric configuration parameters
    /// </summary>
    [Serializable]
    public class MetricParameters
    {
        [Tooltip("Number of samples per second")]
        public float samplingRate = 30f;
        
        [Tooltip("Automatically start recording when initialized")]
        public bool autoStart = false;
        
        [Tooltip("Log raw data points to console")]
        public bool logRawData = false;
        
        [Tooltip("Enable advanced analysis features")]
        public bool enableAdvancedAnalysis = true;
        
        [Tooltip("Maximum number of data points to keep in memory")]
        public int maxDataPoints = 10000;
    }
    
    /// <summary>
    /// Container for metric analysis results
    /// </summary>
    [Serializable]
    public class MetricAnalysisResult
    {
        public string metricName;
        public float mean;
        public float median;
        public float standardDeviation;
        public float min;
        public float max;
        public int sampleCount;
        public float duration;
        public Dictionary<string, object> additionalData;
        
        public MetricAnalysisResult()
        {
            additionalData = new Dictionary<string, object>();
        }
        
        public string ToJson()
        {
            return JsonUtility.ToJson(this, true);
        }
    }
}