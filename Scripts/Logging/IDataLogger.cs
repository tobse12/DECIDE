/*
 * Author: Tobias Sorg
 * Date: 2025-01-15
 * Description: Base interface and abstract class for data logging in the DECIDE VR framework
 * License: GPLv3
 */

using System;
using UnityEngine;

namespace DECIDE.Logging {
    /// <summary>
    /// Interface for all data logger implementations
    /// </summary>
    public interface IDataLogger {
        /// <summary>
        /// Initializes the logger
        /// </summary>
        void Initialize(params object[] parameters);
        
        /// <summary>
        /// Logs metric data
        /// </summary>
        void LogData(Core.MetricLogEntry data);
        
        /// <summary>
        /// Logs final analysis report
        /// </summary>
        void LogFinalReport(Core.FinalAnalysisReport report);
        
        /// <summary>
        /// Flushes any buffered data
        /// </summary>
        void Flush();
        
        /// <summary>
        /// Closes the logger
        /// </summary>
        void Close();
    }
    
    /// <summary>
    /// Abstract base class for data loggers
    /// </summary>
    public abstract class DataLoggerBase : MonoBehaviour, IDataLogger {
        [Header("Logger Settings")]
        [SerializeField] protected bool _enabled = true;
        [SerializeField] protected bool _bufferData = true;
        [SerializeField] protected int _bufferSize = 100;
        [SerializeField] protected float _autoFlushInterval = 5f;
        
        protected bool _isInitialized = false;
        protected float _nextFlushTime;
        protected string _sessionId;
        protected DateTime _sessionStartTime;
        
        /// <summary>
        /// Gets whether the logger is enabled
        /// </summary>
        public bool IsEnabled => _enabled;
        
        /// <summary>
        /// Gets whether the logger is initialized
        /// </summary>
        public bool IsInitialized => _isInitialized;
        
        protected virtual void Start() {
            _sessionId = Guid.NewGuid().ToString();
            _sessionStartTime = DateTime.Now;
            _nextFlushTime = Time.time + _autoFlushInterval;
        }
        
        protected virtual void Update() {
            if (_enabled && _bufferData && Time.time >= _nextFlushTime) {
                Flush();
                _nextFlushTime = Time.time + _autoFlushInterval;
            }
        }
        
        protected virtual void OnDestroy() {
            if (_isInitialized) {
                Flush();
                Close();
            }
        }
        
        protected virtual void OnApplicationPause(bool pauseStatus) {
            if (pauseStatus && _isInitialized) {
                Flush();
            }
        }
        
        protected virtual void OnApplicationFocus(bool hasFocus) {
            if (!hasFocus && _isInitialized) {
                Flush();
            }
        }
        
        /// <summary>
        /// Creates a timestamp string
        /// </summary>
        protected string GetTimestampString(DateTime? timestamp = null) {
            DateTime time = timestamp ?? DateTime.Now;
            return time.ToString("yyyy-MM-dd HH:mm:ss.fff");
        }
        
        /// <summary>
        /// Formats data for logging
        /// </summary>
        protected virtual string FormatData(object data) {
            return JsonUtility.ToJson(data, true);
        }
        
        // Abstract methods to be implemented by derived classes
        public abstract void Initialize(params object[] parameters);
        public abstract void LogData(Core.MetricLogEntry data);
        public abstract void LogFinalReport(Core.FinalAnalysisReport report);
        public abstract void Flush();
        public abstract void Close();
    }
}