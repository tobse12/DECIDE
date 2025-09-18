/*
 * Author: Tobias Sorg
 * Date: 2025-01-15
 * Description: Interface for all metric implementations in the DECIDE VR framework
 * License: GPLv3
 */

using System.Collections.Generic;
using UnityEngine;

namespace DECIDE.Metrics {
    /// <summary>
    /// Base interface for all metric implementations
    /// </summary>
    public interface IMetric {
        /// <summary>
        /// Name of the metric
        /// </summary>
        string Name { get; }
        
        /// <summary>
        /// Whether the metric is currently recording
        /// </summary>
        bool IsRecording { get; }
        
        /// <summary>
        /// Initializes the metric with given parameters
        /// </summary>
        /// <param name="parameters">Configuration parameters for the metric</param>
        void Initialize(MetricParameters parameters);
        
        /// <summary>
        /// Starts recording the metric
        /// </summary>
        void StartRecording();
        
        /// <summary>
        /// Stops recording the metric
        /// </summary>
        void StopRecording();
        
        /// <summary>
        /// Resets the metric data
        /// </summary>
        void Reset();
        
        /// <summary>
        /// Records a data point
        /// </summary>
        /// <param name="data">Data to record</param>
        void RecordDataPoint(object data);
        
        /// <summary>
        /// Updates the metric (called each frame when recording)
        /// </summary>
        void UpdateMetric();
        
        /// <summary>
        /// Gets the current metric data
        /// </summary>
        /// <returns>Dictionary containing metric data</returns>
        Dictionary<string, object> GetData();
        
        /// <summary>
        /// Analyzes and returns metric results
        /// </summary>
        /// <returns>Analysis results</returns>
        MetricAnalysisResult Analyze();
        
        /// <summary>
        /// Updates configuration parameters at runtime
        /// </summary>
        /// <param name="parameters">New configuration parameters</param>
        void UpdateParameters(MetricParameters parameters);
    }
    
    /// <summary>
    /// Base class for metric configuration parameters
    /// </summary>
//    [System.Serializable]
    public class MetricParameters {
        public float samplingRate = 1f; // Samples per second
        public bool autoStart = true;
        public bool logRawData = true;
    }
}