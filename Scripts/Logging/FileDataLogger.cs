/*
 * Author: Tobias Sorg
 * Date: 2025-01-15
 * Description: File-based data logger implementation for the DECIDE VR framework
 * License: GPLv3
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using Newtonsoft.Json;

namespace DECIDE.Logging {
    /// <summary>
    /// Implements file-based data logging in JSON format
    /// </summary>
    public class FileDataLogger : DataLoggerBase {
        [Header("File Settings")]
        [SerializeField] private string _baseDirectory = "DECIDE_Data";
        [SerializeField] private bool _createSubfolderPerSession = true;
        [SerializeField] private bool _compressOldLogs = false;
        
        private string _currentLogPath;
        private string _metricsFilePath;
        private string _finalReportPath;
        private StreamWriter _metricsWriter;
        private List<Core.MetricLogEntry> _buffer;
        private object _lockObject = new object();
        
        /// <summary>
        /// Initializes the file logger with a base path
        /// </summary>
        public override void Initialize(params object[] parameters) {
            if (_isInitialized) {
                return;
            }
            
            if (parameters.Length > 0 && parameters[0] is string basePath) {
                _baseDirectory = basePath;
            }
            
            _buffer = new List<Core.MetricLogEntry>();
            
            // Create directory structure
            CreateDirectoryStructure();
            
            // Open file streams
            OpenFileStreams();
            
            // Write headers
            WriteFileHeaders();
            
            _isInitialized = true;
            
            Debug.Log($"FileDataLogger initialized. Logging to: {_currentLogPath}");
        }
        
        /// <summary>
        /// Creates the directory structure for logging
        /// </summary>
        private void CreateDirectoryStructure() {
            string rootPath = Path.Combine(Application.persistentDataPath, _baseDirectory);
            
            if (_createSubfolderPerSession) {
                string sessionFolder = $"Session_{_sessionStartTime:yyyy-MM-dd_HH-mm-ss}_{_sessionId.Substring(0, 8)}";
                _currentLogPath = Path.Combine(rootPath, sessionFolder);
            } else {
                _currentLogPath = rootPath;
            }
            
            if (!Directory.Exists(_currentLogPath)) {
                Directory.CreateDirectory(_currentLogPath);
            }
            
            _metricsFilePath = Path.Combine(_currentLogPath, "metrics.json");
            _finalReportPath = Path.Combine(_currentLogPath, "final_report.json");
        }
        
        /// <summary>
        /// Opens file streams for writing
        /// </summary>
        private void OpenFileStreams() {
            try {
                _metricsWriter = new StreamWriter(_metricsFilePath, append: false, Encoding.UTF8);
                _metricsWriter.WriteLine("["); // Start JSON array
            } catch (Exception e) {
                Debug.LogError($"Failed to open metrics file: {e.Message}");
                _enabled = false;
            }
        }
        
        /// <summary>
        /// Writes file headers with session information
        /// </summary>
        private void WriteFileHeaders() {
            var sessionInfo = new {
                sessionId = _sessionId,
                startTime = _sessionStartTime,
                unityVersion = Application.unityVersion,
                platform = Application.platform.ToString(),
                deviceModel = SystemInfo.deviceModel,
                deviceType = SystemInfo.deviceType.ToString(),
                vrDevice = UnityEngine.XR.XRSettings.loadedDeviceName
            };
            
            string sessionInfoPath = Path.Combine(_currentLogPath, "session_info.json");
            File.WriteAllText(sessionInfoPath, JsonConvert.SerializeObject(sessionInfo, Formatting.Indented));
        }
        
        /// <summary>
        /// Logs metric data to file
        /// </summary>
        public override void LogData(Core.MetricLogEntry data) {
            if (!_enabled || !_isInitialized) {
                return;
            }
            
            lock (_lockObject) {
                if (_bufferData) {
                    _buffer.Add(data);
                    
                    if (_buffer.Count >= _bufferSize) {
                        Flush();
                    }
                } else {
                    WriteDataToFile(data);
                }
            }
        }
        
        /// <summary>
        /// Logs the final analysis report
        /// </summary>
        public override void LogFinalReport(Core.FinalAnalysisReport report) {
            if (!_enabled || !_isInitialized) {
                return;
            }
            
            try {
                string json = JsonConvert.SerializeObject(report, Formatting.Indented, new JsonSerializerSettings {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                });
                File.WriteAllText(_finalReportPath, json);
                
                Debug.Log($"Final report saved to: {_finalReportPath}");
            } catch (Exception e) {
                Debug.LogError($"Failed to write final report: {e.Message}");
            }
        }
        
        /// <summary>
        /// Writes a single data entry to file
        /// </summary>
        private void WriteDataToFile(Core.MetricLogEntry data) {
            try {
                string json = JsonConvert.SerializeObject(data, Formatting.None, new JsonSerializerSettings {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                });
                
                if (_metricsWriter.BaseStream.Position > 2) { // Not first entry
                    _metricsWriter.WriteLine(",");
                }
                _metricsWriter.Write(json);
            } catch (Exception e) {
                Debug.LogError($"Failed to write metric data: {e.Message}");
            }
        }
        
        /// <summary>
        /// Flushes buffered data to file
        /// </summary>
        public override void Flush() {
            if (!_isInitialized || _metricsWriter == null) {
                return;
            }
            
            lock (_lockObject) {
                if (_buffer.Count > 0) {
                    foreach (var entry in _buffer) {
                        WriteDataToFile(entry);
                    }
                    _buffer.Clear();
                }
                
                _metricsWriter?.Flush();
            }
        }
        
        /// <summary>
        /// Closes the file logger
        /// </summary>
        public override void Close() {
            if (!_isInitialized) {
                return;
            }
            
            lock (_lockObject) {
                Flush();
                
                // Close JSON array
                if (_metricsWriter != null) {
                    _metricsWriter.WriteLine();
                    _metricsWriter.WriteLine("]");
                    _metricsWriter.Close();
                    _metricsWriter = null;
                }
                
                // Compress old logs if enabled
                if (_compressOldLogs) {
                    CompressLogs();
                }
                
                _isInitialized = false;
            }
        }
        
        /// <summary>
        /// Compresses log files to save space
        /// </summary>
        private void CompressLogs() {
            try {
                string zipPath = _currentLogPath + ".zip";
                System.IO.Compression.ZipFile.CreateFromDirectory(_currentLogPath, zipPath);
                Directory.Delete(_currentLogPath, true);
                Debug.Log($"Logs compressed to: {zipPath}");
            } catch (Exception e) {
                Debug.LogError($"Failed to compress logs: {e.Message}");
            }
        }
        
        /// <summary>
        /// Gets the current log file path
        /// </summary>
        public string GetLogPath() {
            return _currentLogPath;
        }
        
        /// <summary>
        /// Gets the size of current log files in bytes
        /// </summary>
        public long GetLogSize() {
            if (!Directory.Exists(_currentLogPath)) {
                return 0;
            }
            
            long size = 0;
            DirectoryInfo dirInfo = new DirectoryInfo(_currentLogPath);
            foreach (FileInfo file in dirInfo.GetFiles("*", SearchOption.AllDirectories)) {
                size += file.Length;
            }
            return size;
        }
    }
}