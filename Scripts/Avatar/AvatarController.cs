/*
 * Author: Tobias Sorg
 * Date: 2025-01-18
 * Summary: Avatar controller for DECIDE VR Framework. Manages avatar behavior, appearance,
 *          movement patterns, and classification properties for military training scenarios.
 * License: GPLv3
 */

using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace DECIDE.Avatars
{
    /// <summary>
    /// Avatar types for classification
    /// </summary>
    public enum AvatarType
    {
        Hostile,
        Friendly,
        Unknown
    }

    /// <summary>
    /// Avatar behavior states
    /// </summary>
    public enum AvatarBehaviorState
    {
        Idle,
        Patrol,
        Alert,
        Engaged,
        Fleeing,
        Hiding
    }

    /// <summary>
    /// Controls individual avatar behavior, movement, and appearance
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class AvatarController : MonoBehaviour
    {
        #region Properties

        public AvatarType Type => avatarType;
        public int AvatarID => avatarID;
        public float SpawnTime => spawnTime;
        public bool HasBeenTargeted => hasBeenTargeted;
        public bool HasBeenClassified => hasBeenClassified;
        public AvatarBehaviorState CurrentState => currentState;

        #endregion

        #region Configuration Fields

        [Header("Avatar Identity")]
        [SerializeField] private AvatarType avatarType = AvatarType.Unknown;
        [SerializeField] private int avatarID;
        [SerializeField] private string avatarName = "Avatar";

        [Header("Appearance")]
        [SerializeField] private Material hostileMaterial;
        [SerializeField] private Material friendlyMaterial;
        [SerializeField] private Material unknownMaterial;
        [SerializeField] private GameObject weaponObject;
        [SerializeField] private GameObject[] accessoryObjects;

        [Header("Movement")]
        [SerializeField] private float walkSpeed = 2f;
        [SerializeField] private float runSpeed = 5f;
        [SerializeField] private float rotationSpeed = 120f;
        [SerializeField] private float stoppingDistance = 0.5f;

        [Header("Behavior")]
        [SerializeField] private AvatarBehaviorState initialState = AvatarBehaviorState.Idle;
        [SerializeField] private float decisionInterval = 5f;
        [SerializeField] private float alertRadius = 10f;
        [SerializeField] private float engagementRange = 5f;

        [Header("Patrol Settings")]
        [SerializeField] private Transform[] patrolPoints;
        [SerializeField] private bool randomPatrol = true;
        [SerializeField] private float waitTimeAtPoint = 2f;

        #endregion

        #region Private Fields

        private NavMeshAgent navAgent;
        private Animator animator;
        private Renderer mainRenderer;
        private AvatarBehaviorState currentState;

        private float spawnTime;
        private bool hasBeenTargeted;
        private bool hasBeenClassified;
        private bool canChangeAppearance;

        private int currentPatrolIndex;
        private float waitTimer;
        private float decisionTimer;

        private Transform playerTransform;
        private Vector3 lastKnownPlayerPosition;

        #endregion

        #region Unity Lifecycle

        void Awake()
        {
            navAgent = GetComponent<NavMeshAgent>();
            animator = GetComponent<Animator>();
            mainRenderer = GetComponentInChildren<Renderer>();

            if (navAgent != null)
            {
                navAgent.speed = walkSpeed;
                navAgent.stoppingDistance = stoppingDistance;
            }
        }

        void Start()
        {
            spawnTime = Time.time;
            currentState = initialState;

            // Find player
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
            }

            // Apply initial appearance
            UpdateAppearance();

            // Start behavior
            StartCoroutine(BehaviorLoop());
        }

        void Update()
        {
            // Update animator
            if (animator != null && navAgent != null)
            {
                float speed = navAgent.velocity.magnitude;
                animator.SetFloat("Speed", speed);
                animator.SetBool("IsMoving", speed > 0.1f);
            }

            // Check player proximity
            CheckPlayerProximity();
        }

        #endregion

        #region Behavior System

        private IEnumerator BehaviorLoop()
        {
            while (enabled)
            {
                yield return new WaitForSeconds(decisionInterval);

                // Make behavior decision based on current state
                switch (currentState)
                {
                    case AvatarBehaviorState.Idle:
                        DecideIdleBehavior();
                        break;
                    case AvatarBehaviorState.Patrol:
                        UpdatePatrolBehavior();
                        break;
                    case AvatarBehaviorState.Alert:
                        UpdateAlertBehavior();
                        break;
                    case AvatarBehaviorState.Engaged:
                        UpdateEngagedBehavior();
                        break;
                    case AvatarBehaviorState.Fleeing:
                        UpdateFleeBehavior();
                        break;
                    case AvatarBehaviorState.Hiding:
                        UpdateHideBehavior();
                        break;
                }
            }
        }

        private void DecideIdleBehavior()
        {
            // Randomly decide next action
            float random = Random.Range(0f, 1f);

            if (random < 0.3f && patrolPoints != null && patrolPoints.Length > 0)
            {
                // Start patrolling
                ChangeState(AvatarBehaviorState.Patrol);
                SetNextPatrolPoint();
            }
            else if (random < 0.5f)
            {
                // Random movement
                Vector3 randomDirection = Random.insideUnitSphere * 10f;
                randomDirection += transform.position;
                NavMeshHit hit;
                if (NavMesh.SamplePosition(randomDirection, out hit, 10f, NavMesh.AllAreas))
                {
                    navAgent.SetDestination(hit.position);
                }
            }
        }

        private void UpdatePatrolBehavior()
        {
            if (patrolPoints == null || patrolPoints.Length == 0)
            {
                ChangeState(AvatarBehaviorState.Idle);
                return;
            }

            // Check if reached patrol point
            if (!navAgent.pathPending && navAgent.remainingDistance < 0.5f)
            {
                waitTimer += Time.deltaTime;
                if (waitTimer >= waitTimeAtPoint)
                {
                    SetNextPatrolPoint();
                    waitTimer = 0;
                }
            }
        }

        private void UpdateAlertBehavior()
        {
            // Look around for threats
            transform.Rotate(0, rotationSpeed * Time.deltaTime, 0);

            // Check if should engage or return to patrol
            if (playerTransform != null)
            {
                float distance = Vector3.Distance(transform.position, playerTransform.position);
                if (distance < engagementRange && avatarType == AvatarType.Hostile)
                {
                    ChangeState(AvatarBehaviorState.Engaged);
                }
            }
        }

        private void UpdateEngagedBehavior()
        {
            if (playerTransform == null) return;

            // Face player
            Vector3 direction = (playerTransform.position - transform.position).normalized;
            direction.y = 0;
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(direction), Time.deltaTime * rotationSpeed / 30f);

            // Move towards or away based on type
            if (avatarType == AvatarType.Hostile)
            {
                navAgent.SetDestination(playerTransform.position);
            }
            else if (avatarType == AvatarType.Friendly)
            {
                // Keep distance
                Vector3 awayFromPlayer = transform.position - playerTransform.position;
                navAgent.SetDestination(transform.position + awayFromPlayer.normalized * 5f);
            }
        }

        private void UpdateFleeBehavior()
        {
            if (playerTransform == null)
            {
                ChangeState(AvatarBehaviorState.Idle);
                return;
            }

            // Move away from player
            Vector3 fleeDirection = (transform.position - playerTransform.position).normalized;
            Vector3 fleePosition = transform.position + fleeDirection * 10f;

            NavMeshHit hit;
            if (NavMesh.SamplePosition(fleePosition, out hit, 10f, NavMesh.AllAreas))
            {
                navAgent.SetDestination(hit.position);
                navAgent.speed = runSpeed;
            }
        }

        private void UpdateHideBehavior()
        {
            // Find cover or hiding spot
            Collider[] covers = Physics.OverlapSphere(transform.position, 15f);
            foreach (var cover in covers)
            {
                if (cover.CompareTag("Cover"))
                {
                    navAgent.SetDestination(cover.transform.position);
                    break;
                }
            }
        }

        #endregion

        #region State Management

        private void ChangeState(AvatarBehaviorState newState)
        {
            currentState = newState;

            // Update speed based on state
            switch (newState)
            {
                case AvatarBehaviorState.Patrol:
                case AvatarBehaviorState.Idle:
                    navAgent.speed = walkSpeed;
                    break;
                case AvatarBehaviorState.Engaged:
                case AvatarBehaviorState.Fleeing:
                    navAgent.speed = runSpeed;
                    break;
            }

            // Update animator
            if (animator != null)
            {
                animator.SetTrigger($"State_{newState}");
            }
        }

        private void CheckPlayerProximity()
        {
            if (playerTransform == null) return;

            float distance = Vector3.Distance(transform.position, playerTransform.position);

            // Check if player is in alert radius
            if (distance < alertRadius && currentState == AvatarBehaviorState.Patrol)
            {
                ChangeState(AvatarBehaviorState.Alert);
                lastKnownPlayerPosition = playerTransform.position;
            }

            // Check if should flee (for friendly avatars)
            if (distance < engagementRange && avatarType == AvatarType.Friendly &&
                currentState != AvatarBehaviorState.Fleeing)
            {
                ChangeState(AvatarBehaviorState.Fleeing);
            }
        }

        #endregion

        #region Patrol System

        private void SetNextPatrolPoint()
        {
            if (patrolPoints == null || patrolPoints.Length == 0) return;

            if (randomPatrol)
            {
                currentPatrolIndex = Random.Range(0, patrolPoints.Length);
            }
            else
            {
                currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
            }

            if (patrolPoints[currentPatrolIndex] != null)
            {
                navAgent.SetDestination(patrolPoints[currentPatrolIndex].position);
            }
        }

        #endregion

        #region Appearance Management

        public void UpdateAppearance()
        {
            if (mainRenderer == null) return;

            // Set material based on type
            switch (avatarType)
            {
                case AvatarType.Hostile:
                    if (hostileMaterial != null)
                        mainRenderer.material = hostileMaterial;
                    if (weaponObject != null)
                        weaponObject.SetActive(true);
                    break;

                case AvatarType.Friendly:
                    if (friendlyMaterial != null)
                        mainRenderer.material = friendlyMaterial;
                    if (weaponObject != null)
                        weaponObject.SetActive(false);
                    break;

                case AvatarType.Unknown:
                    if (unknownMaterial != null)
                        mainRenderer.material = unknownMaterial;
                    if (weaponObject != null)
                        weaponObject.SetActive(Random.Range(0f, 1f) > 0.5f);
                    break;
            }

            // Update accessories
            UpdateAccessories();
        }

        private void UpdateAccessories()
        {
            if (accessoryObjects == null) return;

            foreach (var accessory in accessoryObjects)
            {
                if (accessory != null)
                {
                    // Random chance to show accessory based on type
                    bool show = avatarType switch
                    {
                        AvatarType.Hostile => Random.Range(0f, 1f) > 0.3f,
                        AvatarType.Friendly => Random.Range(0f, 1f) > 0.6f,
                        AvatarType.Unknown => Random.Range(0f, 1f) > 0.5f,
                        _ => false
                    };

                    accessory.SetActive(show);
                }
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Changes the avatar type (for Decision Conflict stressor)
        /// </summary>
        public void ChangeType(AvatarType newType)
        {
            if (!canChangeAppearance) return;

            avatarType = newType;
            UpdateAppearance();
        }

        /// <summary>
        /// Called when avatar is targeted by player
        /// </summary>
        public void OnTargeted()
        {
            hasBeenTargeted = true;

            // React to being targeted based on type
            if (avatarType == AvatarType.Hostile && currentState != AvatarBehaviorState.Engaged)
            {
                ChangeState(AvatarBehaviorState.Alert);
            }
            else if (avatarType == AvatarType.Friendly)
            {
                ChangeState(AvatarBehaviorState.Fleeing);
            }
        }

        /// <summary>
        /// Called when avatar is classified by player
        /// </summary>
        public void OnClassified(AvatarType classifiedAs)
        {
            hasBeenClassified = true;
            bool correct = (classifiedAs == avatarType);

            // Log classification event
            Debug.Log($"Avatar {avatarID} classified as {classifiedAs}. Actual: {avatarType}. Correct: {correct}");

            // Could trigger visual feedback or behavior change
            if (correct)
            {
                // Correct classification effect
                StartCoroutine(FlashColor(Color.green));
            }
            else
            {
                // Incorrect classification effect
                StartCoroutine(FlashColor(Color.red));
            }
        }

        /// <summary>
        /// Enables or disables appearance changing (for stressors)
        /// </summary>
        public void SetCanChangeAppearance(bool canChange)
        {
            canChangeAppearance = canChange;
        }

        /// <summary>
        /// Forces avatar to specific behavior state
        /// </summary>
        public void ForceState(AvatarBehaviorState state)
        {
            ChangeState(state);
        }

        #endregion

        #region Visual Effects

        private IEnumerator FlashColor(Color color)
        {
            if (mainRenderer == null) yield break;

            Material originalMaterial = mainRenderer.material;
            Material flashMaterial = new Material(originalMaterial);
            flashMaterial.color = color;

            mainRenderer.material = flashMaterial;
            yield return new WaitForSeconds(0.5f);
            mainRenderer.material = originalMaterial;

            Destroy(flashMaterial);
        }

        #endregion

        #region Gizmos

        void OnDrawGizmosSelected()
        {
            // Draw alert radius
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, alertRadius);

            // Draw engagement range
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, engagementRange);

            // Draw patrol points
            if (patrolPoints != null)
            {
                Gizmos.color = Color.blue;
                foreach (var point in patrolPoints)
                {
                    if (point != null)
                    {
                        Gizmos.DrawWireSphere(point.position, 0.5f);
                        Gizmos.DrawLine(transform.position, point.position);
                    }
                }
            }
        }

        #endregion
    }
}