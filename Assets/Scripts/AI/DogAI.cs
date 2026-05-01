using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// The dog enemy. Full state machine driven by vision and sound detection.
///
/// STATE FLOW:
///   Resting ──► Patrol ──► Suspicious ──► Alert/Chase ──► ReturnToBasket ──► Resting
///                                                   ▲                              │
///                              Hiss ───────► Stunned ──────────────────────────────┘
///
/// SETUP CHECKLIST:
///   1. Add NavMeshAgent component — bake a NavMesh for your level.
///   2. Assign waypoints[] in the inspector for patrol path.
///   3. Assign basket transform (the dog bed GameObject).
///   4. Set obstructionMask to layers that block line-of-sight (walls, furniture).
///   5. Player GameObject must have the "Player" tag.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class DogAI : MonoBehaviour
{
    public enum DogState { Resting, Patrol, Suspicious, Alert, Chase, ReturnToBasket, Stunned }

    // ── Patrol ───────────────────────────────────────────────────────────────
    [Header("Patrol")]
    [Tooltip("Ordered list of positions the dog walks between.")]
    public Transform[] waypoints;
    public float patrolSpeed = 2f;
    [Tooltip("How long the dog pauses at each waypoint.")]
    public float waypointWaitTime = 1.5f;
    [Tooltip("How many full patrol loops before the dog heads home to rest.")]
    public int patrolCyclesBeforeRest = 2;

    // ── Basket / Comfort Zone ────────────────────────────────────────────────
    [Header("Basket (Comfort Zone)")]
    [Tooltip("The dog's bed. It returns here to rest and after being hissed at.")]
    public Transform basket;
    [Tooltip("How long the dog rests at the basket before patrolling again.")]
    public float restDuration = 6f;
    [Tooltip("Distance from basket considered 'arrived'.")]
    public float basketArrivalRadius = 1.2f;

    // ── Vision ───────────────────────────────────────────────────────────────
    [Header("Vision")]
    public float viewDistance = 10f;
    [Tooltip("Half-angle of the vision cone in degrees. 60 = 120° total FOV.")]
    public float viewAngle = 60f;
    [Tooltip("Extended view range when already in Alert/Chase state.")]
    public float alertViewDistance = 16f;
    [Tooltip("Layers that block line-of-sight. Usually 'Default' or a 'Wall' layer.")]
    public LayerMask obstructionMask;

    // ── Sound Detection ──────────────────────────────────────────────────────
    [Header("Sound Detection")]
    [Tooltip("Within this range a noise makes the dog suspicious.")]
    public float suspicionSoundRadius = 8f;
    [Tooltip("Within this range a noise immediately alerts the dog.")]
    public float alertSoundRadius = 3f;

    // ── Chase ─────────────────────────────────────────────────────────────────
    [Header("Chase")]
    public float chaseSpeed = 5.5f;
    [Tooltip("How close the dog gets before catching the player.")]
    public float catchDistance = 1.1f;
    [Tooltip("Seconds without seeing the player before giving up the chase.")]
    public float losePlayerTime = 5f;

    // ── Suspicion ─────────────────────────────────────────────────────────────
    [Header("Suspicion")]
    public float suspiciousSpeed = 2.8f;
    [Tooltip("How long the dog sniffs around before giving up.")]
    public float investigateDuration = 5f;
    [Tooltip("Seconds of continuous eye contact before the dog goes to alert.")]
    public float timeToAlert = 1.5f;

    // ── Public State ──────────────────────────────────────────────────────────
    public DogState CurrentState { get; private set; } = DogState.Resting;

    private NavMeshAgent agent;
    private Transform playerTransform;

    private int currentWaypointIndex;
    private int patrolCyclesCompleted;
    private float stateTimer;
    private float losePlayerTimer;
    private float alertBuildupTimer;    // Time dog has had eyes on player
    private bool waitingAtWaypoint;
    private Vector3 lastKnownPlayerPos;
    private Vector3 lastHeardPosition;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player) playerTransform = player.transform;
    }

    private void OnEnable()  => NoiseSystem.OnNoiseEmitted += HandleNoise;
    private void OnDisable() => NoiseSystem.OnNoiseEmitted -= HandleNoise;

    private void Start()
    {
        if (basket == null) basket = transform;
        EnterState(DogState.Resting);
    }

    private void Update()
    {
        switch (CurrentState)
        {
            case DogState.Resting:          UpdateResting();        break;
            case DogState.Patrol:           UpdatePatrol();         break;
            case DogState.Suspicious:       UpdateSuspicious();     break;
            case DogState.Alert:            UpdateAlert();          break;
            case DogState.Chase:            UpdateChase();          break;
            case DogState.ReturnToBasket:   UpdateReturnToBasket(); break;
            case DogState.Stunned:                                  break;
        }
    }

    // ── State Machine: Enter ──────────────────────────────────────────────────

    private void EnterState(DogState newState)
    {
        CurrentState = newState;
        stateTimer = 0f;

        switch (newState)
        {
            case DogState.Resting:
                agent.isStopped = true;
                break;

            case DogState.Patrol:
                agent.isStopped = false;
                agent.speed = patrolSpeed;
                GoToNextWaypoint();
                break;

            case DogState.Suspicious:
                agent.isStopped = false;
                agent.speed = suspiciousSpeed;
                agent.SetDestination(lastHeardPosition);
                alertBuildupTimer = 0f;
                break;

            case DogState.Alert:
            case DogState.Chase:
                agent.isStopped = false;
                agent.speed = chaseSpeed;
                losePlayerTimer = 0f;
                break;

            case DogState.ReturnToBasket:
                agent.isStopped = false;
                agent.speed = patrolSpeed;
                agent.SetDestination(basket.position);
                break;

            case DogState.Stunned:
                agent.isStopped = true;
                break;
        }
    }

    // ── State Machine: Update ─────────────────────────────────────────────────

    private void UpdateResting()
    {
        stateTimer += Time.deltaTime;

        // Even while resting, a very close sight line will rouse the dog
        if (CanSeePlayer())
        {
            lastKnownPlayerPos = playerTransform.position;
            EnterState(DogState.Alert);
            return;
        }

        if (stateTimer >= restDuration)
        {
            patrolCyclesCompleted = 0;
            EnterState(DogState.Patrol);
        }
    }

    private void UpdatePatrol()
    {
        if (CanSeePlayer())
        {
            alertBuildupTimer += Time.deltaTime;
            lastKnownPlayerPos = playerTransform.position;
            if (alertBuildupTimer >= timeToAlert)
            {
                EnterState(DogState.Alert);
                return;
            }
        }
        else
        {
            alertBuildupTimer = Mathf.Max(0f, alertBuildupTimer - Time.deltaTime);
        }

        if (waitingAtWaypoint)
        {
            stateTimer += Time.deltaTime;
            if (stateTimer >= waypointWaitTime)
            {
                waitingAtWaypoint = false;
                GoToNextWaypoint();
            }
            return;
        }

        if (!agent.pathPending && agent.remainingDistance < 0.4f)
        {
            waitingAtWaypoint = true;
            stateTimer = 0f;

            // Track patrol cycles; when done, go rest
            if (currentWaypointIndex == 0)
            {
                patrolCyclesCompleted++;
                if (patrolCyclesCompleted >= patrolCyclesBeforeRest)
                {
                    EnterState(DogState.ReturnToBasket);
                    return;
                }
            }
        }
    }

    private void UpdateSuspicious()
    {
        stateTimer += Time.deltaTime;

        if (CanSeePlayer())
        {
            alertBuildupTimer += Time.deltaTime;
            lastKnownPlayerPos = playerTransform.position;

            if (alertBuildupTimer >= timeToAlert)
            {
                EnterState(DogState.Alert);
                return;
            }
        }
        else
        {
            alertBuildupTimer = Mathf.Max(0f, alertBuildupTimer - Time.deltaTime * 0.5f);
        }

        if (stateTimer >= investigateDuration)
        {
            alertBuildupTimer = 0f;
            EnterState(DogState.Patrol);
        }
    }

    private void UpdateAlert()
    {
        if (playerTransform == null) return;

        if (CanSeePlayer())
        {
            lastKnownPlayerPos = playerTransform.position;
            agent.SetDestination(lastKnownPlayerPos);
            losePlayerTimer = 0f;
        }
        else
        {
            losePlayerTimer += Time.deltaTime;
            if (losePlayerTimer >= losePlayerTime)
            {
                lastHeardPosition = lastKnownPlayerPos;
                EnterState(DogState.Suspicious);
                return;
            }
        }

        if (Vector3.Distance(transform.position, playerTransform.position) <= catchDistance)
            CatchPlayer();
    }

    private void UpdateChase() => UpdateAlert();

    private void UpdateReturnToBasket()
    {
        // Still alert for player on the way home
        if (CanSeePlayer())
        {
            lastKnownPlayerPos = playerTransform.position;
            EnterState(DogState.Alert);
            return;
        }

        if (!agent.pathPending && agent.remainingDistance <= basketArrivalRadius)
            EnterState(DogState.Resting);
    }

    // ── Detection ─────────────────────────────────────────────────────────────

    private bool CanSeePlayer()
    {
        if (playerTransform == null) return false;

        float dist = Vector3.Distance(transform.position, playerTransform.position);
        float range = (CurrentState == DogState.Alert || CurrentState == DogState.Chase)
            ? alertViewDistance : viewDistance;

        if (dist > range) return false;

        Vector3 dir = (playerTransform.position - transform.position).normalized;
        if (Vector3.Angle(transform.forward, dir) > viewAngle) return false;

        // Raycast from the dog's head height to the player's centre
        Vector3 origin = transform.position + Vector3.up * 0.6f;
        if (Physics.Raycast(origin, dir, dist, obstructionMask)) return false;

        return true;
    }

    private void HandleNoise(Vector3 noisePos, float radius, NoiseType type)
    {
        // Resting dogs are heavy sleepers — only very loud noises wake them
        if (CurrentState == DogState.Stunned) return;
        if (CurrentState == DogState.Resting && type != NoiseType.Sprint && type != NoiseType.Land) return;

        float dist = Vector3.Distance(transform.position, noisePos);
        if (dist > radius) return;

        lastHeardPosition = noisePos;

        bool isVeryClose = dist <= alertSoundRadius;
        bool isAlreadySearching = CurrentState == DogState.Alert || CurrentState == DogState.Chase;

        if (isVeryClose || isAlreadySearching)
        {
            if (CurrentState != DogState.Alert && CurrentState != DogState.Chase)
                EnterState(DogState.Alert);
        }
        else if (CurrentState == DogState.Patrol      ||
                 CurrentState == DogState.Resting      ||
                 CurrentState == DogState.ReturnToBasket)
        {
            EnterState(DogState.Suspicious);
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Called by PlayerCombat when the player hisses at the dog.</summary>
    public void Stun(float duration)
    {
        StopAllCoroutines();
        StartCoroutine(StunRoutine(duration));
    }

    private IEnumerator StunRoutine(float duration)
    {
        EnterState(DogState.Stunned);
        yield return new WaitForSeconds(duration);
        EnterState(DogState.ReturnToBasket);
    }

    private void CatchPlayer()
    {
        playerTransform.GetComponent<HealthScript>()?.TakeDamage(1f);
        StartCoroutine(CatchCooldown());
    }

    private IEnumerator CatchCooldown()
    {
        agent.isStopped = true;
        yield return new WaitForSeconds(0.6f);
        if (CurrentState == DogState.Alert || CurrentState == DogState.Chase)
            agent.isStopped = false;
    }

    // ── Patrol Helpers ────────────────────────────────────────────────────────

    private void GoToNextWaypoint()
    {
        if (waypoints == null || waypoints.Length == 0) return;
        agent.SetDestination(waypoints[currentWaypointIndex].position);
        currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
    }

    // ── Gizmos ────────────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        // Vision cone
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, viewDistance);
        Vector3 left  = Quaternion.Euler(0, -viewAngle, 0) * transform.forward;
        Vector3 right = Quaternion.Euler(0,  viewAngle, 0) * transform.forward;
        Gizmos.DrawRay(transform.position, left  * viewDistance);
        Gizmos.DrawRay(transform.position, right * viewDistance);

        // Catch range
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, catchDistance);

        // Basket comfort zone
        if (basket != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(basket.position, basketArrivalRadius);
        }

        // Waypoint path preview
        if (waypoints != null && waypoints.Length > 1)
        {
            Gizmos.color = Color.green;
            for (int i = 0; i < waypoints.Length; i++)
            {
                if (waypoints[i] == null) continue;
                Gizmos.DrawSphere(waypoints[i].position, 0.2f);
                Gizmos.DrawLine(waypoints[i].position,
                    waypoints[(i + 1) % waypoints.Length].position);
            }
        }
    }
}
