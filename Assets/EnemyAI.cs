using UnityEngine;
using UnityEngine.AI;
using System.Collections;


[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
public class EnemyAI : MonoBehaviour
{
    private enum EnemyState
    {
        Idle = 0,
        Walk = 1,
        Chase = 2,
        Attack = 3
    }

    [Header("Components")]
    private NavMeshAgent agent;
    private Animator animator;
    private Transform player;
    [SerializeField] private Transform eyePosition; // Reference to the Eye GameObject

    [Header("Detection Settings")]
    [SerializeField] private float immediateDetectionRange = 2f;
    [SerializeField] private float peripheralDetectionRange = 8f;
    [SerializeField] private float mainDetectionRange = 15f;
    [SerializeField] private float mainDetectionAngle = 60f;
    [SerializeField] private float peripheralDetectionAngle = 90f;
    [SerializeField] private LayerMask detectionLayers;
    [SerializeField] private LayerMask obstacleLayer; // Add this for obstacle detection
    [SerializeField] private bool showDebugLines = true; // Add debug toggle
    [SerializeField] private float detectionTimeout = 5f; // Time in seconds to track player before rechecking detection

    

    [Header("Movement Settings")]
    [SerializeField] private float patrolSpeed = 3.5f;
    [SerializeField] private float chaseSpeed = 7f;
    [SerializeField] private float patrolWaitTime = 2f;
    [SerializeField] private float minPatrolRadius = 10f;
    [SerializeField] private float maxPatrolRadius = 30f;
    [SerializeField] private float attackRange = 2f;

    [Header("References")]
    [SerializeField] private GameManager gameManager;

    private EnemyState currentState;
    private Vector3 currentPatrolDestination;
    private float stateTimer;
    private bool isWaiting;
    private int failedPatrolAttempts;
    private const int MAX_PATROL_ATTEMPTS = 3;
    private bool gameOverTriggered = false;
    private float detectionTimer = 0f;


    private void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        player = GameObject.FindGameObjectWithTag("Player").transform;

        if (eyePosition == null)
        {
            Debug.LogError("Eye position not set! Please assign the Eye GameObject in the inspector.");
            enabled = false;
            return;
        }
        
        // Initialize with proper movement settings
        agent.speed = patrolSpeed;
        agent.isStopped = false;
        
        // Start in walk state and set initial patrol destination
        currentState = EnemyState.Walk;
        UpdateAnimatorState();
        SetNewPatrolDestination();
    }

    private void Update()
    {
        switch (currentState)
        {
            case EnemyState.Idle:
            case EnemyState.Walk:
                UpdatePatrolState();
                CheckForPlayerDetection();
                break;
            case EnemyState.Chase:
                UpdateChaseState();
                break;
            case EnemyState.Attack:
                UpdateAttackState();
                break;
        }
        
        UpdateAnimatorState();
    }

    private void UpdatePatrolState()
    {
        if (isWaiting)
        {
            stateTimer -= Time.deltaTime;
            if (stateTimer <= 0)
            {
                isWaiting = false;
                TransitionToState(EnemyState.Walk);
                SetNewPatrolDestination();
                Debug.Log("Patrol wait time over - starting patrol");

            }
            return;
        }

        if (!agent.pathPending && agent.remainingDistance < 0.5f)
        {
            isWaiting = true;
            stateTimer = patrolWaitTime;
            TransitionToState(EnemyState.Idle);
            Debug.Log("Reached patrol point - waiting");
        }
    }

    private void UpdateChaseState() 
    {
        detectionTimer += Time.deltaTime;
        
        if (detectionTimer >= detectionTimeout)
        {
            if (IsPlayerDetected()) // Use the CheckForPlayerDetection logic
            {
                // Reset timer and continue chase
                detectionTimer = 0f;
                if (showDebugLines)
                {
                    Debug.Log("Player still detected - continuing chase");
                }
            }
            else 
            {
                // Lost player - transition to patrol
                if (showDebugLines)
                {
                    Debug.Log("Lost player - transitioning to patrol");
                }
                isWaiting = true;
                stateTimer = patrolWaitTime;
                TransitionToState(EnemyState.Idle);
                return;
            }
        }

        // Update destination to current player position
        agent.SetDestination(player.position);

        // Check for attack range
       if (Vector3.Distance(transform.position, player.position) <= attackRange)
        {
            player.gameObject.SetActive(false); // Or use player.enabled if it's a specific component
            TransitionToState(EnemyState.Attack);
        }
    }

    

    private void TriggerGameOver()
    {
        if (gameOverTriggered) return;
        
        if (gameManager != null)
        {
            gameOverTriggered = true;
            agent.isStopped = true;
            Debug.Log("Triggering game over!");
            gameManager.ShowGameOver();
        }
        else
        {
            Debug.LogError("GameManager reference not set on " + gameObject.name);
        }
    }

    private void UpdateAttackState()
    {
        if (gameOverTriggered) return;

        // Turn to face player
        transform.LookAt(player.position);
    }

    // Add animation event callback
    public void OnAttackAnimationComplete()
    {
        TriggerGameOver();
        Debug.Log($"GameOver Triggered");
    }


    private void CheckForPlayerDetection()
    {
        if (currentState == EnemyState.Attack) return;

        float distanceToPlayer = Vector3.Distance(eyePosition.position, player.position);
        
        //if (showDebugLines)
        //{
        //    Debug.Log($"Distance to player: {distanceToPlayer}");
        //}
        
        // Immediate detection check
        if (distanceToPlayer <= immediateDetectionRange)
        {
            if (HasLineOfSightToPlayer())
            {
                Debug.Log("Immediate detection - Transitioning to Chase");
                TransitionToState(EnemyState.Chase);
                return;
            }
        }

        // Main detection cone check
        if (distanceToPlayer <= mainDetectionRange)
        {
            Vector3 directionToPlayer = (player.position - eyePosition.position).normalized;
            float angle = Vector3.Angle(eyePosition.forward, directionToPlayer);

            if (showDebugLines)
            {
                Debug.Log($"Angle to player: {angle}");
            }

            if (angle <= mainDetectionAngle * 0.5f && HasLineOfSightToPlayer())
            {
                Debug.Log("Main cone detection - Transitioning to Chase");
                TransitionToState(EnemyState.Chase);
                return;
            }
        }

        // Peripheral vision check
        if (distanceToPlayer <= peripheralDetectionRange)
        {
            Vector3 directionToPlayer = (player.position - eyePosition.position).normalized;
            float angle = Vector3.Angle(eyePosition.forward, directionToPlayer);

            if (angle <= peripheralDetectionAngle * 0.5f && HasLineOfSightToPlayer())
            {
                Debug.Log("Peripheral detection - Transitioning to Chase");
                TransitionToState(EnemyState.Chase);
            }
        }
    }

    private bool IsPlayerDetected()
    {
        float distanceToPlayer = Vector3.Distance(eyePosition.position, player.position);
        
        // Immediate detection check - no line of sight needed
        if (distanceToPlayer <= immediateDetectionRange)
        {
            return true;
        }

        // Main detection cone check
        if (distanceToPlayer <= mainDetectionRange)
        {
            Vector3 directionToPlayer = (player.position - eyePosition.position).normalized;
            float angle = Vector3.Angle(eyePosition.forward, directionToPlayer);
            
            if (angle <= mainDetectionAngle * 0.5f && HasLineOfSightToPlayer())
            {
                return true;
            }
        }

        // Peripheral vision check
        if (distanceToPlayer <= peripheralDetectionRange)
        {
            Vector3 directionToPlayer = (player.position - eyePosition.position).normalized;
            float angle = Vector3.Angle(eyePosition.forward, directionToPlayer);
            
            if (angle <= peripheralDetectionAngle * 0.5f && HasLineOfSightToPlayer())
            {
                return true;
            }
        }

        return false;
    }

    private bool HasLineOfSightToPlayer()
    {
        if (player == null || eyePosition == null) return false;

        Vector3 directionToPlayer = player.position - eyePosition.position;
        float distanceToPlayer = directionToPlayer.magnitude;
        
        // Check if player is below eye level
        float heightDifference = eyePosition.position.y - player.position.y;
        
        // Only allow detection if player is below eye level
        if (heightDifference < 0) // Player is above eye level
        {
            if (showDebugLines)
            {
                Debug.DrawLine(eyePosition.position, player.position, Color.blue, 0.1f);
                Debug.Log("Player above eye level - cannot detect");
            }
            return false;
        }

        if (showDebugLines)
        {
            Debug.DrawLine(eyePosition.position, player.position, Color.yellow, 0.1f);
            Debug.Log($"Checking line of sight. Distance: {distanceToPlayer}, Height diff: {heightDifference}");
        }

        // Check for obstacles
        if (Physics.Raycast(eyePosition.position, directionToPlayer.normalized, out RaycastHit obstacleHit, distanceToPlayer, obstacleLayer))
        {
            if (showDebugLines)
            {
                Debug.DrawLine(eyePosition.position, obstacleHit.point, Color.red, 0.1f);
                Debug.Log($"View blocked by: {obstacleHit.transform.name}");
            }
            return false;
        }

        // Check for player
        if (Physics.Raycast(eyePosition.position, directionToPlayer.normalized, out RaycastHit playerHit, distanceToPlayer, detectionLayers))
        {
            bool isPlayer = playerHit.transform.CompareTag("Player");
            if (showDebugLines)
            {
                Color rayColor = isPlayer ? Color.green : Color.red;
                Debug.DrawLine(eyePosition.position, playerHit.point, rayColor, 0.1f);
                Debug.Log($"Hit {playerHit.transform.name}, isPlayer: {isPlayer}");
            }
            return isPlayer;
        }

        return false;
    }

    private void SetNewPatrolDestination()
    {
        if (failedPatrolAttempts >= MAX_PATROL_ATTEMPTS)
        {
            // If we've failed too many times, try a closer point
            failedPatrolAttempts = 0;
            FindPatrolPoint(minPatrolRadius * 0.5f);
            return;
        }

        if (!FindPatrolPoint(Random.Range(minPatrolRadius, maxPatrolRadius)))
        {
            failedPatrolAttempts++;
            SetNewPatrolDestination();
        }
    }

    private bool FindPatrolPoint(float radius)
    {
        Vector3 randomDirection = Random.insideUnitSphere * radius;
        randomDirection += transform.position;
        NavMeshHit hit;

        if (NavMesh.SamplePosition(randomDirection, out hit, radius, NavMesh.AllAreas))
        {
            currentPatrolDestination = hit.position;
            agent.SetDestination(currentPatrolDestination);
            return true;
        }

        return false;
    }

    private void TransitionToState(EnemyState newState)
    {
        if (currentState == newState) return;

        Debug.Log($"Transitioning from {currentState} to {newState}");
        
        detectionTimer = 0f;

        switch (newState)
        {
            case EnemyState.Idle:
                agent.isStopped = true;
                agent.speed = 0;
                break;

            case EnemyState.Walk:
                agent.isStopped = false;
                agent.speed = patrolSpeed;
                break;

            case EnemyState.Chase:
                agent.isStopped = false;
                agent.speed = chaseSpeed;
                break;

            case EnemyState.Attack:
                agent.isStopped = true;
                agent.speed = 0;
                break;
        }

        currentState = newState;
        UpdateAnimatorState();
    }

    private void UpdateAnimatorState()
    {
        switch (currentState)
        {
            case EnemyState.Idle:
            case EnemyState.Walk:
                // Set to walk animation if moving, idle if stopped
                float speed = agent.velocity.magnitude;
                animator.SetInteger("State", speed > 0.1f ? 1 : 0);
                break;
            case EnemyState.Chase:
                animator.SetInteger("State", 2); // Chase
                break;
            case EnemyState.Attack:
                animator.SetInteger("State", 3); // Attack
                break;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            Debug.Log("Player collision detected!");
            TriggerGameOver();
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!eyePosition) return;

        // Draw immediate detection range
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(eyePosition.position, immediateDetectionRange);

        // Draw main detection cone
        Gizmos.color = Color.yellow;
        DrawDetectionCone(mainDetectionAngle, mainDetectionRange);

        // Draw peripheral detection cone
        Gizmos.color = Color.blue;
        DrawDetectionCone(peripheralDetectionAngle, peripheralDetectionRange);

        // Draw horizontal line to show eye level
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(
            eyePosition.position - eyePosition.right * mainDetectionRange,
            eyePosition.position + eyePosition.right * mainDetectionRange
        );
         // Draw attack range
        Gizmos.color = new Color(1f, 0f, 0f, 0.3f); // Semi-transparent red
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }

    private void DrawDetectionCone(float angle, float range)
    {
        if (!eyePosition) return;

        float halfAngle = angle * 0.5f;
        Vector3 leftDirection = Quaternion.Euler(0, -halfAngle, 0) * eyePosition.forward;
        Vector3 rightDirection = Quaternion.Euler(0, halfAngle, 0) * eyePosition.forward;

        Gizmos.DrawLine(eyePosition.position, eyePosition.position + leftDirection * range);
        Gizmos.DrawLine(eyePosition.position, eyePosition.position + rightDirection * range);
        
        Vector3 previousPoint = eyePosition.position + leftDirection * range;
        int segments = 20;
        
        for (int i = 0; i <= segments; i++)
        {
            float currentAngle = -halfAngle + (angle * i / segments);
            Vector3 direction = Quaternion.Euler(0, currentAngle, 0) * eyePosition.forward;
            Vector3 currentPoint = eyePosition.position + direction * range;
            
            Gizmos.DrawLine(previousPoint, currentPoint);
            previousPoint = currentPoint;
        }
    }



}