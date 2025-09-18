/*
 * Author: Tobias Sorg
 * Date: 2025-01-18
 * Summary: Situational awareness measurement metric for DECIDE VR Framework. Tracks spatial
 *          awareness, threat detection, environment scanning, and tactical positioning.
 * License: GPLv3
 */

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DECIDE.Metrics
{
    /// <summary>
    /// Measures situational awareness through spatial and threat perception
    /// </summary>
    public class SituationalAwarenessMetric : BaseMetric
    {
        #region Data Structures
        
        private struct AwarenessEvent
        {
            public string eventType;
            public GameObject target;
            public float detectionTime;
            public float distance;
            public float angle;
            public bool wasInPeriphery;
            public float timestamp;
        }
        
        private struct ThreatAssessment
        {
            public GameObject threat;
            public AvatarType type;
            public float threatLevel;
            public float distance;
            public Vector3 direction;
            public bool isTracked;
            public float lastUpdateTime;
        }
        
        private struct SpatialScan
        {
            public float startAngle;
            public float endAngle;
            public float duration;
            public int objectsDetected;
            public float timestamp;
        }
        
        #endregion
        
        #region Private Fields
        
        // Awareness tracking
        private List<AwarenessEvent> awarenessEvents = new List<AwarenessEvent>();
        private Dictionary<GameObject, ThreatAssessment> knownThreats = new Dictionary<GameObject, ThreatAssessment>();
        private Dictionary<GameObject, float> objectDetectionTimes = new Dictionary<GameObject, float>();
        
        // Spatial coverage
        private float[,] spatialCoverageGrid;
        private const int GRID_SIZE = 36; // 10-degree sectors
        private float totalSpatialCoverage;
        private List<SpatialScan> scanPatterns = new List<SpatialScan>();
        
        // Environment awareness
        private HashSet<GameObject> detectedObjects = new HashSet<GameObject>();
        private HashSet<GameObject> missedObjects = new HashSet<GameObject>();
        private int totalObjectsInScene;
        
        // Performance metrics
        private float averageDetectionTime;
        private float peripheralDetectionRate;
        private float threatPrioritizationScore;
        private float spatialMemoryScore;
        
        // Scanning behavior
        private float currentScanAngle;
        private float lastScanAngle;
        private float scanStartTime;
        private bool isScanning;
        private int completedScans;
        
        // Field of view parameters
        private const float CENTRAL_FOV = 60f;
        private const float PERIPHERAL_FOV = 180f;
        private const float DETECTION_RANGE = 50f;
        
        // References
        private HeadMovementMetric headMovement;
        private GazeTrackingMetric gazeTracking;
        
        #endregion
        
        #region Properties
        
        public override string MetricName => "SituationalAwareness";
        
        public float AwarenessScore { get; private set; }
        
        public float SpatialCoverage => totalSpatialCoverage;
        
        public float ThreatDetectionAccuracy { get; private set; }
        
        public int ActiveThreats => knownThreats.Count(t => t.Value.isTracked);
        
        #endregion
        
        #region Base Metric Implementation
        
        protected override void OnStartTracking()
        {
            base.OnStartTracking();
            InitializeSpatialGrid();
            FindReferences();
            SubscribeToEvents();
        }
        
        protected override void OnStopTracking()
        {
            base.OnStopTracking();
            UnsubscribeFromEvents();
            CalculateFinalScores();
        }
        
        protected override void OnUpdateMetric()
        {
            UpdateSpatialCoverage();
            UpdateThreatTracking();
            DetectScanningPatterns();
            UpdateAwarenessScore();
        }
        
        #endregion
        
        #region Initialization
        
        private void InitializeSpatialGrid()
        {
            spatialCoverageGrid = new float[GRID_SIZE, GRID_SIZE];
            totalSpatialCoverage = 0;
        }
        
        private void FindReferences()
        {
            headMovement = FindObjectOfType<HeadMovementMetric>();
            gazeTracking = FindObjectOfType<GazeTrackingMetric>();
        }
        
        #endregion
        
        #region Event Handling
        
        private void SubscribeToEvents()
        {
            if (ScenarioEvents.Instance != null)
            {
                ScenarioEvents.Instance.OnAvatarSpawned += OnAvatarSpawned;
                ScenarioEvents.Instance.OnAvatarDetected += OnAvatarDetected;
                ScenarioEvents.Instance.OnAvatarLost += OnAvatarLost;
                ScenarioEvents.Instance.OnObjectEntersView += OnObjectEntersView;
            }
        }
        
        private void UnsubscribeFromEvents()
        {
            if (ScenarioEvents.Instance != null)
            {
                ScenarioEvents.Instance.OnAvatarSpawned -= OnAvatarSpawned;
                ScenarioEvents.Instance.OnAvatarDetected -= OnAvatarDetected;
                ScenarioEvents.Instance.OnAvatarLost -= OnAvatarLost;
                ScenarioEvents.Instance.OnObjectEntersView -= OnObjectEntersView;
            }
        }
        
        private void OnAvatarSpawned(Avatar avatar)
        {
            totalObjectsInScene++;
            
            // Check if avatar is immediately visible
            if (IsObjectVisible(avatar.gameObject))
            {
                OnAvatarDetected(avatar);
            }
            else
            {
                missedObjects.Add(avatar.gameObject);
            }
        }
        
        private void OnAvatarDetected(Avatar avatar)
        {
            if (objectDetectionTimes.ContainsKey(avatar.gameObject))
                return;
            
            float detectionTime = Time.time;
            objectDetectionTimes[avatar.gameObject] = detectionTime;
            
            // Calculate detection delay
            float detectionDelay = 0;
            if (avatar != null)
            {
                detectionDelay = detectionTime - avatar.SpawnTime;
            }
            
            // Determine if in periphery
            Vector3 toTarget = avatar.transform.position - Camera.main.transform.position;
            float angle = Vector3.Angle(Camera.main.transform.forward, toTarget);
            bool inPeriphery = angle > CENTRAL_FOV / 2f;
            
            // Record awareness event
            var awarenessEvent = new AwarenessEvent
            {
                eventType = "Detection",
                target = avatar.gameObject,
                detectionTime = detectionDelay,
                distance = toTarget.magnitude,
                angle = angle,
                wasInPeriphery = inPeriphery,
                timestamp = Time.time
            };
            
            awarenessEvents.Add(awarenessEvent);
            detectedObjects.Add(avatar.gameObject);
            missedObjects.Remove(avatar.gameObject);
            
            // Create threat assessment
            CreateThreatAssessment(avatar);
            
            LogEvent($"Avatar detected: {avatar.Type} (delay: {detectionDelay:F2}s, periphery: {inPeriphery})");
        }
        
        private void OnAvatarLost(Avatar avatar)
        {
            if (knownThreats.ContainsKey(avatar.gameObject))
            {
                var threat = knownThreats[avatar.gameObject];
                threat.isTracked = false;
                threat.lastUpdateTime = Time.time;
                knownThreats[avatar.gameObject] = threat;
                
                LogEvent($"Lost track of {avatar.Type}");
            }
        }
        
        private void OnObjectEntersView(GameObject obj)
        {
            if (!detectedObjects.Contains(obj))
            {
                detectedObjects.Add(obj);
                missedObjects.Remove(obj);
            }
        }
        
        #endregion
        
        #region Spatial Coverage
        
        private void UpdateSpatialCoverage()
        {
            if (Camera.main == null) return;
            
            Vector3 forward = Camera.main.transform.forward;
            float yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            float pitch = Mathf.Asin(forward.y) * Mathf.Rad2Deg;
            
            // Map to grid coordinates
            int yawIndex = Mathf.FloorToInt((yaw + 180f) / 10f) % GRID_SIZE;
            int pitchIndex = Mathf.FloorToInt((pitch + 90f) / 10f) % GRID_SIZE;
            
            // Update coverage grid
            spatialCoverageGrid[yawIndex, pitchIndex] = Mathf.Min(
                spatialCoverageGrid[yawIndex, pitchIndex] + Time.deltaTime,
                1f
            );
            
            // Calculate total coverage
            CalculateTotalCoverage();
            
            // Track scanning angle
            currentScanAngle = yaw;
        }
        
        private void CalculateTotalCoverage()
        {
            int coveredSectors = 0;
            for (int i = 0; i < GRID_SIZE; i++)
            {
                for (int j = 0; j < GRID_SIZE; j++)
                {
                    if (spatialCoverageGrid[i, j] > 0.1f)
                        coveredSectors++;
                }
            }
            
            totalSpatialCoverage = (float)coveredSectors / (GRID_SIZE * GRID_SIZE) * 100f;
        }
        
        #endregion
        
        #region Threat Tracking
        
        private void UpdateThreatTracking()
        {
            var threatsToRemove = new List<GameObject>();
            
            foreach (var kvp in knownThreats.ToList())
            {
                var threat = kvp.Value;
                
                // Check if threat is still visible
                bool isVisible = IsObjectVisible(kvp.Key);
                
                if (isVisible)
                {
                    // Update threat assessment
                    threat.distance = Vector3.Distance(
                        Camera.main.transform.position,
                        kvp.Key.transform.position
                    );
                    threat.direction = (kvp.Key.transform.position - 
                                      Camera.main.transform.position).normalized;
                    threat.isTracked = true;
                    threat.lastUpdateTime = Time.time;
                    
                    // Update threat level based on type and distance
                    threat.threatLevel = CalculateThreatLevel(threat);
                }
                else if (Time.time - threat.lastUpdateTime > 5f)
                {
                    // Remove threats not seen for 5 seconds
                    threatsToRemove.Add(kvp.Key);
                    continue;
                }
                
                knownThreats[kvp.Key] = threat;
            }
            
            // Remove old threats
            foreach (var threat in threatsToRemove)
            {
                knownThreats.Remove(threat);
            }
            
            // Calculate threat prioritization score
            CalculateThreatPrioritization();
        }
        
        private void CreateThreatAssessment(Avatar avatar)
        {
            var threat = new ThreatAssessment
            {
                threat = avatar.gameObject,
                type = avatar.Type,
                distance = Vector3.Distance(
                    Camera.main.transform.position,
                    avatar.transform.position
                ),
                direction = (avatar.transform.position - 
                           Camera.main.transform.position).normalized,
                isTracked = true,
                lastUpdateTime = Time.time
            };
            
            threat.threatLevel = CalculateThreatLevel(threat);
            knownThreats[avatar.gameObject] = threat;
        }
        
        private float CalculateThreatLevel(ThreatAssessment threat)
        {
            float level = 0;
            
            // Base threat by type
            switch (threat.type)
            {
                case AvatarType.Hostile:
                    level = 100f;
                    break;
                case AvatarType.Unknown:
                    level = 50f;
                    break;
                case AvatarType.Friendly:
                    level = 10f;
                    break;
            }
            
            // Modify by distance (closer = higher threat)
            float distanceModifier = Mathf.Clamp01(1f - (threat.distance / DETECTION_RANGE));
            level *= (0.5f + distanceModifier * 0.5f);
            
            return level;
        }
        
        private void CalculateThreatPrioritization()
        {
            if (knownThreats.Count == 0)
            {
                threatPrioritizationScore = 100f;
                return;
            }
            
            // Check if highest threats are being tracked
            var sortedThreats = knownThreats.Values
                .OrderByDescending(t => t.threatLevel)
                .ToList();
            
            float score = 0;
            int topThreatsToCheck = Mathf.Min(3, sortedThreats.Count);
            
            for (int i = 0; i < topThreatsToCheck; i++)
            {
                if (sortedThreats[i].isTracked)
                    score += 100f / topThreatsToCheck;
            }
            
            threatPrioritizationScore = score;
        }
        
        #endregion
        
        #region Scanning Patterns
        
        private void DetectScanningPatterns()
        {
            float angleDiff = Mathf.Abs(currentScanAngle - lastScanAngle);
            
            // Detect start of scanning movement
            if (!isScanning && angleDiff > 5f)
            {
                isScanning = true;
                scanStartTime = Time.time;
            }
            
            // Detect end of scanning movement
            if (isScanning && angleDiff < 1f)
            {
                float scanDuration = Time.time - scanStartTime;
                
                if (scanDuration > 0.5f)
                {
                    // Record scan pattern
                    var scan = new SpatialScan
                    {
                        startAngle = lastScanAngle,
                        endAngle = currentScanAngle,
                        duration = scanDuration,
                        objectsDetected = CountObjectsInScanArc(lastScanAngle, currentScanAngle),
                        timestamp = Time.time
                    };
                    
                    scanPatterns.Add(scan);
                    completedScans++;
                    
                    LogEvent($"Scan completed: {Mathf.Abs(currentScanAngle - lastScanAngle):F0}° in {scanDuration:F2}s");
                }
                
                isScanning = false;
            }
            
            lastScanAngle = currentScanAngle;
        }
        
        private int CountObjectsInScanArc(float startAngle, float endAngle)
        {
            int count = 0;
            float minAngle = Mathf.Min(startAngle, endAngle);
            float maxAngle = Mathf.Max(startAngle, endAngle);
            
            foreach (var obj in detectedObjects)
            {
                if (obj == null) continue;
                
                Vector3 toObject = obj.transform.position - Camera.main.transform.position;
                float objectAngle = Mathf.Atan2(toObject.x, toObject.z) * Mathf.Rad2Deg;
                
                if (objectAngle >= minAngle && objectAngle <= maxAngle)
                    count++;
            }
            
            return count;
        }
        
        #endregion
        
        #region Awareness Score Calculation
        
        private void UpdateAwarenessScore()
        {
            float score = 0;
            float weightSum = 0;
            
            // Detection performance (30%)
            if (totalObjectsInScene > 0)
            {
                float detectionRate = (float)detectedObjects.Count / totalObjectsInScene;
                score += detectionRate * 30f;
                weightSum += 30f;
            }
            
            // Spatial coverage (20%)
            score += totalSpatialCoverage * 0.2f;
            weightSum += 20f;
            
            // Threat prioritization (25%)
            score += threatPrioritizationScore * 0.25f;
            weightSum += 25f;
            
            // Peripheral awareness (15%)
            if (awarenessEvents.Count > 0)
            {
                int peripheralDetections = awarenessEvents.Count(e => e.wasInPeriphery);
                peripheralDetectionRate = (float)peripheralDetections / awarenessEvents.Count;
                score += peripheralDetectionRate * 15f;
                weightSum += 15f;
            }
            
            // Scanning behavior (10%)
            float scanScore = Mathf.Clamp01(completedScans / 10f) * 100f;
            score += scanScore * 0.1f;
            weightSum += 10f;
            
            // Normalize score
            AwarenessScore = weightSum > 0 ? (score / weightSum) * 100f : 0;
        }
        
        #endregion
        
        #region Helper Methods
        
        private bool IsObjectVisible(GameObject obj)
        {
            if (obj == null || Camera.main == null) return false;
            
            Vector3 toObject = obj.transform.position - Camera.main.transform.position;
            float distance = toObject.magnitude;
            
            // Check distance
            if (distance > DETECTION_RANGE) return false;
            
            // Check angle (within FOV)
            float angle = Vector3.Angle(Camera.main.transform.forward, toObject);
            if (angle > PERIPHERAL_FOV / 2f) return false;
            
            // Check line of sight
            RaycastHit hit;
            if (Physics.Raycast(Camera.main.transform.position, toObject.normalized, 
                              out hit, distance))
            {
                return hit.collider.gameObject == obj || 
                       hit.collider.transform.IsChildOf(obj.transform);
            }
            
            return true;
        }
        
        private void CalculateFinalScores()
        {
            // Calculate average detection time
            if (awarenessEvents.Count > 0)
            {
                averageDetectionTime = awarenessEvents.Average(e => e.detectionTime);
            }
            
            // Calculate threat detection accuracy
            if (knownThreats.Count > 0)
            {
                int correctlyIdentified = knownThreats.Count(t => 
                    t.Value.type == AvatarType.Hostile && t.Value.threatLevel > 75f ||
                    t.Value.type == AvatarType.Friendly && t.Value.threatLevel < 25f
                );
                
                ThreatDetectionAccuracy = (float)correctlyIdentified / knownThreats.Count * 100f;
            }
            
            // Calculate spatial memory score
            CalculateSpatialMemory();
        }
        
        private void CalculateSpatialMemory()
        {
            // Score based on ability to track multiple threats
            if (knownThreats.Count == 0)
            {
                spatialMemoryScore = 100f;
                return;
            }
            
            float totalTrackingTime = 0;
            float totalPossibleTime = 0;
            
            foreach (var threat in knownThreats.Values)
            {
                float threatDuration = Time.time - (threat.lastUpdateTime - 5f); // 5s memory window
                totalPossibleTime += threatDuration;
                
                if (threat.isTracked)
                {
                    totalTrackingTime += threatDuration;
                }
            }
            
            spatialMemoryScore = totalPossibleTime > 0 ? 
                (totalTrackingTime / totalPossibleTime) * 100f : 100f;
        }
        
        #endregion
        
        #region Data Export
        
        protected override Dictionary<string, object> GetMetricData()
        {
            var data = base.GetMetricData();
            
            // Overall scores
            data["awarenessScore"] = AwarenessScore;
            data["spatialCoverage"] = totalSpatialCoverage;
            data["threatDetectionAccuracy"] = ThreatDetectionAccuracy;
            data["spatialMemoryScore"] = spatialMemoryScore;
            
            // Detection metrics
            data["totalObjectsDetected"] = detectedObjects.Count;
            data["totalObjectsMissed"] = missedObjects.Count;
            data["detectionRate"] = totalObjectsInScene > 0 ? 
                (float)detectedObjects.Count / totalObjectsInScene * 100f : 0;
            data["averageDetectionTime"] = averageDetectionTime;
            data["peripheralDetectionRate"] = peripheralDetectionRate * 100f;
            
            // Threat tracking
            data["activeThreats"] = ActiveThreats;
            data["totalThreatsIdentified"] = knownThreats.Count;
            data["threatPrioritizationScore"] = threatPrioritizationScore;
            
            // Scanning behavior
            data["completedScans"] = completedScans;
            data["averageScanDuration"] = scanPatterns.Count > 0 ? 
                scanPatterns.Average(s => s.duration) : 0;
            data["averageScanArc"] = scanPatterns.Count > 0 ? 
                scanPatterns.Average(s => Mathf.Abs(s.endAngle - s.startAngle)) : 0;
            
            // Spatial distribution
            var quadrantCoverage = CalculateQuadrantCoverage();
            data["frontCoverage"] = quadrantCoverage["front"];
            data["rearCoverage"] = quadrantCoverage["rear"];
            data["leftCoverage"] = quadrantCoverage["left"];
            data["rightCoverage"] = quadrantCoverage["right"];
            
            // Event statistics
            if (awarenessEvents.Count > 0)
            {
                data["minDetectionTime"] = awarenessEvents.Min(e => e.detectionTime);
                data["maxDetectionTime"] = awarenessEvents.Max(e => e.detectionTime);
                data["averageDetectionDistance"] = awarenessEvents.Average(e => e.distance);
                data["averageDetectionAngle"] = awarenessEvents.Average(e => e.angle);
            }
            
            return data;
        }
        
        private Dictionary<string, float> CalculateQuadrantCoverage()
        {
            var coverage = new Dictionary<string, float>
            {
                ["front"] = 0,
                ["rear"] = 0,
                ["left"] = 0,
                ["right"] = 0
            };
            
            // Calculate coverage for each quadrant
            for (int i = 0; i < GRID_SIZE; i++)
            {
                for (int j = 0; j < GRID_SIZE; j++)
                {
                    if (spatialCoverageGrid[i, j] > 0.1f)
                    {
                        float angle = (i * 10f) - 180f;
                        
                        if (angle >= -45f && angle <= 45f)
                            coverage["front"]++;
                        else if (angle >= 135f || angle <= -135f)
                            coverage["rear"]++;
                        else if (angle > 45f && angle < 135f)
                            coverage["right"]++;
                        else
                            coverage["left"]++;
                    }
                }
            }
            
            // Convert to percentages
            float sectorsPerQuadrant = (GRID_SIZE * GRID_SIZE) / 4f;
            foreach (var key in coverage.Keys.ToList())
            {
                coverage[key] = (coverage[key] / sectorsPerQuadrant) * 100f;
            }
            
            return coverage;
        }
        
        #endregion
    }
}