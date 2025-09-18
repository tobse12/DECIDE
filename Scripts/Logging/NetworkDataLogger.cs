/*
 * Author: Tobias Sorg
 * Date: 2025-01-15
 * Description: Network-based data logger implementation for the DECIDE VR framework
 * License: GPLv3
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;

namespace DECIDE.Logging {
    /// <summary>
    /// Implements network-based data logging to a remote server
    /// </summary>
    public class NetworkDataLogger : DataLoggerBase {
        [Header("Network Settings")]
        [SerializeField] private string _serverUrl = "http://localhost:8080";
        [SerializeField] private int _port = 8080;
        [SerializeField] private string _apiEndpoint = "/api/metrics";
        [SerializeField] private float _connectionTimeout = 5f;
        [SerializeField] private int _maxRetries = 3;
        [SerializeField] private bool _useAuthentication = false;
        [SerializeField] private string _authToken = "";
        
        [Header("Batch Settings")]
        [SerializeField] private bool _batchSending = true;
        [SerializeField] private int _batchSize = 50;
        [SerializeField] private float _batchInterval = 10f;
        
        private string _fullUrl;
        private Queue<Core.MetricLogEntry> _sendQueue;
        private Queue<Core.FinalAnalysisReport> _reportQueue;
        private Coroutine _sendCoroutine;
        private bool _isConnected = false;
        private float _nextBatchTime;
        
        /// <summary>
        /// Initializes the network logger with server details
        /// </summary>
        public override void Initialize(params object[] parameters) {
            if (_isInitialized) {
                return;
            }
            
            if (parameters.Length > 0 && parameters[0] is string url) {
                _serverUrl = url;
            }
            if (parameters.Length > 1 && parameters[1] is int port) {
                _port = port;
            }
            
            _sendQueue = new Queue<Core.MetricLogEntry>();
            _reportQueue = new Queue<Core.FinalAnalysisReport>();
            
            // Construct full URL
            _fullUrl = _serverUrl;
            if (!_fullUrl.StartsWith("http://") && !_fullUrl.StartsWith("https://")) {
                _fullUrl = "http://" + _fullUrl;
            }
            if (_port != 80 && _port != 443) {
                _fullUrl = $"{_fullUrl}:{_port}";
            }
            _fullUrl += _apiEndpoint;
            
            // Test connection
            StartCoroutine(TestConnection());
            
            // Start batch sending coroutine
            if (_batchSending) {
                _sendCoroutine = StartCoroutine(BatchSendCoroutine());
            }
            
            _isInitialized = true;
            _nextBatchTime = Time.time + _batchInterval;
            
            Debug.Log($"NetworkDataLogger initialized. Target: {_fullUrl}");
        }
        
        /// <summary>
        /// Tests the connection to the server
        /// </summary>
        private IEnumerator TestConnection() {
            var testData = new {
                type = "connection_test",
                timestamp = DateTime.Now,
                sessionId = _sessionId
            };
            
            string json = JsonConvert.SerializeObject(testData);
            
            using (UnityWebRequest request = UnityWebRequest.Post(_fullUrl + "/test", json, "application/json")) {
                request.timeout = (int)_connectionTimeout;
                
                if (_useAuthentication && !string.IsNullOrEmpty(_authToken)) {
                    request.SetRequestHeader("Authorization", $"Bearer {_authToken}");
                }
                
                yield return request.SendWebRequest();
                
                if (request.result == UnityWebRequest.Result.Success) {
                    _isConnected = true;
                    Debug.Log("Successfully connected to metrics server");
                } else {
                    _isConnected = false;
                    Debug.LogWarning($"Failed to connect to metrics server: {request.error}");
                }
            }
        }
        
        /// <summary>
        /// Logs metric data to the network queue
        /// </summary>
        public override void LogData(Core.MetricLogEntry data) {
            if (!_enabled || !_isInitialized) {
                return;
            }
            
            lock (_sendQueue) {
                _sendQueue.Enqueue(data);
                
                if (!_batchSending || _sendQueue.Count >= _batchSize) {
                    StartCoroutine(SendDataImmediate());
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
            
            lock (_reportQueue) {
                _reportQueue.Enqueue(report);
            }
            
            StartCoroutine(SendReport(report));
        }
        
        /// <summary>
        /// Coroutine for batch sending data
        /// </summary>
        private IEnumerator BatchSendCoroutine() {
            while (_enabled && _isInitialized) {
                yield return new WaitForSeconds(_batchInterval);
                
                if (_sendQueue.Count > 0) {
                    yield return SendBatch();
                }
            }
        }
        
        /// <summary>
        /// Sends a batch of data to the server
        /// </summary>
        private IEnumerator SendBatch() {
            if (_sendQueue.Count == 0) {
                yield break;
            }
            
            List<Core.MetricLogEntry> batch = new List<Core.MetricLogEntry>();
            
            lock (_sendQueue) {
                int count = Mathf.Min(_batchSize, _sendQueue.Count);
                for (int i = 0; i < count; i++) {
                    batch.Add(_sendQueue.Dequeue());
                }
            }
            
            var payload = new {
                type = "metric_batch",
                sessionId = _sessionId,
                timestamp = DateTime.Now,
                data = batch
            };
            
            yield return SendToServer(payload, _fullUrl + "/batch");
        }
        
        /// <summary>
        /// Sends data immediately
        /// </summary>
        private IEnumerator SendDataImmediate() {
            if (_sendQueue.Count == 0) {
                yield break;
            }
            
            Core.MetricLogEntry data = null;
            lock (_sendQueue) {
                if (_sendQueue.Count > 0) {
                    data = _sendQueue.Dequeue();
                }
            }
            
            if (data != null) {
                var payload = new {
                    type = "metric_single",
                    sessionId = _sessionId,
                    timestamp = DateTime.Now,
                    data = data
                };
                
                yield return SendToServer(payload, _fullUrl);
            }
        }
        
        /// <summary>
        /// Sends a report to the server
        /// </summary>
        private IEnumerator SendReport(Core.FinalAnalysisReport report) {
            var payload = new {
                type = "final_report",
                sessionId = _sessionId,
                timestamp = DateTime.Now,
                report = report
            };
            
            yield return SendToServer(payload, _fullUrl + "/report");
        }
        
        /// <summary>
        /// Sends data to the server with retry logic
        /// </summary>
        private IEnumerator SendToServer(object data, string url) {
            string json = JsonConvert.SerializeObject(data, new JsonSerializerSettings {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            });
            
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            
            int retries = 0;
            bool success = false;
            
            while (retries < _maxRetries && !success) {
                using (UnityWebRequest request = new UnityWebRequest(url, "POST")) {
                    request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("Content-Type", "application/json");
                    request.timeout = (int)_connectionTimeout;
                    
                    if (_useAuthentication && !string.IsNullOrEmpty(_authToken)) {
                        request.SetRequestHeader("Authorization", $"Bearer {_authToken}");
                    }
                    
                    yield return request.SendWebRequest();
                    
                    if (request.result == UnityWebRequest.Result.Success) {
                        success = true;
                        _isConnected = true;
                    } else {
                        retries++;
                        Debug.LogWarning($"Failed to send data (attempt {retries}/{_maxRetries}): {request.error}");
                        
                        if (retries < _maxRetries) {
                            yield return new WaitForSeconds(Mathf.Pow(2, retries)); // Exponential backoff
                        }
                    }
                }
            }
            
            if (!success) {
                _isConnected = false;
                Debug.LogError($"Failed to send data after {_maxRetries} attempts");
                // Could implement local caching here for failed sends
            }
        }
        
        /// <summary>
        /// Flushes any buffered data
        /// </summary>
        public override void Flush() {
            if (!_isInitialized) {
                return;
            }
            
            if (_sendQueue.Count > 0) {
                StartCoroutine(SendBatch());
            }
        }
        
        /// <summary>
        /// Closes the network logger
        /// </summary>
        public override void Close() {
            if (!_isInitialized) {
                return;
            }
            
            // Stop batch sending coroutine
            if (_sendCoroutine != null) {
                StopCoroutine(_sendCoroutine);
                _sendCoroutine = null;
            }
            
            // Send any remaining data
            Flush();
            
            // Send session end notification
            StartCoroutine(SendSessionEnd());
            
            _isInitialized = false;
        }
        
        /// <summary>
        /// Sends a session end notification
        /// </summary>
        private IEnumerator SendSessionEnd() {
            var endData = new {
                type = "session_end",
                sessionId = _sessionId,
                timestamp = DateTime.Now,
                duration = (DateTime.Now - _sessionStartTime).TotalSeconds
            };
            
            yield return SendToServer(endData, _fullUrl + "/session/end");
        }
        
        /// <summary>
        /// Gets the connection status
        /// </summary>
        public bool IsConnected => _isConnected;
        
        /// <summary>
        /// Gets the number of queued items
        /// </summary>
        public int QueuedItems => _sendQueue?.Count ?? 0;
    }
}