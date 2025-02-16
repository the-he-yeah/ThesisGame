using UnityEngine;
using UnityEngine.AI;
using System.Collections;


[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
public class EnemyAI : MonoBehaviour
{
    private enum EnemyState
    {
        Patrol = 0,
        Chase = 1,
        Return = 2,
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
    [SerializeField] private float returnSpeed = 5f;
    [SerializeField] private float patrolWaitTime = 2f;
    [SerializeField] private float minPatrolRadius = 10f;
    [SerializeField] private float maxPatrolRadius = 30f;
    [SerializeField] private float attackRange = 2f;

    [Header("References")]
    [SerializeField] private GameManager gameManager;

    private EnemyState currentState;
    private Vector3 lastKnownPlayerPosition;
    private Vector3 currentPatrolDestination;
    private float stateTimer;
    private bool isWaiting;
    private int failedPatrolAttempts;
    private const int MAX_PATROL_ATTEMPTS = 3;
    private bool isAttackingPlayer = false;
    private float attackTimer = 0f;
    private const float ATTACK_DELAY = 3f;
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
        
        currentState = EnemyState.Patrol;
        agent.speed = patrolSpeed;
        SetNewPatrolDestination();
    }

    private void Update()
    {
        switch (currentState)
        {
            case EnemyState.Patrol:
                UpdatePatrolState();
                CheckForPlayerDetection();
                break;
            case EnemyState.Chase:
                UpdateChaseState();
                break;
            case EnemyState.Return:
                UpdateReturnState();
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
                SetNewPatrolDestination();
            }
            return;
        }

        if (!agent.pathPending && agent.remainingDistance < 0.5f)
        {
            isWaiting = true;
            stateTimer = patrolWaitTime;
        }
    }

    private void UpdateChaseState() 
    {
        detectionTimer += Time.deltaTime;
        
        if (detectionTimer >= detectionTimeout)
        {
            // Recheck if player is still detectable
            bool playerDetected = false;
            
            float distanceToPlayer = Vector3.Distance(eyePosition.position, player.position);
            
            // Check immediate detection
            if (distanceToPlayer <= immediateDetectionRange && HasLineOfSightToPlayer())
            {
                playerDetected = true;
            }
            // Check main detection cone
            else if (distanceToPlayer <= mainDetectionRange)
            {
                Vector3 directionToPlayer = (player.position - eyePosition.position).normalized;
                float angle = Vector3.Angle(eyePosition.forward, directionToPlayer);
                
                if (angle <= mainDetectionAngle * 0.5f && HasLineOfSightToPlayer())
                {
                    playerDetected = true;
                }
            }
            // Check peripheral detection
            else if (distanceToPlayer <= peripheralDetectionRange)
            {
                Vector3 directionToPlayer = (player.position - eyePosition.position).normalized;
                float angle = Vector3.Angle(eyePosition.forward, directionToPlayer);
                
                if (angle <= peripheralDetectionAngle * 0.5f && HasLineOfSightToPlayer())
                {
                    playerDetected = true; 
                }
            }

            if (playerDetected)
            {
                // Reset timer and continue chasing
                detectionTimer = 0f;
                if (showDebugLines)
                {
                    Debug.Log("Player still detected - continuing chase");
                }
            }
            else 
            {
                // Lost player - transition to return state
                if (showDebugLines)
                {
                    Debug.Log("Lost player - transitioning to return state");
                }
                TransitionToState(EnemyState.Return);
                return;
            }
        }

        // Continue normal chase behavior
        agent.SetDestination(player.position);
        lastKnownPlayerPosition = player.position;

        if (Vector3.Distance(transform.position, player.position) <= attackRange)
        {
            TransitionToState(EnemyState.Attack);
        }
    }

    private void UpdateReturnState()
    {
        if (!agent.pathPending && agent.remainingDistance < 0.5f)
        {
            TransitionToState(EnemyState.Patrol);
            return;
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

        transform.LookAt(player.position);
        
        if (Vector3.Distance(transform.position, player.position) > attackRange)
        {
            // Reset attack state if player moves out of range
            isAttackingPlayer = false;
            attackTimer = 0f;
            TransitionToState(EnemyState.Chase);
        }
        else if (!isAttackingPlayer)
        {
            // Start attack sequence
            isAttackingPlayer = true;
            attackTimer = 0f;
            Debug.Log("Starting attack sequence!");
        }
        else
        {
            // Count up the timer
            attackTimer += Time.deltaTime;
            
            if (showDebugLines)
            {
                Debug.Log($"Attack timer: {ATTACK_DELAY - attackTimer:F1} seconds remaining");
            }

            if (attackTimer >= ATTACK_DELAY)
            {
                Debug.Log("Attack successful!");
                TriggerGameOver();
            }
        }
    }


    private void CheckForPlayerDetection()
    {
        if (currentState == EnemyState.Attack) return;

        float distanceToPlayer = Vector3.Distance(eyePosition.position, player.position);
        
        if (showDebugLines)
        {
            Debug.Log($"Distance to player: {distanceToPlayer}");
        }
        
        // Immediate detection check
        if (distanceToPlayer <= immediateDetectionRange)
        {
            Debug.Log("Immediate detection - Transitioning to Chase");
            TransitionToState(EnemyState.Chase);
            return;
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
        
        // Reset detection timer when transitioning states
        detectionTimer = 0f;

        switch (newState)
        {
            case EnemyState.Patrol:
                agent.isStopped = false;
                agent.speed = patrolSpeed;
                SetNewPatrolDestination();
                break;

            case EnemyState.Chase:
                agent.isStopped = false;
                agent.speed = chaseSpeed;
                break;

            case EnemyState.Return:
                agent.isStopped = false;
                agent.speed = returnSpeed;
                agent.SetDestination(lastKnownPlayerPosition);
                break;

            case EnemyState.Attack:
                agent.isStopped = true;
                animator.SetTrigger("Attack");
                break;
        }

        currentState = newState;
    }

    private void UpdateAnimatorState()
    {
        animator.SetInteger("State", (int)currentState);
        animator.SetFloat("Speed", agent.velocity.magnitude / chaseSpeed);
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