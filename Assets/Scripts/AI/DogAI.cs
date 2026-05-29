using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class DogAI : MonoBehaviour
{
    private float _animLogTimer;

    public enum DogState
    {
        Resting, Patrol, Suspicious, Alert, Chase,
        ReturnToBasket, Stunned, Pinning, Carrying, Escorting
    }

    [Header("Animation")]
    [Tooltip("Animator on the dog model child. Auto-found if left empty.")]
    public Animator animator;

    private static readonly int VertHash = Animator.StringToHash("Vert");
    private static readonly int StateHash = Animator.StringToHash("State");

    [Header("Patrol")]
    [Tooltip("Ordered list of positions the dog walks between.")]
    public Transform[] waypoints;
    public float patrolSpeed = 2f;
    [Tooltip("How long the dog pauses at each waypoint.")]
    public float waypointWaitTime = 1.5f;
    [Tooltip("How many full patrol loops before the dog heads home to rest.")]
    public int patrolCyclesBeforeRest = 2;

    [Header("Basket (Comfort Zone)")]
    [Tooltip("The dog's bed. It returns here to rest and after being hissed at.")]
    public Transform basket;
    [Tooltip("Base rest duration — actual time is randomised around this.")]
    public float restDuration = 6f;
    [Tooltip("Random seconds added/removed from rest duration each time.")]
    public float restVariance = 2f;
    [Tooltip("Distance from basket considered 'arrived'.")]
    public float basketArrivalRadius = 1.2f;

    [Header("Vision")]
    public float viewDistance = 10f;
    [Tooltip("Half-angle of the vision cone in degrees. 60 = 120 total FOV.")]
    public float viewAngle = 60f;
    [Tooltip("Extended view range when already in Alert/Chase state.")]
    public float alertViewDistance = 16f;
    [Tooltip("Layers that block line-of-sight. Usually Default or a Wall layer.")]
    public LayerMask obstructionMask;

    [Header("Sound Detection")]
    [Tooltip("Within this range a noise makes the dog suspicious.")]
    public float suspicionSoundRadius = 8f;
    [Tooltip("Within this range a noise immediately alerts the dog.")]
    public float alertSoundRadius = 3f;

    [Header("Chase")]
    public float chaseSpeed = 5.5f;
    [Tooltip("How close the dog gets before pinning the player.")]
    public float catchDistance = 1.1f;
    [Tooltip("Seconds without seeing the player before giving up the chase.")]
    public float losePlayerTime = 5f;

    [Header("Suspicion")]
    public float suspiciousSpeed = 2.8f;
    [Tooltip("How long the dog sniffs around before giving up.")]
    public float investigateDuration = 5f;
    [Tooltip("Seconds of continuous eye contact before the dog goes to alert.")]
    public float timeToAlert = 1.5f;

    [Header("Search Sweep")]
    public float sweepRadius = 3f;
    [Tooltip("Extra positions checked during Suspicious investigation.")]
    public int sweepPointCount = 3;
    [Tooltip("Seconds paused at each sweep point.")]
    public float sweepPauseTime = 1.2f;
    [Tooltip("Chance per waypoint arrival of a brief patrol sniff (0–1).")]
    [Range(0f, 1f)]
    public float patrolSweepChance = 0.12f;

    [Header("Escalation")]
    public float escalationViewAngleIncrease = 5f;
    public float escalationTimeToAlertDecrease = 0.2f;
    public float escalationLosePlayerTimeDecrease = 0.5f;
    public int maxEscalationSteps = 4;

    [Header("Pinning")]
    [Tooltip("Seconds after catching cat before first bite.")]
    public float firstBiteDelay = 0.4f;
    [Tooltip("Seconds after first bite before second bite if still pinned.")]
    public float secondBiteDelay = 5f;
    [Tooltip("Seconds of shaking before releasing cat (if still has HP).")]
    public float shakeDelay = 1f;
    [Tooltip("How many E presses needed to escape the pin.")]
    public int escapePressesNeeded = 8;

    [Header("Carrying")]
    [Tooltip("Transform at dog's mouth where cat is parented when carried.")]
    public Transform mouthTransform;
    [Tooltip("The front door — where the cat gets thrown out.")]
    public Transform frontDoor;

    [Header("Escort")]
    [Tooltip("How far behind grandma the dog walks.")]
    public float escortOffset = 1.5f;
    [Tooltip("Speed while escorting — should be slightly above grandma's walk speed.")]
    public float escortSpeed = 1.4f;

    [Header("References")]
    [Tooltip("Assign GrandmaAI so the dog can alert her when it barks.")]
    public GrandmaAI grandmaAI;

    public DogState CurrentState { get; private set; } = DogState.Resting;

    private NavMeshAgent agent;
    private Transform playerTransform;
    private HealthScript playerHealth;
    private Transform escortTarget;
    private int currentWaypointIndex;
    private int patrolCyclesCompleted;
    private float stateTimer;
    private float losePlayerTimer;
    private float alertBuildupTimer;
    private bool waitingAtWaypoint;
    private Vector3 lastKnownPlayerPos;
    private Vector3 lastHeardPosition;
    private float currentRestDuration;
    private int escapeCounter;
    private Queue<Vector3> sweepQueue = new Queue<Vector3>();
    private bool generatedSweepPoints;
    private bool pausingAtSweepPoint;
    private float sweepPauseTimer;
    private int escalationLevel;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (animator == null) animator = GetComponentInChildren<Animator>();

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player)
        {
            playerTransform = player.transform;
            playerHealth = player.GetComponent<HealthScript>();
            Debug.Log("[DogAI] Player found: " + player.name);
        }
        else
        {
            Debug.LogWarning("[DogAI] No GameObject with tag 'Player' found!");
        }
    }

    private void OnEnable() => NoiseSystem.OnNoiseEmitted += HandleNoise;
    private void OnDisable() => NoiseSystem.OnNoiseEmitted -= HandleNoise;

    private void Start()
    {
        if (basket == null)
        {
            basket = transform;
            Debug.LogWarning("[DogAI] No basket assigned — using dog's own position as basket.");
        }

        if (waypoints == null || waypoints.Length == 0)
            Debug.LogWarning("[DogAI] No waypoints assigned — dog will not patrol.");

        if (frontDoor == null)
            Debug.LogWarning("[DogAI] No front door assigned — carrying state will not work.");

        EnterState(DogState.Resting);
    }

    private void Update()
    {
        switch (CurrentState)
        {
            case DogState.Resting: UpdateResting(); break;
            case DogState.Patrol: UpdatePatrol(); break;
            case DogState.Suspicious: UpdateSuspicious(); break;
            case DogState.Alert: UpdateAlert(); break;
            case DogState.Chase: UpdateAlert(); break;
            case DogState.ReturnToBasket: UpdateReturnToBasket(); break;
            case DogState.Pinning: UpdatePinning(); break;
            case DogState.Carrying: UpdateCarrying(); break;
            case DogState.Escorting: UpdateEscorting(); break;
            case DogState.Stunned: break;
        }
        UpdateAnimator();
    }

    private void EnterState(DogState newState)
    {
        Debug.Log("[DogAI] State: " + CurrentState + " -> " + newState);
        DogState previousState = CurrentState;
        CurrentState = newState;
        stateTimer = 0f;

        switch (newState)
        {
            case DogState.Resting:
                agent.isStopped = true;
                currentRestDuration = restDuration + Random.Range(-restVariance, restVariance);
                currentRestDuration = Mathf.Max(2f, currentRestDuration);
                Debug.Log("[DogAI] Resting for " + currentRestDuration.ToString("F1") + "s.");
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
                generatedSweepPoints = false;
                sweepQueue.Clear();
                pausingAtSweepPoint = false;
                Debug.Log("[DogAI] Suspicious — investigating at " + lastHeardPosition);
            break;

            case DogState.Alert:
            case DogState.Chase:
                agent.isStopped = false;
                agent.speed = chaseSpeed;
                losePlayerTimer = 0f;
                if (grandmaAI != null && previousState != DogState.Alert && previousState != DogState.Chase)
                    grandmaAI.AlertFromBark(transform.position);
            break;

            case DogState.ReturnToBasket:
                agent.isStopped = false;
                agent.speed = patrolSpeed;
                agent.SetDestination(basket.position);
                Debug.Log("[DogAI] Returning to basket.");
            break;

            case DogState.Stunned:
                agent.isStopped = true;
                Debug.Log("[DogAI] Stunned by hiss!");
            break;

            case DogState.Pinning:
                agent.isStopped = true;
                escapeCounter = escapePressesNeeded;
                Debug.Log("[DogAI] Pinning cat!");
                StartCoroutine(PinRoutine());
            break;

            case DogState.Carrying:
                agent.isStopped = false;
                agent.speed = patrolSpeed;
                if (playerTransform != null)
                {
                    Transform parent = mouthTransform != null ? mouthTransform : transform;
                    playerTransform.SetParent(parent);
                    playerTransform.localPosition = Vector3.zero;
                    playerHealth?.SetCarried(true);
                }
                if (frontDoor != null)
                    agent.SetDestination(frontDoor.position);
                Debug.Log("[DogAI] Carrying cat to front door.");
            break;

            case DogState.Escorting:
                agent.isStopped = false;
                agent.speed = escortSpeed;
                Debug.Log("[DogAI] Escorting grandma.");
            break;
        }
    }

    private void UpdateAnimator()
    {
        if (animator == null) return;

        float commanded = Mathf.Max(0.01f, agent.speed);
        float vert = Mathf.Clamp01(agent.velocity.magnitude / commanded);
        animator.SetFloat(VertHash, vert);

        float state = agent.speed > patrolSpeed + 0.5f ? 1f : 0f;
        animator.SetFloat(StateHash, state);

        _animLogTimer += Time.deltaTime;
        if (_animLogTimer >= 0.5f)
        {
            _animLogTimer = 0f;
            float vertReadback = animator.GetFloat(VertHash);
            float stateReadback = animator.GetFloat(StateHash);
            Debug.Log($"[DogAI Anim] State={CurrentState} | agent.speed={agent.speed:F2} velocity={agent.velocity.magnitude:F2} | Set Vert={vert:F2} (readback {vertReadback:F2}) State={state:F0} (readback {stateReadback:F0})");
        }
    }

    private void UpdateEscorting()
    {
        if (escortTarget == null)
        {
            EnterState(DogState.ReturnToBasket);
            return;
        }

        if (CanSeePlayer())
        {
            lastKnownPlayerPos = playerTransform.position;
            EnterState(DogState.Alert);
            return;
        }

        Vector3 behindGrandma = escortTarget.position - escortTarget.forward * escortOffset;
        agent.SetDestination(behindGrandma);
    }

    private void UpdateResting()
    {
        stateTimer += Time.deltaTime;

        if (CanSeePlayer())
        {
            lastKnownPlayerPos = playerTransform.position;
            EnterState(DogState.Alert);
            return;
        }

        if (stateTimer >= currentRestDuration)
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
            if (sweepQueue.Count > 0)
            {
                if (pausingAtSweepPoint)
                {
                    sweepPauseTimer += Time.deltaTime;
                    if (sweepPauseTimer >= sweepPauseTime)
                    {
                        pausingAtSweepPoint = false;
                        sweepQueue.Clear();
                        waitingAtWaypoint = false;
                        GoToNextWaypoint();
                    }
                    return;
                }
                if (!agent.pathPending && agent.remainingDistance < 0.4f)
                {
                    pausingAtSweepPoint = true;
                    sweepPauseTimer = 0f;
                }
                return;
            }

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
            pausingAtSweepPoint = false;

            if (Random.value < patrolSweepChance)
            {
                QueueSweepPoints(transform.position, sweepRadius * 0.5f, 1);
                if (sweepQueue.Count > 0)
                    agent.SetDestination(sweepQueue.Dequeue());
            }

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
                sweepQueue.Clear();
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
            sweepQueue.Clear();
            alertBuildupTimer = 0f;
            EnterState(DogState.Patrol);
            return;
        }

        if (sweepQueue.Count > 0)
        {
            if (pausingAtSweepPoint)
            {
                sweepPauseTimer += Time.deltaTime;
                if (sweepPauseTimer >= sweepPauseTime)
                {
                    pausingAtSweepPoint = false;
                    if (sweepQueue.Count > 0)
                        agent.SetDestination(sweepQueue.Dequeue());
                }
                return;
            }
            if (!agent.pathPending && agent.remainingDistance < 0.4f)
            {
                pausingAtSweepPoint = true;
                sweepPauseTimer = 0f;
            }
            return;
        }

        if (!generatedSweepPoints && !agent.pathPending && agent.remainingDistance < 0.4f)
        {
            generatedSweepPoints = true;
            QueueSweepPoints(lastHeardPosition, sweepRadius, sweepPointCount);
            if (sweepQueue.Count > 0)
                stateTimer = 0;
                agent.SetDestination(sweepQueue.Dequeue());
        }
    }

    private void UpdateAlert()
    {
        if (playerTransform == null) return;

        if (CanSeePlayer())
        {
            lastKnownPlayerPos = playerTransform.position;
            losePlayerTimer = 0f;
        }
        else
        {
            float distToLastKnown = Vector3.Distance(transform.position, lastKnownPlayerPos);
            
            if (distToLastKnown < 1.5f)
            {
                losePlayerTimer += Time.deltaTime;
                if (losePlayerTimer >= losePlayerTime)
                {
                    lastHeardPosition = lastKnownPlayerPos;
                    EnterState(DogState.Suspicious);
                    return;
                }
            }
        }

        agent.SetDestination(lastKnownPlayerPos);

        if (Vector3.Distance(transform.position, playerTransform.position) <= catchDistance)
            EnterState(DogState.Pinning);
    }

    private void UpdateReturnToBasket()
    {
        if (CanSeePlayer())
        {
            lastKnownPlayerPos = playerTransform.position;
            EnterState(DogState.Alert);
            return;
        }

        if (!agent.pathPending && agent.remainingDistance <= basketArrivalRadius)
            EnterState(DogState.Resting);
    }

    private void UpdatePinning()
    {
    }

    private void UpdateCarrying()
    {
        if (frontDoor == null) return;

        if (!agent.pathPending && agent.remainingDistance < 0.4f)
        {
            if (playerTransform != null)
            {
                playerTransform.SetParent(null);
                playerHealth?.SetCarried(false);
            }
            Debug.Log("[DogAI] Cat thrown out the front door!");
            GameManager.Instance?.ReloadScene();
        }
    }

    private IEnumerator PinRoutine()
    {
        yield return new WaitForSeconds(firstBiteDelay);
        if (CurrentState != DogState.Pinning) yield break;

        playerHealth?.TakeDamage(1f);
        Debug.Log("[DogAI] First bite!");

        if (playerHealth != null && playerHealth.CurrentHealth <= 0f)
        {
            EnterState(DogState.Carrying);
            yield break;
        }

        float timer = 0f;
        while (timer < secondBiteDelay && CurrentState == DogState.Pinning)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        if (CurrentState != DogState.Pinning) yield break;

        playerHealth?.TakeDamage(1f);
        Debug.Log("[DogAI] Second bite!");

        if (playerHealth != null && playerHealth.CurrentHealth <= 0f)
        {
            EnterState(DogState.Carrying);
            yield break;
        }

        Debug.Log("[DogAI] Shaking cat.");
        yield return new WaitForSeconds(shakeDelay);

        Debug.Log("[DogAI] Dog releases cat.");
        lastKnownPlayerPos = playerTransform != null ? playerTransform.position : transform.position;
        losePlayerTimer = 0f;
        EnterState(DogState.Alert);
    }

    private void QueueSweepPoints(Vector3 center, float radius, int count)
    {
        if (NavMesh.SamplePosition(center, out NavMeshHit centerHit, 3f, NavMesh.AllAreas))
            center = centerHit.position;

        for (int i = 0; i < count; i++)
        {
            Vector2 rand = Random.insideUnitCircle.normalized * Random.Range(radius * 0.4f, radius);
            Vector3 candidate = center + new Vector3(rand.x, 0f, rand.y);
            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, radius * 1.5f, NavMesh.AllAreas))
                sweepQueue.Enqueue(hit.position);
        }
        Debug.Log($"[DogAI] Sweep: generated {sweepQueue.Count}/{count} points around {center}");
    }

    private void Escalate()
    {
        if (escalationLevel >= maxEscalationSteps) return;
        escalationLevel++;
        viewAngle = Mathf.Min(viewAngle + escalationViewAngleIncrease, 120f);
        timeToAlert = Mathf.Max(timeToAlert - escalationTimeToAlertDecrease, 0.3f);
        losePlayerTime = Mathf.Max(losePlayerTime - escalationLosePlayerTimeDecrease, 1.5f);
        Debug.Log($"[DogAI] Escalated to level {escalationLevel} — viewAngle={viewAngle:F0} timeToAlert={timeToAlert:F1} losePlayerTime={losePlayerTime:F1}");
    }

    public void TryEscape()
    {
        if (CurrentState != DogState.Pinning) return;

        escapeCounter--;
        Debug.Log("[DogAI] Escape attempt — " + escapeCounter + " presses remaining.");

        if (escapeCounter <= 0)
        {
            Debug.Log("[DogAI] Cat escaped the pin!");
            Escalate();
            StopAllCoroutines();
            EnterState(DogState.Alert);
        }
    }

    public void Stun(float duration)
    {
        if (CurrentState == DogState.Carrying) return;
        Escalate();
        StopAllCoroutines();
        StartCoroutine(StunRoutine(duration));
    }

    public void AlertFromGrandma(Vector3 catPosition)
    {
        if (CurrentState == DogState.Stunned ||
            CurrentState == DogState.Carrying ||
            CurrentState == DogState.Pinning) return;

        lastKnownPlayerPos = catPosition;
        lastHeardPosition = catPosition;
        EnterState(DogState.Alert);
    }

    public void StartEscort(Transform target)
    {
        if (CurrentState == DogState.Stunned ||
            CurrentState == DogState.Carrying ||
            CurrentState == DogState.Pinning ||
            CurrentState == DogState.Alert ||
            CurrentState == DogState.Chase) return;

        escortTarget = target;
        EnterState(DogState.Escorting);
    }

    public void StopEscort()
    {
        if (CurrentState != DogState.Escorting) return;
        escortTarget = null;
        EnterState(DogState.ReturnToBasket);
    }

    private IEnumerator StunRoutine(float duration)
    {
        EnterState(DogState.Stunned);
        yield return new WaitForSeconds(duration);
        EnterState(DogState.ReturnToBasket);
    }

    private bool CanSeePlayer()
    {
        if (playerTransform == null) return false;
        if (CurrentState == DogState.Pinning || CurrentState == DogState.Carrying) return false;

        float dist = Vector3.Distance(transform.position, playerTransform.position);
        float range = (CurrentState == DogState.Alert || CurrentState == DogState.Chase)
            ? alertViewDistance : viewDistance;
        if (dist > range) return false;

        Vector3 origin = transform.position + Vector3.up * 0.8f;
        Vector3 target = playerTransform.position + Vector3.up * 0.3f;
        Vector3 dir = (target - origin).normalized;
        float castDist = Vector3.Distance(origin, target);

        if (Vector3.Angle(transform.forward, dir) > viewAngle) return false;

        Debug.DrawRay(origin, dir * castDist, Color.red, 0.1f);
        if (Physics.Raycast(origin, dir, out RaycastHit hit, castDist, obstructionMask))
        {
            return false;
        }

        return true;
    }

    private void HandleNoise(Vector3 noisePos, float radius, NoiseType type)
    {
        if (CurrentState == DogState.Stunned) return;
        if (CurrentState == DogState.Pinning) return;
        if (CurrentState == DogState.Carrying) return;
        if (CurrentState == DogState.Resting && type != NoiseType.Sprint && type != NoiseType.Land) return;

        float dist = Vector3.Distance(transform.position, noisePos);
        if (dist > radius) return;

        lastHeardPosition = noisePos;

        bool isVeryClose = dist <= alertSoundRadius && (type == NoiseType.Sprint || type == NoiseType.Land || type == NoiseType.ObjectKnocked);
        bool isAlreadySearching = CurrentState == DogState.Alert || CurrentState == DogState.Chase;

        if (isVeryClose || isAlreadySearching)
        {
            if (CurrentState != DogState.Alert && CurrentState != DogState.Chase)
                EnterState(DogState.Alert);
        }
        else if (CurrentState == DogState.Patrol ||
                 CurrentState == DogState.Resting ||
                 CurrentState == DogState.ReturnToBasket)
        {
            EnterState(DogState.Suspicious);
        }
    }

    private void GoToNextWaypoint()
    {
        if (waypoints == null || waypoints.Length == 0) return;
        Debug.Log("[DogAI] Moving to waypoint " + currentWaypointIndex);
        agent.SetDestination(waypoints[currentWaypointIndex].position);
        currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, viewDistance);
        Vector3 left = Quaternion.Euler(0, -viewAngle, 0) * transform.forward;
        Vector3 right = Quaternion.Euler(0, viewAngle, 0) * transform.forward;
        Gizmos.DrawRay(transform.position, left * viewDistance);
        Gizmos.DrawRay(transform.position, right * viewDistance);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, catchDistance);

        if (basket != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(basket.position, basketArrivalRadius);
        }

        if (frontDoor != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireCube(frontDoor.position, Vector3.one * 0.5f);
        }

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