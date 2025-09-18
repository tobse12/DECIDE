/*
 * Author: Tobias Sorg
 * Date: 2025-01-15
 * Description: Interface for all stressor implementations in the DECIDE VR framework
 * License: GPLv3
 */

using UnityEngine;

namespace DECIDE.Stressors {
    /// <summary>
    /// Base interface for all stressor implementations
    /// </summary>
    public interface IStressor {
        /// <summary>
        /// Name of the stressor
        /// </summary>
        string Name { get; }
        
        /// <summary>
        /// Current intensity of the stressor (0-1)
        /// </summary>
        float Intensity { get; set; }
        
        /// <summary>
        /// Whether the stressor is currently active
        /// </summary>
        bool IsActive { get; }
        
        /// <summary>
        /// Initializes the stressor with given parameters
        /// </summary>
        /// <param name="parameters">Configuration parameters for the stressor</param>
        void Initialize(StressorParameters parameters);
        
        /// <summary>
        /// Activates the stressor
        /// </summary>
        void Activate();
        
        /// <summary>
        /// Deactivates the stressor
        /// </summary>
        void Deactivate();
        
        /// <summary>
        /// Updates the stressor (called each frame when active)
        /// </summary>
        void UpdateStressor();
        
        /// <summary>
        /// Gets the current configuration parameters
        /// </summary>
        /// <returns>Current stressor parameters</returns>
        StressorParameters GetParameters();
        
        /// <summary>
        /// Updates configuration parameters at runtime
        /// </summary>
        /// <param name="parameters">New configuration parameters</param>
        void UpdateParameters(StressorParameters parameters);
    }
    
    /// <summary>
    /// Base class for stressor configuration parameters
    /// </summary>
    [System.Serializable]
    public class StressorParameters {
        public float intensity = 0.5f;
        public float duration = -1f; // -1 for infinite
        public float fadeInTime = 1f;
        public float fadeOutTime = 1f;
        public bool autoActivate = false;
    }
}