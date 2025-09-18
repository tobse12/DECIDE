/*
 * Author: Tobias Sorg
 * Date: 2025-01-15
 * Description: Object pooling system for avatar management in the DECIDE VR framework
 * License: GPLv3
 */

using System.Collections.Generic;
using UnityEngine;
using DECIDE.Core;

namespace DECIDE.Avatars {
    /// <summary>
    /// Manages avatar pooling for efficient spawning and despawning
    /// </summary>
    public class AvatarPool : MonoBehaviour {
        [Header("Pool Configuration")]
        [SerializeField] private int _poolSizePerType = 10;
        [SerializeField] private bool _expandPoolIfNeeded = true;
        [SerializeField] private int _maxPoolSize = 30;
        
        [Header("Avatar Prefabs")]
        [SerializeField] private GameObject[] _hostilePrefabs;
        [SerializeField] private GameObject[] _friendlyPrefabs;
        [SerializeField] private GameObject[] _unknownPrefabs;
        
        [Header("Avatar Configuration")]
        [SerializeField] private float _defaultMoveSpeed = 2f;
        [SerializeField] private float _defaultRunSpeed = 5f;
        [SerializeField] private bool _enableDecisionConflict = false;
        
        // Pool storage
        private Dictionary<AvatarType, Queue<GameObject>> _availableAvatars;
        private Dictionary<AvatarType, List<GameObject>> _activeAvatars;
        private Dictionary<AvatarType, GameObject[]> _prefabsByType;
        private Transform _poolContainer;
        
        // Statistics
        private int _totalSpawned = 0;
        private int _totalReturned = 0;
        private Dictionary<AvatarType, int> _spawnCountByType;
        
        private void Awake() {
            InitializePools();
        }
        
        /// <summary>
        /// Initializes the object pools
        /// </summary>
        private void InitializePools() {
            // Create container for pooled objects
            _poolContainer = new GameObject("Avatar Pool Container").transform;
            _poolContainer.SetParent(transform);
            
            // Initialize dictionaries
            _availableAvatars = new Dictionary<AvatarType, Queue<GameObject>>();
            _activeAvatars = new Dictionary<AvatarType, List<GameObject>>();
            _spawnCountByType = new Dictionary<AvatarType, int>();
            
            _prefabsByType = new Dictionary<AvatarType, GameObject[]> {
                { AvatarType.Hostile, _hostilePrefabs },
                { AvatarType.Friendly, _friendlyPrefabs },
                { AvatarType.Unknown, _unknownPrefabs }
            };
            
            // Create pools for each avatar type
            foreach (AvatarType type in System.Enum.GetValues(typeof(AvatarType))) {
                _availableAvatars[type] = new Queue<GameObject>();
                _activeAvatars[type] = new List<GameObject>();
                _spawnCountByType[type] = 0;
                
                // Pre-populate pools
                PopulatePool(type, _poolSizePerType);
            }
            
            Debug.Log($"Avatar pools initialized with {_poolSizePerType} avatars per type");
        }
        
        /// <summary>
        /// Populates a pool with avatar instances
        /// </summary>
        private void PopulatePool(AvatarType type, int count) {
            GameObject[] prefabs = _prefabsByType[type];
            if (prefabs == null || prefabs.Length == 0) {
                Debug.LogWarning($"No prefabs configured for avatar type: {type}");
                return;
            }
            
            for (int i = 0; i < count; i++) {
                GameObject prefab = prefabs[Random.Range(0, prefabs.Length)];
                GameObject avatar = CreateAvatar(prefab, type);
                _availableAvatars[type].Enqueue(avatar);
            }
        }
        
        /// <summary>
        /// Creates a new avatar instance
        /// </summary>
        private GameObject CreateAvatar(GameObject prefab, AvatarType type) {
            GameObject avatar = Instantiate(prefab, _poolContainer);
            avatar.name = $"Avatar_{type}_{avatar.GetInstanceID()}";
            
            // Configure avatar controller
            AvatarController controller = avatar.GetComponent<AvatarController>();
            if (controller == null) {
                controller = avatar.AddComponent<AvatarController>();
            }
            
            controller.Initialize(type, Vector3.zero, _enableDecisionConflict);
            
            // Deactivate initially
            avatar.SetActive(false);
            
            return avatar;
        }
        
        /// <summary>
        /// Gets an avatar from the pool
        /// </summary>
        public GameObject GetAvatar(AvatarType type) {
            GameObject avatar = null;
            
            // Check if available in pool
            if (_availableAvatars[type].Count > 0) {
                avatar = _availableAvatars[type].Dequeue();
            } else if (_expandPoolIfNeeded) {
                // Expand pool if needed and allowed
                int currentTotal = _availableAvatars[type].Count + _activeAvatars[type].Count;
                if (currentTotal < _maxPoolSize) {
                    GameObject[] prefabs = _prefabsByType[type];
                    if (prefabs != null && prefabs.Length > 0) {
                        GameObject prefab = prefabs[Random.Range(0, prefabs.Length)];
                        avatar = CreateAvatar(prefab, type);
                        Debug.Log($"Expanded {type} avatar pool. New size: {currentTotal + 1}");
                    }
                } else {
                    Debug.LogWarning($"Cannot expand {type} avatar pool. Max size ({_maxPoolSize}) reached");
                }
            }
            
            if (avatar != null) {
                // Activate and configure avatar
                avatar.SetActive(true);
                _activeAvatars[type].Add(avatar);
                
                // Reset avatar state
                AvatarController controller = avatar.GetComponent<AvatarController>();
                if (controller != null) {
                    controller.Initialize(type, avatar.transform.position, _enableDecisionConflict);
                }
                
                // Update statistics
                _totalSpawned++;
                _spawnCountByType[type]++;
                
                return avatar;
            }
            
            Debug.LogWarning($"No available avatars of type {type}");
            return null;
        }
        
        /// <summary>
        /// Returns an avatar to the pool
        /// </summary>
        public void ReturnAvatar(GameObject avatar) {
            if (avatar == null) return;
            
            AvatarController controller = avatar.GetComponent<AvatarController>();
            if (controller == null) {
                Debug.LogWarning("Attempted to return non-avatar object to pool");
                return;
            }
            
            AvatarType type = controller.Type;
            
            // Remove from active list
            if (_activeAvatars[type].Contains(avatar)) {
                _activeAvatars[type].Remove(avatar);
            }
            
            // Reset and deactivate
            ResetAvatar(avatar);
            avatar.SetActive(false);
            
            // Return to pool
            _availableAvatars[type].Enqueue(avatar);
            
            // Update statistics
            _totalReturned++;
        }
        
        /// <summary>
        /// Resets an avatar to its default state
        /// </summary>
        private void ResetAvatar(GameObject avatar) {
            // Reset position and rotation
            avatar.transform.position = Vector3.zero;
            avatar.transform.rotation = Quaternion.identity;
            
            // Reset any physics
            Rigidbody rb = avatar.GetComponent<Rigidbody>();
            if (rb != null) {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            
            // Reset animation
            Animator animator = avatar.GetComponent<Animator>();
            if (animator != null) {
                animator.Rebind();
                animator.Update(0f);
            }
            
            // Reset AI navigation
            UnityEngine.AI.NavMeshAgent navAgent = avatar.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (navAgent != null && navAgent.enabled) {
                navAgent.ResetPath();
            }
        }
        
        /// <summary>
        /// Returns all active avatars to the pool
        /// </summary>
        public void ReturnAllAvatars() {
            foreach (AvatarType type in System.Enum.GetValues(typeof(AvatarType))) {
                List<GameObject> activeList = new List<GameObject>(_activeAvatars[type]);
                foreach (GameObject avatar in activeList) {
                    ReturnAvatar(avatar);
                }
            }
        }
        
        /// <summary>
        /// Gets the number of available avatars of a specific type
        /// </summary>
        public int GetAvailableCount(AvatarType type) {
            return _availableAvatars[type].Count;
        }
        
        /// <summary>
        /// Gets the number of active avatars of a specific type
        /// </summary>
        public int GetActiveCount(AvatarType type) {
            return _activeAvatars[type].Count;
        }
        
        /// <summary>
        /// Gets total pool size for a specific type
        /// </summary>
        public int GetTotalPoolSize(AvatarType type) {
            return _availableAvatars[type].Count + _activeAvatars[type].Count;
        }
        
        /// <summary>
        /// Gets pool statistics
        /// </summary>
        public PoolStatistics GetStatistics() {
            return new PoolStatistics {
                totalSpawned = _totalSpawned,
                totalReturned = _totalReturned,
                currentActive = GetTotalActiveCount(),
                spawnCountByType = new Dictionary<AvatarType, int>(_spawnCountByType),
                poolSizeByType = GetPoolSizesByType()
            };
        }
        
        /// <summary>
        /// Gets total number of active avatars
        /// </summary>
        private int GetTotalActiveCount() {
            int total = 0;
            foreach (var list in _activeAvatars.Values) {
                total += list.Count;
            }
            return total;
        }
        
        /// <summary>
        /// Gets pool sizes by type
        /// </summary>
        private Dictionary<AvatarType, int> GetPoolSizesByType() {
            var sizes = new Dictionary<AvatarType, int>();
            foreach (AvatarType type in System.Enum.GetValues(typeof(AvatarType))) {
                sizes[type] = GetTotalPoolSize(type);
            }
            return sizes;
        }
        
        /// <summary>
        /// Warms up the pools by pre-instantiating avatars
        /// </summary>
        public void WarmupPools(int additionalPerType) {
            foreach (AvatarType type in System.Enum.GetValues(typeof(AvatarType))) {
                PopulatePool(type, additionalPerType);
            }
            Debug.Log($"Avatar pools warmed up with {additionalPerType} additional avatars per type");
        }
        
        /// <summary>
        /// Clears all pools and destroys avatars
        /// </summary>
        public void ClearPools() {
            // Return all active avatars
            ReturnAllAvatars();
            
            // Destroy all pooled avatars
            foreach (var queue in _availableAvatars.Values) {
                while (queue.Count > 0) {
                    GameObject avatar = queue.Dequeue();
                    if (avatar != null) {
                        Destroy(avatar);
                    }
                }
            }
            
            // Clear collections
            _availableAvatars.Clear();
            _activeAvatars.Clear();
            _spawnCountByType.Clear();
            
            // Reset statistics
            _totalSpawned = 0;
            _totalReturned = 0;
            
            Debug.Log("Avatar pools cleared");
        }
        
        private void OnDestroy() {
            ClearPools();
        }
    }
    
    /// <summary>
    /// Pool statistics container
    /// </summary>
    [System.Serializable]
    public class PoolStatistics {
        public int totalSpawned;
        public int totalReturned;
        public int currentActive;
        public Dictionary<AvatarType, int> spawnCountByType;
        public Dictionary<AvatarType, int> poolSizeByType;
        
        public override string ToString() {
            return $"Pool Stats - Spawned: {totalSpawned}, Returned: {totalReturned}, Active: {currentActive}";
        }
    }
}