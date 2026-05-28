using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class DogAI : MonoBehaviour
{
    private float _animLogTimer;

    public enum DogState
    {
        Resting, Patrol, Suspicious, Alert, Chase,
        ReturnToBasket, Stunned, Pinning, Carrying
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

    [Header("References")]
    [Tooltip("Assign GrandmaAI so the dog can alert her when it barks.")]
    public GrandmaAI grandmaAI;

    public DogState CurrentState { get; private set; } = DogState.Resting;

    private NavMeshAgent agent;
    private Transform playerTransform;
    private HealthScript playerHealth;

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
            case DogState.Stunned: break;
        }
        UpdateAnimator();
    }

    private void EnterState(DogState newState)
    {
        Debug.Log("[DogAI] State: " + CurrentState + " -> " + newState);
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
                Debug.Log("[DogAI] Suspicious — investigating at " + lastHeardPosition);
                break;

            case DogState.Alert:
            case DogState.Chase:
                agent.isStopped = false;
                agent.speed = chaseSpeed;
                losePlayerTimer = 0f;
                if (grandmaAI != null)
                    grandmaAI.AlertFromBark(transform.position);
                Debug.Log("[DogAI] ALERT — chasing player!");
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
                Debug.Log("[DogAI] Lost player — checking last known position.");
                lastHeardPosition = lastKnownPlayerPos;
                agent.SetDestination(lastKnownPlayerPos);
                EnterState(DogState.Suspicious);
                return;
            }
        }

        float distToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        if (distToPlayer <= catchDistance)
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
        EnterState(DogState.Alert);
    }

    public void TryEscape()
    {
        if (CurrentState != DogState.Pinning) return;

        escapeCounter--;
        Debug.Log("[DogAI] Escape attempt — " + escapeCounter + " presses remaining.");

        if (escapeCounter <= 0)
        {
            Debug.Log("[DogAI] Cat escaped the pin!");
            StopAllCoroutines();
            EnterState(DogState.Alert);
        }
    }

    public void Stun(float duration)
    {
        if (CurrentState == DogState.Carrying) return;
        StopAllCoroutines();
        StartCoroutine(StunRoutine(duration));
    }

    public void AlertFromGrandma(Vector3 catPosition)
    {
        if (CurrentState == DogState.Stunned || CurrentState == DogState.Carrying) return;

        lastKnownPlayerPos = catPosition;
        lastHeardPosition = catPosition;
        EnterState(DogState.Alert);
        Debug.Log("[DogAI] Grandma called — rushing to cat's position!");
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

        Vector3 dir = (playerTransform.position - transform.position).normalized;
        if (Vector3.Angle(transform.forward, dir) > viewAngle) return false;

        Vector3 origin = transform.position + Vector3.up * 0.6f;
        if (Physics.Raycast(origin, dir, dist, obstructionMask)) return false;

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

        bool isVeryClose = dist <= alertSoundRadius;
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