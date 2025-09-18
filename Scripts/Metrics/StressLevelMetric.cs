/*
 * Author: Tobias Sorg
 * Date: 2025-01-18
 * Summary: Composite stress level calculation metric for DECIDE VR Framework. Combines multiple
 *          physiological and behavioral indicators to compute overall stress level.
 * License: GPLv3
 */

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DECIDE.Metrics
{
    /// <summary>
    /// Calculates overall stress level from multiple indicators
    /// </summary>
    public class StressLevelMetric : BaseMetric
    {
        #region Data Structures
        
        private struct StressIndicator
        {
            public string name;
            public float value;
            public float weight;
            public float timestamp;
        }
        
        private struct StressEvent
        {
            public string eventType;
            public float stressImpact;
            public float timestamp;
            public float duration;
        }
        
        #endregion
        
        #region Private Fields
        
        // Stress tracking
        private List<float> stressHistory = new List<float>();
        private List<StressIndicator> indicators = new List<StressIndicator>();
        private List<StressEvent> stressEvents = new List<StressEvent>();
        
        // Current stress values
        private float currentStressLevel = 0f;
        private float baselineStressLevel = 20f;
        private float peakStressLevel = 0f;
        
        // Stress components with weights
        private Dictionary<string, float> stressComponents = new Dictionary<string, float>
        {
            ["heartRateVariability"] = 0.2f,
            ["movementJitter"] = 0.15f,
            ["reactionTimeDelay"] = 0.15f,
            ["missedTargets"] = 0.1f,
            ["rapidHeadMovement"] = 0.1f,
            ["triggerPressure"] = 0.1f,
            ["environmentalStressors"] = 0.2f
        };
        
        // Physiological simulation
        private float simulatedHeartRate = 70f;
        private float heartRateVariability = 5f;
        private float skinConductance = 1.0f;
        private float pupilDilation = 3.5f; // mm
        
        // Behavioral indicators
        private int rapidMovementCount = 0;
        private int errorCount = 0;
        private float averageReactionTime = 0f;
        private float reactionTimeVariability = 0f;
        
        // Stress accumulation
        private float stressAccumulation = 0f;
        private float stressDecayRate = 0.1f; // per second
        private float stressGrowthRate = 0.5f;
        
        // References to other metrics
        private HeadMovementMetric headMovementMetric;
        private ControllerMovementMetric controllerMetric;
        private GazeTrackingMetric gazeMetric;
        
        // Thresholds
        private const float LOW_STRESS_THRESHOLD = 30f;
        private const float MEDIUM_STRESS_THRESHOLD = 60f;
        private const float HIGH_STRESS_THRESHOLD = 80f;
        
        #endregion
        
        #region Properties
        
        public override string MetricName => "StressLevel";
        
        public float CurrentStressLevel => currentStressLevel;
        
        public StressCategory CurrentStressCategory => GetStressCategory(currentStressLevel);
        
        public float AverageStressLevel => stressHistory.Count > 0 ? stressHistory.Average() : 0;
        
        public enum StressCategory
        {
            Minimal,
            Low,
            Moderate,
            High,
            Extreme
        }
        
        #endregion
        
        #region Base Metric Implementation
        
        protected override void OnStartTracking()
        {
            base.OnStartTracking();
            FindMetricReferences();
            InitializeBaseline();
        }
        
        protected override void OnUpdateMetric()
        {
            CalculateStressLevel();
            UpdatePhysiologicalSimulation();
            DetectStressEvents();
            RecordStressData();
        }
        
        #endregion
        
        #region Stress Calculation
        
        private void CalculateStressLevel()
        {
            float totalStress = baselineStressLevel;
            
            // Calculate stress from each component
            totalStress += CalculatePhysiologicalStress();
            totalStress += CalculateBehavioralStress();
            totalStress += CalculateEnvironmentalStress();
            totalStress += CalculatePerformanceStress();
            
            // Apply stress accumulation/decay
            ApplyStressDynamics(ref totalStress);
            
            // Clamp to valid range
            currentStressLevel = Mathf.Clamp(totalStress, 0f, 100f);
            
            // Update peak stress
            if (currentStressLevel > peakStressLevel)
            {
                peakStressLevel = currentStressLevel;
                LogEvent($"New peak stress level: {peakStressLevel:F1}");
            }
            
            // Record to history
            stressHistory.Add(currentStressLevel);
            
            // Limit history size
            if (stressHistory.Count > 10000)
                stressHistory.RemoveAt(0);
        }
        
        private float CalculatePhysiologicalStress()
        {
            float stress = 0f;
            
            // Heart rate component (normalized to 0-100)
            float hrStress = Mathf.Clamp01((simulatedHeartRate - 60f) / 100f) * 30f;
            stress += hrStress * GetComponentWeight("heartRateVariability");
            
            // Heart rate variability (lower HRV = higher stress)
            float hrvStress = Mathf.Clamp01(1f - (heartRateVariability / 20f)) * 20f;
            stress += hrvStress * GetComponentWeight("heartRateVariability");
            
            // Skin conductance (higher = more stress)
            float scStress = Mathf.Clamp01((skinConductance - 1f) / 2f) * 15f;
            stress += scStress;
            
            // Pupil dilation (larger = more stress/arousal)
            float pupilStress = Mathf.Clamp01((pupilDilation - 3f) / 3f) * 10f;
            stress += pupilStress;
            
            RecordIndicator("Physiological", stress, 0.3f);
            
            return stress;
        }
        
        private float CalculateBehavioralStress()
        {
            float stress = 0f;
            
            // Movement jitter from controllers
            if (controllerMetric != null)
            {
                float jitterStress = controllerMetric.OverallTremorScore * 0.2f;
                stress += jitterStress * GetComponentWeight("movementJitter");
            }
            
            // Rapid head movements
            if (headMovementMetric != null)
            {
                float headStress = Mathf.Clamp01(rapidMovementCount / 10f) * 15f;
                stress += headStress * GetComponentWeight("rapidHeadMovement");
            }
            
            // Reaction time delays
            if (averageReactionTime > 0)
            {
                float rtStress = Mathf.Clamp01((averageReactionTime - 1f) / 3f) * 20f;
                stress += rtStress * GetComponentWeight("reactionTimeDelay");
            }
            
            // Error rate
            float errorStress = Mathf.Clamp01(errorCount / 5f) * 15f;
            stress += errorStress * GetComponentWeight("missedTargets");
            
            RecordIndicator("Behavioral", stress, 0.25f);
            
            return stress;
        }
        
        private float CalculateEnvironmentalStress()
        {
            float stress = 0f;
            
            // Get active stressor count and intensity
            StressorManager stressorManager = FindObjectOfType<StressorManager>();
            if (stressorManager != null)
            {
                var activeStressors = stressorManager.GetActiveStressors();
                float stressorIntensity = 0f;
                
                foreach (var stressor in activeStressors)
                {
                    stressorIntensity += stressor.CurrentIntensity;
                }
                
                stress = Mathf.Clamp(stressorIntensity * 10f, 0f, 40f);
            }
            
            RecordIndicator("Environmental", stress, 0.25f);
            
            return stress * GetComponentWeight("environmentalStressors");
        }
        
        private float CalculatePerformanceStress()
        {
            float stress = 0f;
            
            // Classification accuracy (lower accuracy = higher stress)
            ClassificationMetric classMetric = FindObjectOfType<ClassificationMetric>();
            if (classMetric != null)
            {
                float accuracy = classMetric.GetAccuracy();
                float accuracyStress = Mathf.Clamp01(1f - accuracy) * 20f;
                stress += accuracyStress;
            }
            
            // Time pressure
            float timePressure = CalculateTimePressure();
            stress += timePressure * 10f;
            
            RecordIndicator("Performance", stress, 0.2f);
            
            return stress;
        }
        
        #endregion
        
        #region Stress Dynamics
        
        private void ApplyStressDynamics(ref float stress)
        {
            // Stress accumulation over time
            if (stress > currentStressLevel)
            {
                // Stress is increasing
                stressAccumulation += (stress - currentStressLevel) * stressGrowthRate * Time.deltaTime;
            }
            else
            {
                // Stress is decreasing - apply decay
                stressAccumulation -= stressDecayRate * Time.deltaTime;
            }
            
            // Clamp accumulation
            stressAccumulation = Mathf.Clamp(stressAccumulation, -10f, 30f);
            
            // Apply accumulation to stress
            stress += stressAccumulation;
            
            // Apply smoothing for more realistic changes
            stress = Mathf.Lerp(currentStressLevel, stress, Time.deltaTime * 2f);
        }
        
        private float CalculateTimePressure()
        {
            // Simple time pressure based on scenario duration
            float elapsedTime = Time.time - trackingStartTime;
            float pressurePhase = elapsedTime / 300f; // 5 minute phases
            
            // Increase pressure over time
            return Mathf.Clamp01(pressurePhase);
        }
        
        #endregion
        
        #region Physiological Simulation
        
        private void UpdatePhysiologicalSimulation()
        {
            // Simulate heart rate based on stress
            float targetHR = 60f + (currentStressLevel * 0.8f);
            simulatedHeartRate = Mathf.Lerp(simulatedHeartRate, targetHR, Time.deltaTime * 0.5f);
            
            // Add natural variability
            simulatedHeartRate += UnityEngine.Random.Range(-2f, 2f);
            simulatedHeartRate = Mathf.Clamp(simulatedHeartRate, 50f, 180f);
            
            // Simulate HRV (inversely related to stress)
            heartRateVariability = Mathf.Lerp(heartRateVariability, 
                20f - (currentStressLevel * 0.15f), Time.deltaTime);
            
            // Simulate skin conductance
            float targetSC = 1f + (currentStressLevel * 0.02f);
            skinConductance = Mathf.Lerp(skinConductance, targetSC, Time.deltaTime * 0.3f);
            
            // Simulate pupil dilation
            float targetPupil = 3.5f + (currentStressLevel * 0.02f);
            pupilDilation = Mathf.Lerp(pupilDilation, targetPupil, Time.deltaTime * 0.4f);
        }
        
        #endregion
        
        #region Event Detection
        
        private void DetectStressEvents()
        {
            // Detect rapid stress increase
            if (stressHistory.Count >= 10)
            {
                float recentAvg = stressHistory.TakeLast(10).Average();
                float previousAvg = stressHistory.TakeLast(20).Take(10).Average();
                
                if (recentAvg - previousAvg > 15f)
                {
                    RecordStressEvent("RapidIncrease", recentAvg - previousAvg);
                }
            }
            
            // Detect sustained high stress
            if (currentStressLevel > HIGH_STRESS_THRESHOLD)
            {
                if (stressHistory.Count(s => s > HIGH_STRESS_THRESHOLD) > 30)
                {
                    RecordStressEvent("SustainedHighStress", currentStressLevel);
                }
            }
            
            // Detect stress recovery
            if (stressHistory.Count >= 30)
            {
                float recent = stressHistory.TakeLast(10).Average();
                float earlier = stressHistory.TakeLast(30).Take(10).Average();
                
                if (earlier - recent > 20f)
                {
                    RecordStressEvent("StressRecovery", earlier - recent);
                }
            }
        }
        
        private void RecordStressEvent(string eventType, float impact)
        {
            stressEvents.Add(new StressEvent
            {
                eventType = eventType,
                stressImpact = impact,
                timestamp = Time.time,
                duration = 0
            });
            
            LogEvent($"Stress event: {eventType} (impact: {impact:F1})");
        }
        
        #endregion
        
        #region Data Recording
        
        private void RecordStressData()
        {
            var data = new Dictionary<string, object>
            {
                ["stressLevel"] = currentStressLevel,
                ["category"] = CurrentStressCategory.ToString(),
                ["heartRate"] = simulatedHeartRate,
                ["hrv"] = heartRateVariability,
                ["skinConductance"] = skinConductance,
                ["pupilDilation"] = pupilDilation,
                ["accumulation"] = stressAccumulation
            };
            
            RecordDataPoint(data);
        }
        
        private void RecordIndicator(string name, float value, float weight)
        {
            indicators.Add(new StressIndicator
            {
                name = name,
                value = value,
                weight = weight,
                timestamp = Time.time
            });
        }
        
        #endregion
        
        #region Helper Methods
        
        private void FindMetricReferences()
        {
            headMovementMetric = FindObjectOfType<HeadMovementMetric>();
            controllerMetric = FindObjectOfType<ControllerMovementMetric>();
            gazeMetric = FindObjectOfType<GazeTrackingMetric>();
        }
        
        private void InitializeBaseline()
        {
            // Set baseline based on initial conditions
            currentStressLevel = baselineStressLevel;
            simulatedHeartRate = 70f + UnityEngine.Random.Range(-5f, 5f);
            heartRateVariability = 15f + UnityEngine.Random.Range(-3f, 3f);
            skinConductance = 1f;
            pupilDilation = 3.5f;
        }
        
        private float GetComponentWeight(string component)
        {
            return stressComponents.ContainsKey(component) ? stressComponents[component] : 1f;
        }
        
        private StressCategory GetStressCategory(float stressLevel)
        {
            if (stressLevel < 20f) return StressCategory.Minimal;
            if (stressLevel < 40f) return StressCategory.Low;
            if (stressLevel < 60f) return StressCategory.Moderate;
            if (stressLevel < 80f) return StressCategory.High;
            return StressCategory.Extreme;
        }
        
        #endregion
        
        #region Public Methods
        
        public void IncrementErrorCount()
        {
            errorCount++;
        }
        
        public void UpdateReactionTime(float reactionTime)
        {
            // Update rolling average
            averageReactionTime = (averageReactionTime * 0.9f) + (reactionTime * 0.1f);
        }
        
        public void TriggerStressResponse(float intensity, string source)
        {
            float impact = intensity * 20f;
            stressAccumulation += impact;
            
            RecordStressEvent($"Trigger_{source}", impact);
            LogEvent($"Stress response triggered from {source}: +{impact:F1}");
        }
        
        #endregion
        
        #region Data Export
        
        protected override Dictionary<string, object> GetMetricData()
        {
            var data = base.GetMetricData();
            
            // Current values
            data["currentStressLevel"] = currentStressLevel;
            data["stressCategory"] = CurrentStressCategory.ToString();
            data["peakStressLevel"] = peakStressLevel;
            data["averageStressLevel"] = AverageStressLevel;
            data["baselineStressLevel"] = baselineStressLevel;
            
            // Physiological data
            data["heartRate"] = simulatedHeartRate;
            data["heartRateVariability"] = heartRateVariability;
            data["skinConductance"] = skinConductance;
            data["pupilDilation"] = pupilDilation;
            
            // Stress distribution
            if (stressHistory.Count > 0)
            {
                data["minStress"] = stressHistory.Min();
                data["maxStress"] = stressHistory.Max();
                data["stressStdDev"] = CalculateStandardDeviation(stressHistory);
                
                // Time spent in each category
                var categoryDistribution = new Dictionary<string, float>();
                foreach (StressCategory category in Enum.GetValues(typeof(StressCategory)))
                {
                    int count = stressHistory.Count(s => GetStressCategory(s) == category);
                    categoryDistribution[category.ToString()] = 
                        (float)count / stressHistory.Count * 100f;
                }
                data["stressCategoryDistribution"] = categoryDistribution;
            }
            
            // Stress events
            var eventSummary = stressEvents
                .GroupBy(e => e.eventType)
                .ToDictionary(g => g.Key, g => g.Count());
            data["stressEvents"] = eventSummary;
            data["totalStressEvents"] = stressEvents.Count;
            
            // Component contributions
            var componentContributions = new Dictionary<string, float>();
            foreach (var component in stressComponents)
            {
                componentContributions[component.Key] = component.Value * 100f;
            }
            data["componentWeights"] = componentContributions;
            
            return data;
        }
        
        #endregion
    }
}