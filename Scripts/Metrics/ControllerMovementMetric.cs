/*
 * Author: Tobias Sorg
 * Date: 2025-01-18
 * Summary: VR controller movement tracking metric for DECIDE VR Framework. Monitors controller
 *          position, velocity, tremor, grip patterns, and trigger usage during scenarios.
 * License: GPLv3
 */

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;

namespace DECIDE.Metrics
{
    /// <summary>
    /// Tracks VR controller movements, interactions, and stability
    /// </summary>
    public class ControllerMovementMetric : BaseMetric
    {
        #region Data Structures
        
        private struct ControllerData
        {
            public Vector3 position;
            public Quaternion rotation;
            public float timestamp;
            public float linearVelocity;
            public float angularVelocity;
            public float triggerValue;
            public float gripValue;
            public bool primaryButton;
            public bool secondaryButton;
            public HandType hand;
        }
        
        private struct InteractionEvent
        {
            public string interactionType;
            public GameObject target;
            public float timestamp;
            public float duration;
            public Vector3 position;
            public HandType hand;
        }
        
        private enum HandType
        {
            Left,
            Right
        }
        
        #endregion
        
        #region Private Fields
        
        // Controller tracking
        private Dictionary<HandType, List<ControllerData>> controllerHistory = new Dictionary<HandType, List<ControllerData>>();
        private Dictionary<HandType, Vector3> lastControllerPosition = new Dictionary<HandType, Vector3>();
        private Dictionary<HandType, Quaternion> lastControllerRotation = new Dictionary<HandType, Quaternion>();
        
        // Interaction tracking
        private List<InteractionEvent> interactionEvents = new List<InteractionEvent>();
        private Dictionary<HandType, float> triggerPressStartTime = new Dictionary<HandType, float>();
        private Dictionary<HandType, float> gripPressStartTime = new Dictionary<HandType, float>();
        
        // Movement metrics
        private Dictionary<HandType, float> totalLinearMovement = new Dictionary<HandType, float>();
        private Dictionary<HandType, float> totalAngularMovement = new Dictionary<HandType, float>();
        private Dictionary<HandType, float> maxLinearVelocity = new Dictionary<HandType, float>();
        private Dictionary<HandType, float> maxAngularVelocity = new Dictionary<HandType, float>();
        
        // Tremor detection
        private Dictionary<HandType, List<float>> tremorMeasurements = new Dictionary<HandType, List<float>>();
        private Dictionary<HandType, float> tremorScore = new Dictionary<HandType, float>();
        private const float TREMOR_FREQUENCY_MIN = 4f; // Hz
        private const float TREMOR_FREQUENCY_MAX = 12f; // Hz
        
        // Aim stability
        private Dictionary<HandType, List<Vector3>> aimPoints = new Dictionary<HandType, List<Vector3>>();
        private Dictionary<HandType, float> aimStabilityScore = new Dictionary<HandType, float>();
        
        // Controller usage patterns
        private float dominantHandUsageRatio;
        private int controllerSwitchCount;
        private HandType? lastActiveHand;
        
        // Gesture detection
        private Dictionary<string, int> detectedGestures = new Dictionary<string, int>();
        private float lastGestureTime;
        
        // Input devices
        private InputDevice leftController;
        private InputDevice rightController;
        
        #endregion
        
        #region Properties
        
        public override string MetricName => "ControllerMovement";
        
        public float OverallTremorScore => 
            tremorScore.Values.Count > 0 ? tremorScore.Values.Average() : 0;
        
        public float OverallAimStability => 
            aimStabilityScore.Values.Count > 0 ? aimStabilityScore.Values.Average() : 100f;
        
        public HandType DominantHand { get; private set; }
        
        #endregion
        
        #region Base Metric Implementation
        
        public override void StartTracking()
        {
            base.StartTracking();
            InitializeControllers();
            ResetMetrics();
        }
        
        public override void StopTracking()
        {
            base.StopTracking();
            CalculateFinalMetrics();
        }
        
        protected override void UpdateMetric()
        {
            if (!isTracking) return;
            
            // Update both controllers
            UpdateController(HandType.Left, leftController);
            UpdateController(HandType.Right, rightController);
            
            // Detect gestures
            DetectGestures();
        }
        
        #endregion
        
        #region Controller Initialization
        
        private void InitializeControllers()
        {
            var devices = new List<InputDevice>();
            
            // Get left controller
            InputDevices.GetDevicesWithCharacteristics(
                InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.Left,
                devices
            );
            if (devices.Count > 0)
                leftController = devices[0];
            
            // Get right controller
            devices.Clear();
            InputDevices.GetDevicesWithCharacteristics(
                InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.Right,
                devices
            );
            if (devices.Count > 0)
                rightController = devices[0];
        }
        
        #endregion
        
        #region Controller Update
        
        private void UpdateController(HandType hand, InputDevice device)
        {
            if (!device.isValid) return;
            
            // Get controller transform
            Vector3 position;
            Quaternion rotation;
            
            if (!device.TryGetFeatureValue(CommonUsages.devicePosition, out position) ||
                !device.TryGetFeatureValue(CommonUsages.deviceRotation, out rotation))
                return;
            
            // Calculate velocities
            float linearVelocity = 0;
            float angularVelocity = 0;
            
            if (lastControllerPosition.ContainsKey(hand))
            {
                linearVelocity = Vector3.Distance(position, lastControllerPosition[hand]) / Time.deltaTime;
                angularVelocity = Quaternion.Angle(rotation, lastControllerRotation[hand]) / Time.deltaTime;
                
                // Update cumulative movement
                totalLinearMovement[hand] += linearVelocity * Time.deltaTime;
                totalAngularMovement[hand] += angularVelocity * Time.deltaTime;
                
                // Update max velocities
                maxLinearVelocity[hand] = Mathf.Max(maxLinearVelocity[hand], linearVelocity);
                maxAngularVelocity[hand] = Mathf.Max(maxAngularVelocity[hand], angularVelocity);
            }
            
            // Get input values
            float triggerValue;
            float gripValue;
            bool primaryButton;
            bool secondaryButton;
            
            device.TryGetFeatureValue(CommonUsages.trigger, out triggerValue);
            device.TryGetFeatureValue(CommonUsages.grip, out gripValue);
            device.TryGetFeatureValue(CommonUsages.primaryButton, out primaryButton);
            device.TryGetFeatureValue(CommonUsages.secondaryButton, out secondaryButton);
            
            // Track trigger interactions
            TrackTriggerInteraction(hand, triggerValue);
            
            // Track grip interactions
            TrackGripInteraction(hand, gripValue);
            
            // Detect tremor
            DetectTremor(hand, position, linearVelocity);
            
            // Track aim stability
            TrackAimStability(hand, rotation);
            
            // Record controller data
            RecordControllerData(hand, position, rotation, linearVelocity, angularVelocity,
                              triggerValue, gripValue, primaryButton, secondaryButton);
            
            // Update active hand tracking
            if (linearVelocity > 0.1f || triggerValue > 0.1f)
            {
                if (lastActiveHand.HasValue && lastActiveHand.Value != hand)
                {
                    controllerSwitchCount++;
                }
                lastActiveHand = hand;
            }
            
            // Store current state
            lastControllerPosition[hand] = position;
            lastControllerRotation[hand] = rotation;
        }
        
        #endregion
        
        #region Interaction Tracking
        
        private void TrackTriggerInteraction(HandType hand, float triggerValue)
        {
            bool isPressed = triggerValue > 0.5f;
            
            if (isPressed && !triggerPressStartTime.ContainsKey(hand))
            {
                // Trigger press started
                triggerPressStartTime[hand] = Time.time;
                LogEvent($"{hand} trigger pressed");
                
                // Check for target
                GameObject target = GetAimTarget(hand);
                RecordInteraction("TriggerPress", hand, target);
            }
            else if (!isPressed && triggerPressStartTime.ContainsKey(hand))
            {
                // Trigger released
                float duration = Time.time - triggerPressStartTime[hand];
                triggerPressStartTime.Remove(hand);
                
                LogEvent($"{hand} trigger released (duration: {duration:F2}s)");
            }
        }
        
        private void TrackGripInteraction(HandType hand, float gripValue)
        {
            bool isPressed = gripValue > 0.5f;
            
            if (isPressed && !gripPressStartTime.ContainsKey(hand))
            {
                gripPressStartTime[hand] = Time.time;
                RecordInteraction("GripPress", hand, null);
            }
            else if (!isPressed && gripPressStartTime.ContainsKey(hand))
            {
                float duration = Time.time - gripPressStartTime[hand];
                gripPressStartTime.Remove(hand);
            }
        }
        
        private GameObject GetAimTarget(HandType hand)
        {
            if (!lastControllerPosition.ContainsKey(hand)) return null;
            
            Vector3 origin = lastControllerPosition[hand];
            Vector3 direction = lastControllerRotation[hand] * Vector3.forward;
            
            RaycastHit hit;
            if (Physics.Raycast(origin, direction, out hit, 100f))
            {
                Avatar avatar = hit.collider.GetComponentInParent<Avatar>();
                return avatar != null ? avatar.gameObject : hit.collider.gameObject;
            }
            
            return null;
        }
        
        private void RecordInteraction(string type, HandType hand, GameObject target)
        {
            interactionEvents.Add(new InteractionEvent
            {
                interactionType = type,
                target = target,
                timestamp = Time.time,
                position = lastControllerPosition.ContainsKey(hand) ? 
                          lastControllerPosition[hand] : Vector3.zero,
                hand = hand
            });
        }
        
        #endregion
        
        #region Tremor Detection
        
        private void DetectTremor(HandType hand, Vector3 position, float velocity)
        {
            if (!tremorMeasurements.ContainsKey(hand))
                tremorMeasurements[hand] = new List<float>();
            
            // Add velocity to tremor measurements
            tremorMeasurements[hand].Add(velocity);
            
            // Keep only recent measurements (last 2 seconds)
            int samplesToKeep = Mathf.RoundToInt(2f / Time.deltaTime);
            if (tremorMeasurements[hand].Count > samplesToKeep)
            {
                tremorMeasurements[hand].RemoveRange(0, 
                    tremorMeasurements[hand].Count - samplesToKeep);
            }
            
            // Analyze tremor frequency
            if (tremorMeasurements[hand].Count >= 30)
            {
                float tremorIntensity = AnalyzeTremorFrequency(tremorMeasurements[hand]);
                tremorScore[hand] = Mathf.Clamp01(tremorIntensity / 10f) * 100f;
            }
        }
        
        private float AnalyzeTremorFrequency(List<float> velocities)
        {
            // Simple frequency analysis using zero-crossing detection
            float mean = velocities.Average();
            int zeroCrossings = 0;
            
            for (int i = 1; i < velocities.Count; i++)
            {
                if ((velocities[i-1] - mean) * (velocities[i] - mean) < 0)
                    zeroCrossings++;
            }
            
            // Estimate frequency
            float duration = velocities.Count * Time.deltaTime;
            float frequency = zeroCrossings / (2f * duration);
            
            // Check if in tremor frequency range
            if (frequency >= TREMOR_FREQUENCY_MIN && frequency <= TREMOR_FREQUENCY_MAX)
            {
                // Calculate tremor intensity based on amplitude
                float amplitude = velocities.Max() - velocities.Min();
                return amplitude * (frequency / TREMOR_FREQUENCY_MAX);
            }
            
            return 0;
        }
        
        #endregion
        
        #region Aim Stability
        
        private void TrackAimStability(HandType hand, Quaternion rotation)
        {
            if (!aimPoints.ContainsKey(hand))
                aimPoints[hand] = new List<Vector3>();
            
            // Calculate aim point
            Vector3 aimDirection = rotation * Vector3.forward;
            Vector3 aimPoint = lastControllerPosition[hand] + aimDirection * 10f;
            
            aimPoints[hand].Add(aimPoint);
            
            // Keep only recent aim points (last second)
            int maxPoints = Mathf.RoundToInt(1f / Time.deltaTime);
            if (aimPoints[hand].Count > maxPoints)
            {
                aimPoints[hand].RemoveRange(0, aimPoints[hand].Count - maxPoints);
            }
            
            // Calculate stability score
            if (aimPoints[hand].Count >= 10)
            {
                Vector3 centroid = CalculateCentroid(aimPoints[hand]);
                float avgDeviation = aimPoints[hand].Average(p => Vector3.Distance(p, centroid));
                
                // Convert to 0-100 score (lower deviation = higher stability)
                aimStabilityScore[hand] = Mathf.Clamp01(1f - (avgDeviation / 0.5f)) * 100f;
            }
        }
        
        private Vector3 CalculateCentroid(List<Vector3> points)
        {
            Vector3 sum = Vector3.zero;
            foreach (var point in points)
                sum += point;
            return sum / points.Count;
        }
        
        #endregion
        
        #region Gesture Detection
        
        private void DetectGestures()
        {
            if (Time.time - lastGestureTime < 0.5f) return; // Gesture cooldown
            
            // Detect pointing gesture
            if (IsPointingGesture())
            {
                RecordGesture("Pointing");
            }
            
            // Detect waving gesture
            if (IsWavingGesture())
            {
                RecordGesture("Waving");
            }
            
            // Detect grabbing gesture
            if (IsGrabbingGesture())
            {
                RecordGesture("Grabbing");
            }
        }
        
        private bool IsPointingGesture()
        {
            // Check if right controller is extended forward with trigger pressed
            if (!controllerHistory.ContainsKey(HandType.Right) || 
                controllerHistory[HandType.Right].Count < 10)
                return false;
            
            var recent = controllerHistory[HandType.Right].TakeLast(10).ToList();
            bool triggerPressed = recent.Average(c => c.triggerValue) > 0.7f;
            bool armExtended = recent.Average(c => c.position.z) > 0.3f;
            
            return triggerPressed && armExtended;
        }
        
        private bool IsWavingGesture()
        {
            // Check for rapid left-right movement
            if (!controllerHistory.ContainsKey(HandType.Right) || 
                controllerHistory[HandType.Right].Count < 30)
                return false;
            
            var recent = controllerHistory[HandType.Right].TakeLast(30).ToList();
            float xVariance = CalculateVariance(recent.Select(c => c.position.x).ToList());
            
            return xVariance > 0.1f;
        }
        
        private bool IsGrabbingGesture()
        {
            // Check if both grip buttons are pressed
            bool leftGrip = false;
            bool rightGrip = false;
            
            if (controllerHistory.ContainsKey(HandType.Left) && 
                controllerHistory[HandType.Left].Count > 0)
            {
                leftGrip = controllerHistory[HandType.Left].Last().gripValue > 0.7f;
            }
            
            if (controllerHistory.ContainsKey(HandType.Right) && 
                controllerHistory[HandType.Right].Count > 0)
            {
                rightGrip = controllerHistory[HandType.Right].Last().gripValue > 0.7f;
            }
            
            return leftGrip && rightGrip;
        }
        
        private void RecordGesture(string gestureName)
        {
            if (!detectedGestures.ContainsKey(gestureName))
                detectedGestures[gestureName] = 0;
            
            detectedGestures[gestureName]++;
            lastGestureTime = Time.time;
            LogEvent($"Gesture detected: {gestureName}");
        }
        
        #endregion
        
        #region Data Recording
        
        private void RecordControllerData(HandType hand, Vector3 position, Quaternion rotation,
                                         float linearVelocity, float angularVelocity,
                                         float triggerValue, float gripValue,
                                         bool primaryButton, bool secondaryButton)
        {
            if (!controllerHistory.ContainsKey(hand))
                controllerHistory[hand] = new List<ControllerData>();
            
            controllerHistory[hand].Add(new ControllerData
            {
                position = position,
                rotation = rotation,
                timestamp = Time.time,
                linearVelocity = linearVelocity,
                angularVelocity = angularVelocity,
                triggerValue = triggerValue,
                gripValue = gripValue,
                primaryButton = primaryButton,
                secondaryButton = secondaryButton,
                hand = hand
            });
            
            // Limit history size
            if (controllerHistory[hand].Count > 10000)
                controllerHistory[hand].RemoveAt(0);
        }
        
        #endregion
        
        #region Final Calculations
        
        private void CalculateFinalMetrics()
        {
            // Calculate dominant hand
            float leftUsage = totalLinearMovement.ContainsKey(HandType.Left) ? 
                totalLinearMovement[HandType.Left] : 0;
            float rightUsage = totalLinearMovement.ContainsKey(HandType.Right) ? 
                totalLinearMovement[HandType.Right] : 0;
            
            DominantHand = rightUsage >= leftUsage ? HandType.Right : HandType.Left;
            dominantHandUsageRatio = (leftUsage + rightUsage) > 0 ? 
                Mathf.Max(leftUsage, rightUsage) / (leftUsage + rightUsage) : 0.5f;
        }
        
        #endregion
        
        #region Data Export
        
        protected override Dictionary<string, object> GetMetricData()
        {
            var data = base.GetMetricData();
            
            // Overall metrics
            data["dominantHand"] = DominantHand.ToString();
            data["dominantHandUsageRatio"] = dominantHandUsageRatio * 100f;
            data["controllerSwitchCount"] = controllerSwitchCount;
            data["overallTremorScore"] = OverallTremorScore;
            data["overallAimStability"] = OverallAimStability;
            
            // Per-hand metrics
            foreach (HandType hand in Enum.GetValues(typeof(HandType)))
            {
                string prefix = hand.ToString().ToLower();
                
                // Movement metrics
                data[$"{prefix}TotalLinearMovement"] = totalLinearMovement.ContainsKey(hand) ? 
                    totalLinearMovement[hand] : 0;
                data[$"{prefix}TotalAngularMovement"] = totalAngularMovement.ContainsKey(hand) ? 
                    totalAngularMovement[hand] : 0;
                data[$"{prefix}MaxLinearVelocity"] = maxLinearVelocity.ContainsKey(hand) ? 
                    maxLinearVelocity[hand] : 0;
                data[$"{prefix}MaxAngularVelocity"] = maxAngularVelocity.ContainsKey(hand) ? 
                    maxAngularVelocity[hand] : 0;
                
                // Stability metrics
                data[$"{prefix}TremorScore"] = tremorScore.ContainsKey(hand) ? 
                    tremorScore[hand] : 0;
                data[$"{prefix}AimStability"] = aimStabilityScore.ContainsKey(hand) ? 
                    aimStabilityScore[hand] : 100f;
                
                // Input statistics
                if (controllerHistory.ContainsKey(hand) && controllerHistory[hand].Count > 0)
                {
                    var history = controllerHistory[hand];
                    data[$"{prefix}AverageTriggerValue"] = history.Average(c => c.triggerValue);
                    data[$"{prefix}AverageGripValue"] = history.Average(c => c.gripValue);
                    data[$"{prefix}TriggerPressCount"] = history.Count(c => c.triggerValue > 0.5f);
                    data[$"{prefix}GripPressCount"] = history.Count(c => c.gripValue > 0.5f);
                }
            }
            
            // Interaction events
            var interactionSummary = interactionEvents
                .GroupBy(e => e.interactionType)
                .ToDictionary(g => g.Key, g => g.Count());
            data["interactionEvents"] = interactionSummary;
            data["totalInteractions"] = interactionEvents.Count;
            
            // Gesture detection
            data["detectedGestures"] = detectedGestures;
            data["totalGesturesDetected"] = detectedGestures.Values.Sum();
            
            // Average interaction response time
            if (interactionEvents.Count > 0)
            {
                var triggerEvents = interactionEvents.Where(e => e.interactionType == "TriggerPress").ToList();
                if (triggerEvents.Count > 1)
                {
                    float avgTimeBetween = 0;
                    for (int i = 1; i < triggerEvents.Count; i++)
                    {
                        avgTimeBetween += triggerEvents[i].timestamp - triggerEvents[i-1].timestamp;
                    }
                    data["averageTimeBetweenTriggers"] = avgTimeBetween / (triggerEvents.Count - 1);
                }
            }
            
            return data;
        }
        
        #endregion
        
        #region Utility Methods
        
        private void ResetMetrics()
        {
            controllerHistory.Clear();
            lastControllerPosition.Clear();
            lastControllerRotation.Clear();
            interactionEvents.Clear();
            triggerPressStartTime.Clear();
            gripPressStartTime.Clear();
            totalLinearMovement.Clear();
            totalAngularMovement.Clear();
            maxLinearVelocity.Clear();
            maxAngularVelocity.Clear();
            tremorMeasurements.Clear();
            tremorScore.Clear();
            aimPoints.Clear();
            aimStabilityScore.Clear();
            detectedGestures.Clear();
            
            foreach (HandType hand in Enum.GetValues(typeof(HandType)))
            {
                totalLinearMovement[hand] = 0;
                totalAngularMovement[hand] = 0;
                maxLinearVelocity[hand] = 0;
                maxAngularVelocity[hand] = 0;
                tremorScore[hand] = 0;
                aimStabilityScore[hand] = 100f;
                controllerHistory[hand] = new List<ControllerData>();
                tremorMeasurements[hand] = new List<float>();
                aimPoints[hand] = new List<Vector3>();
            }
            
            dominantHandUsageRatio = 0.5f;
            controllerSwitchCount = 0;
            lastActiveHand = null;
            lastGestureTime = 0;
        }
        
        #endregion
    }
}