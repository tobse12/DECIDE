/*
 * Author: Tobias Sorg
 * Date: 2025-01-18
 * Summary: Eye gaze tracking metric for DECIDE VR Framework. Tracks fixations, saccades, 
 *          scan paths, and object attention patterns during training scenarios.
 * License: GPLv3
 */

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using DECIDE.Avatars; // For AvatarType enum and AvatarController
using DECIDE.Events;   // For event system

namespace DECIDE.Metrics
{
    /// <summary>
    /// Tracks eye gaze patterns, fixations, and saccadic movements
    /// </summary>
    public class GazeTrackingMetric : BaseMetric
    {
        #region Data Structures

        private struct GazeData
        {
            public Vector3 gazeDirection;
            public Vector3 gazeOrigin;
            public Vector3 gazePoint;
            public GameObject targetObject;
            public AvatarType? targetAvatarType;
            public float timestamp;
            public bool isFixation;
            public float fixationDuration;
        }

        private struct FixationEvent
        {
            public GameObject target;
            public float duration;
            public float startTime;
            public Vector3 position;
            public AvatarType? avatarType;
        }

        #endregion

        #region Private Fields

        private List<GazeData> gazeHistory = new List<GazeData>();
        private List<FixationEvent> fixationEvents = new List<FixationEvent>();
        private Dictionary<GameObject, float> objectGazeTimes = new Dictionary<GameObject, float>();
        private Dictionary<AvatarType, float> avatarTypeGazeTimes = new Dictionary<AvatarType, float>();

        private Vector3 lastGazePoint;
        private float currentFixationTime;
        private GameObject currentFixationTarget;
        private float lastSaccadeTime;

        // Fixation detection parameters
        private const float FIXATION_THRESHOLD_ANGLE = 2f; // degrees
        private const float MINIMUM_FIXATION_DURATION = 0.1f; // seconds
        private const float MAX_SACCADE_DURATION = 0.5f; // seconds

        // Saccade metrics
        private int saccadeCount;
        private float totalSaccadeAmplitude;
        private List<float> saccadeVelocities = new List<float>();
        private List<float> saccadeDurations = new List<float>();

        // Attention metrics
        private float timeToFirstAvatarFixation = -1f;
        private int avatarSwitchCount;
        private GameObject lastFixatedAvatar;

        // Pupil metrics (if available)
        private List<float> pupilDiameters = new List<float>();
        private float basePupilDiameter = 3.5f; // mm

        #endregion

        #region Properties

        public override string MetricName => "GazeTracking";

        public float AverageFixationDuration =>
            fixationEvents.Count > 0 ? fixationEvents.Average(f => f.duration) : 0;

        public float ScanPathEfficiency { get; private set; }

        public float VisualAttentionDistribution { get; private set; }

        #endregion

        #region Base Metric Implementation

        public override void StartTracking()
        {
            base.StartTracking();
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

            // Try to get eye tracking data
            InputDevice eyeDevice = InputDevices.GetDeviceAtXRNode(XRNode.Eyes);

            if (eyeDevice.isValid && eyeDevice.TryGetFeatureValue(CommonUsages.eyesData, out Eyes eyesData))
            {
                ProcessEyeTrackingData(eyesData);
            }
            else
            {
                // Fallback to head-based gaze estimation
                ProcessHeadGazeData();
            }
        }

        #endregion

        #region Eye Tracking Processing

        private void ProcessEyeTrackingData(Eyes eyesData)
        {
            Vector3 gazeOrigin = Camera.main.transform.position;
            Vector3 gazeDirection;

            // Calculate combined eye gaze
            if (eyesData.TryGetFixationPoint(out Vector3 fixationPoint))
            {
                gazeDirection = (fixationPoint - gazeOrigin).normalized;
            }
            else
            {
                // Use individual eye data if available
                Vector3 leftEyePos, rightEyePos;
                Quaternion leftEyeRot, rightEyeRot;

                if (eyesData.TryGetLeftEyePosition(out leftEyePos) &&
                    eyesData.TryGetRightEyePosition(out rightEyePos) &&
                    eyesData.TryGetLeftEyeRotation(out leftEyeRot) &&
                    eyesData.TryGetRightEyeRotation(out rightEyeRot))
                {
                    Vector3 leftGaze = leftEyeRot * Vector3.forward;
                    Vector3 rightGaze = rightEyeRot * Vector3.forward;
                    gazeDirection = ((leftGaze + rightGaze) * 0.5f).normalized;
                }
                else
                {
                    gazeDirection = Camera.main.transform.forward;
                }
            }

            // Process pupil diameter if available
            float leftPupil, rightPupil;
            if (eyesData.TryGetLeftEyeOpenAmount(out leftPupil) &&
                eyesData.TryGetRightEyeOpenAmount(out rightPupil))
            {
                float avgPupil = (leftPupil + rightPupil) * 0.5f * 5f; // Scale to mm
                pupilDiameters.Add(avgPupil);
            }

            ProcessGazeData(gazeOrigin, gazeDirection);
        }

        private void ProcessHeadGazeData()
        {
            Vector3 gazeOrigin = Camera.main.transform.position;
            Vector3 gazeDirection = Camera.main.transform.forward;
            ProcessGazeData(gazeOrigin, gazeDirection);
        }

        private void ProcessGazeData(Vector3 origin, Vector3 direction)
        {
            // Perform raycast to find gaze target
            RaycastHit hit;
            GameObject targetObject = null;
            AvatarType? targetAvatarType = null;
            Vector3 gazePoint = origin + direction * 100f;

            if (Physics.Raycast(origin, direction, out hit, 100f))
            {
                targetObject = hit.collider.gameObject;
                gazePoint = hit.point;

                // Check for avatar
                AvatarController avatar = targetObject.GetComponentInParent<AvatarController>();
                if (avatar != null)
                {
                    targetObject = avatar.gameObject;
                    targetAvatarType = avatar.Type;

                    // Track time to first avatar fixation
                    if (timeToFirstAvatarFixation < 0 && currentFixationTime >= MINIMUM_FIXATION_DURATION)
                    {
                        timeToFirstAvatarFixation = Time.time - trackingStartTime;
                    }
                }
            }

            // Calculate fixation/saccade
            DetectFixationAndSaccade(origin, direction, gazePoint, targetObject, targetAvatarType);

            // Update gaze time tracking
            UpdateGazeTimes(targetObject, targetAvatarType);

            // Record gaze data
            RecordGazeData(origin, direction, gazePoint, targetObject, targetAvatarType);
        }

        #endregion

        #region Fixation and Saccade Detection

        private void DetectFixationAndSaccade(Vector3 origin, Vector3 direction, Vector3 gazePoint,
                                             GameObject target, AvatarType? avatarType)
        {
            if (gazeHistory.Count == 0)
            {
                lastGazePoint = gazePoint;
                return;
            }

            float angleChange = Vector3.Angle(gazePoint - origin, lastGazePoint - origin);
            bool isFixation = angleChange < FIXATION_THRESHOLD_ANGLE;

            if (isFixation)
            {
                // Continue or start fixation
                currentFixationTime += Time.deltaTime;

                if (currentFixationTime >= MINIMUM_FIXATION_DURATION && currentFixationTarget == null)
                {
                    currentFixationTarget = target;
                    LogEvent($"Fixation started on {(target ? target.name : "empty space")}");
                }
            }
            else
            {
                // Saccade detected
                if (currentFixationTime >= MINIMUM_FIXATION_DURATION)
                {
                    // Record completed fixation
                    RecordFixation(currentFixationTarget, currentFixationTime, avatarType);

                    // Check for avatar switch
                    if (currentFixationTarget != null && lastFixatedAvatar != null &&
                        currentFixationTarget != lastFixatedAvatar)
                    {
                        AvatarController currentAvatar = currentFixationTarget.GetComponent<AvatarController>();
                        AvatarController lastAvatar = lastFixatedAvatar.GetComponent<AvatarController>();

                        if (currentAvatar != null && lastAvatar != null)
                        {
                            avatarSwitchCount++;
                        }
                    }

                    if (currentFixationTarget != null && currentFixationTarget.GetComponent<AvatarController>() != null)
                    {
                        lastFixatedAvatar = currentFixationTarget;
                    }
                }

                // Record saccade metrics
                RecordSaccade(angleChange);

                // Reset fixation tracking
                currentFixationTime = 0;
                currentFixationTarget = null;
            }

            lastGazePoint = gazePoint;
        }

        private void RecordFixation(GameObject target, float duration, AvatarType? avatarType)
        {
            fixationEvents.Add(new FixationEvent
            {
                target = target,
                duration = duration,
                startTime = Time.time - duration,
                position = lastGazePoint,
                avatarType = avatarType
            });

            LogEvent($"Fixation completed: {(target ? target.name : "empty")} for {duration:F2}s");
        }

        private void RecordSaccade(float amplitude)
        {
            float saccadeDuration = Time.time - lastSaccadeTime;

            if (saccadeDuration < MAX_SACCADE_DURATION)
            {
                saccadeCount++;
                totalSaccadeAmplitude += amplitude;

                float velocity = amplitude / Time.deltaTime;
                saccadeVelocities.Add(velocity);
                saccadeDurations.Add(saccadeDuration);
            }

            lastSaccadeTime = Time.time;
        }

        #endregion

        #region Tracking Updates

        private void UpdateGazeTimes(GameObject target, AvatarType? avatarType)
        {
            if (target != null)
            {
                if (!objectGazeTimes.ContainsKey(target))
                    objectGazeTimes[target] = 0;
                objectGazeTimes[target] += Time.deltaTime;
            }

            if (avatarType.HasValue)
            {
                if (!avatarTypeGazeTimes.ContainsKey(avatarType.Value))
                    avatarTypeGazeTimes[avatarType.Value] = 0;
                avatarTypeGazeTimes[avatarType.Value] += Time.deltaTime;
            }
        }

        private void RecordGazeData(Vector3 origin, Vector3 direction, Vector3 point,
                                   GameObject target, AvatarType? avatarType)
        {
            gazeHistory.Add(new GazeData
            {
                gazeDirection = direction,
                gazeOrigin = origin,
                gazePoint = point,
                targetObject = target,
                targetAvatarType = avatarType,
                timestamp = Time.time,
                isFixation = currentFixationTime >= MINIMUM_FIXATION_DURATION,
                fixationDuration = currentFixationTime
            });

            // Limit history size
            if (gazeHistory.Count > 10000)
                gazeHistory.RemoveAt(0);
        }

        #endregion

        #region Metric Calculations

        private void CalculateFinalMetrics()
        {
            CalculateScanPathEfficiency();
            CalculateVisualAttentionDistribution();
        }

        private void CalculateScanPathEfficiency()
        {
            if (gazeHistory.Count < 2) return;

            // Calculate actual scan path length
            float actualPathLength = 0;
            for (int i = 1; i < gazeHistory.Count; i++)
            {
                actualPathLength += Vector3.Distance(gazeHistory[i].gazePoint, gazeHistory[i - 1].gazePoint);
            }

            // Calculate optimal path length (direct distance)
            float optimalPathLength = Vector3.Distance(
                gazeHistory.First().gazePoint,
                gazeHistory.Last().gazePoint
            );

            // Efficiency ratio (lower is more efficient)
            ScanPathEfficiency = optimalPathLength > 0 ? actualPathLength / optimalPathLength : 1f;
        }

        private void CalculateVisualAttentionDistribution()
        {
            if (avatarTypeGazeTimes.Count == 0) return;

            float totalGazeTime = avatarTypeGazeTimes.Values.Sum();
            if (totalGazeTime == 0) return;

            // Calculate entropy of attention distribution
            float entropy = 0;
            foreach (var kvp in avatarTypeGazeTimes)
            {
                float probability = kvp.Value / totalGazeTime;
                if (probability > 0)
                    entropy -= probability * Mathf.Log(probability, 2);
            }

            // Normalize to 0-1 range
            float maxEntropy = Mathf.Log(avatarTypeGazeTimes.Count, 2);
            VisualAttentionDistribution = maxEntropy > 0 ? entropy / maxEntropy : 0;
        }

        #endregion

        #region Data Export

        public override Dictionary<string, object> GetMetricData()
        {
            var data = base.GetMetricData();

            // Fixation metrics
            data["totalFixations"] = fixationEvents.Count;
            data["averageFixationDuration"] = AverageFixationDuration;
            data["longestFixation"] = fixationEvents.Count > 0 ?
                fixationEvents.Max(f => f.duration) : 0;

            // Saccade metrics
            data["saccadeCount"] = saccadeCount;
            data["averageSaccadeAmplitude"] = saccadeCount > 0 ?
                totalSaccadeAmplitude / saccadeCount : 0;
            data["averageSaccadeVelocity"] = saccadeVelocities.Count > 0 ?
                saccadeVelocities.Average() : 0;
            data["peakSaccadeVelocity"] = saccadeVelocities.Count > 0 ?
                saccadeVelocities.Max() : 0;

            // Scan path metrics
            data["scanPathLength"] = CalculateTotalScanPath();
            data["scanPathEfficiency"] = ScanPathEfficiency;

            // Attention metrics
            data["visualAttentionDistribution"] = VisualAttentionDistribution;
            data["avatarSwitchCount"] = avatarSwitchCount;
            data["timeToFirstAvatarFixation"] = timeToFirstAvatarFixation;

            // Avatar type gaze distribution
            var avatarGazePercentages = new Dictionary<string, float>();
            float totalAvatarGaze = avatarTypeGazeTimes.Values.Sum();
            foreach (var kvp in avatarTypeGazeTimes)
            {
                avatarGazePercentages[kvp.Key.ToString()] =
                    totalAvatarGaze > 0 ? (kvp.Value / totalAvatarGaze) * 100 : 0;
            }
            data["avatarGazeDistribution"] = avatarGazePercentages;

            // Pupil metrics (if available)
            if (pupilDiameters.Count > 0)
            {
                data["averagePupilDiameter"] = pupilDiameters.Average();
                data["pupilDiameterVariance"] = CalculateVariance(pupilDiameters);
                data["maxPupilDilation"] = pupilDiameters.Max() - basePupilDiameter;
            }

            // Top gazed objects
            var topGazed = objectGazeTimes
                .OrderByDescending(kvp => kvp.Value)
                .Take(5)
                .ToDictionary(kvp => kvp.Key.name, kvp => kvp.Value);
            data["topGazedObjects"] = topGazed;

            return data;
        }

        private float CalculateTotalScanPath()
        {
            float total = 0;
            for (int i = 1; i < gazeHistory.Count; i++)
            {
                total += Vector3.Distance(gazeHistory[i].gazePoint, gazeHistory[i - 1].gazePoint);
            }
            return total;
        }

        private float CalculateVariance(List<float> values)
        {
            if (values.Count == 0) return 0;
            float mean = values.Average();
            return values.Average(v => Mathf.Pow(v - mean, 2));
        }

        #endregion

        #region Utility Methods

        private void ResetMetrics()
        {
            gazeHistory.Clear();
            fixationEvents.Clear();
            objectGazeTimes.Clear();
            avatarTypeGazeTimes.Clear();
            saccadeVelocities.Clear();
            saccadeDurations.Clear();
            pupilDiameters.Clear();

            saccadeCount = 0;
            totalSaccadeAmplitude = 0;
            currentFixationTime = 0;
            currentFixationTarget = null;
            lastFixatedAvatar = null;
            avatarSwitchCount = 0;
            timeToFirstAvatarFixation = -1f;
            ScanPathEfficiency = 0;
            VisualAttentionDistribution = 0;
        }

        #endregion
    }
}