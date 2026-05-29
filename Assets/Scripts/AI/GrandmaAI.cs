using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class GrandmaAI : MonoBehaviour
{
    public enum GrandmaState
    {
        WatchingTV,
        GetWater,
        ClosingWindows,
        Spotted,
        Kicking,
        Fleeing,
        Cautious,
        Carrying
    }

    [Header("Routine")]
    [Tooltip("Where Grandma sits at the start of the game.")]
    public Transform sofaPosition;
    [Tooltip("Where she goes to get water before closing windows.")]
    public Transform kitchenWaterPoint;
    [Tooltip("How long she watches TV before starting her routine.")]
    public float tvWatchDuration = 60f;
    [Tooltip("How long she pauses at the kitchen before heading to windows.")]
    public float waterDrinkDuration = 2f;

    [Header("Windows")]
    [Tooltip("Windows in the order she closes them — this is the game timer.")]
    public Transform[] windowTransforms;
    [Tooltip("How long she takes to close each window.")]
    public float windowCloseDuration = 1.5f;

    [Header("Movement")]
    public float walkSpeed = 1.1f;

    [Header("Vision")]
    [Tooltip("Short range — she lost her glasses.")]
    public float viewDistance = 5f;
    [Tooltip("Narrow cone — tunnel vision.")]
    public float viewAngle = 45f;
    public LayerMask obstructionMask;

    [Header("Vision Buildup")]
    [Tooltip("Seconds grandma must continuously see the cat before screaming.")]
    public float spotBuildupThreshold = 0.8f;

    [Header("Noise Detection")]
    [Tooltip("Only very loud noises (radius of the noise must exceed this to alert her).")]
    public float loudNoiseThreshold = 8f;

    [Header("Kick")]
    [Tooltip("How close the cat must be for her to kick it.")]
    public float kickRange = 1.5f;
    [Tooltip("Force applied to the cat's Rigidbody when kicked.")]
    public float kickForce = 10f;
    [Tooltip("Seconds between kick attempts.")]
    public float kickCooldown = 2f;
    [Tooltip("How far the cat must get before she gives up chasing.")]
    public float giveUpDistance = 8f;

    [Header("Cautious (Dog Bark Response)")]
    [Tooltip("How long she looks around at the bark location before giving up.")]
    public float cautiousLookDuration = 5f;

    [Tooltip("Room centres grandma may check after a dog bark.")]
    public Transform[] roomCheckPoints;
    [Tooltip("How many of the nearest rooms she checks.")]
    public int cautiousRoomChecks = 2;
    [Tooltip("Seconds she pauses at each room check point.")]
    public float cautiousRoomPauseTime = 2f;

    [Header("Carrying")]
    [Tooltip("Where the cat is parented when she carries it — e.g. her hand bone.")]
    public Transform handTransform;
    [Tooltip("The front door — where she throws the cat out.")]
    public Transform frontDoor;

    [Header("References")]
    [Tooltip("Assign the DogAI so she can call the dog when she spots the cat.")]
    public DogAI dogAI;

    public GrandmaState currentState = GrandmaState.WatchingTV;
    public event System.Action OnAllWindowsClosed;

    private NavMeshAgent agent;
    private Transform playerTransform;
    private Rigidbody playerRigidbody;
    private HealthScript playerHealth;
    private float tvTimer;
    private float waterTimer;
    private float windowTimer;
    private bool atWindow;
    private int currentWindowIndex;
    private float kickTimer;
    private bool isSpottedRoutineRunning;
    private GrandmaState stateBeforeInterruption;
    private Vector3 barkPosition;
    private float cautiousTimer;
    private bool arrivedAtBarkPos;
    private Queue<Vector3> cautiousCheckQueue = new Queue<Vector3>();
    private bool headingToRoomCheck;
    private bool pausingAtCheckPoint;
    private float checkPointPauseTimer;
    private float spotBuildupTimer;
    private Vector3 lastSpottedPosition;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.speed = walkSpeed;
        agent.isStopped = true;

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player)
        {
            playerTransform = player.transform;
            playerRigidbody = player.GetComponent<Rigidbody>();
            playerHealth = player.GetComponent<HealthScript>();
        }
    }

    private void Start()
    {
        if (sofaPosition != null)
        {
            transform.position = sofaPosition.position;
            transform.rotation = sofaPosition.rotation;
        }

        if (frontDoor == null)
            Debug.LogWarning("GrandmaAI: No front door assigned.");

        Debug.Log("Grandma: Watching TV.");
    }

    private void Update()
    {
        kickTimer += Time.deltaTime;

        bool visionBlocked = currentState == GrandmaState.Spotted ||
                             currentState == GrandmaState.Kicking ||
                             currentState == GrandmaState.Fleeing ||
                             currentState == GrandmaState.Carrying;

        if (!visionBlocked)
        {
            if (CanSeePlayer())
            {
                spotBuildupTimer += Time.deltaTime;
                lastSpottedPosition = playerTransform.position;
                if (spotBuildupTimer >= spotBuildupThreshold)
                {
                    spotBuildupTimer = 0f;
                    EnterSpottedState();
                    return;
                }
            }
            else if (spotBuildupTimer > 0.15f)
            {
                Debug.Log("Grandma: Was that a cat?");
                spotBuildupTimer = 0f;
                if (currentState != GrandmaState.Cautious)
                {
                    stateBeforeInterruption = currentState;
                    arrivedAtBarkPos = false;
                    headingToRoomCheck = false;
                    cautiousCheckQueue.Clear();
                    cautiousTimer = 0f;
                    pausingAtCheckPoint = false;
                    currentState = GrandmaState.Cautious;
                    agent.isStopped = false;
                    agent.speed = walkSpeed;
                    agent.SetDestination(lastSpottedPosition);
                }
            }
            else
            {
                spotBuildupTimer = Mathf.Max(0f, spotBuildupTimer - Time.deltaTime);
            }
        }

        switch (currentState)
        {
            case GrandmaState.WatchingTV: HandleWatchingTV(); break;
            case GrandmaState.GetWater: HandleGetWater(); break;
            case GrandmaState.ClosingWindows: HandleClosingWindows(); break;
            case GrandmaState.Kicking: HandleKicking(); break;
            case GrandmaState.Fleeing: HandleFleeing(); break;
            case GrandmaState.Cautious: HandleCautious(); break;
            case GrandmaState.Carrying: HandleCarrying(); break;
        }
    }

    private void HandleWatchingTV()
    {
        tvTimer += Time.deltaTime;
        if (tvTimer >= tvWatchDuration)
        {
            Debug.Log("Grandma: Getting up to get water.");
            agent.isStopped = false;
            agent.SetDestination(kitchenWaterPoint.position);
            currentState = GrandmaState.GetWater;
        }
    }

    private void HandleGetWater()
    {
        if (agent.pathPending || agent.remainingDistance >= 0.4f) return;

        waterTimer += Time.deltaTime;
        if (waterTimer >= waterDrinkDuration)
        {
            waterTimer = 0f;
            if (windowTransforms != null && windowTransforms.Length > 0)
            {
                currentWindowIndex = 0;
                agent.SetDestination(windowTransforms[0].position);
                currentState = GrandmaState.ClosingWindows;
                dogAI?.StartEscort(transform);
                Debug.Log("Grandma: Going to close the windows.");
            }
        }
    }

    private void HandleClosingWindows()
    {
        if (atWindow)
        {
            windowTimer += Time.deltaTime;
            if (windowTimer >= windowCloseDuration)
            {
                windowTimer = 0f;
                atWindow = false;

                CloseWindow(currentWindowIndex);
                currentWindowIndex++;

                if (currentWindowIndex >= windowTransforms.Length)
                {
                    Debug.Log("Grandma: All windows closed.");
                    OnAllWindowsClosed?.Invoke();
                }
                else
                {
                    agent.SetDestination(windowTransforms[currentWindowIndex].position);
                    Debug.Log($"Grandma: Moving to window {currentWindowIndex}.");
                }
            }
            return;
        }

        if (!agent.pathPending && agent.remainingDistance < 0.4f)
        {
            atWindow = true;
            windowTimer = 0f;
            Debug.Log($"Grandma: Closing window {currentWindowIndex}.");
        }
    }

    private void EnterSpottedState()
    {
        if (isSpottedRoutineRunning) return;

        stateBeforeInterruption = currentState;

        currentState = GrandmaState.Spotted;
        agent.isStopped = true;
        Debug.Log("Grandma: *SCREAMS* — CAT!");

        if (dogAI != null)
            dogAI.AlertFromGrandma(playerTransform.position);

        isSpottedRoutineRunning = true;
        StartCoroutine(SpottedRoutine());
    }

    private IEnumerator SpottedRoutine()
    {
        yield return new WaitForSeconds(1.2f);

        isSpottedRoutineRunning = false;

        currentState = GrandmaState.Kicking;
        agent.isStopped = false;
        agent.speed = walkSpeed * 1.5f;
        agent.SetDestination(playerTransform.position);
        Debug.Log("Grandma: Shuffling toward cat to kick it.");
    }

    private void HandleKicking()
    {
        if (playerTransform == null) return;

        agent.SetDestination(playerTransform.position);

        float dist = Vector3.Distance(transform.position, playerTransform.position);

        if (dist <= kickRange && kickTimer >= kickCooldown)
            TryKick();

        if (dist > giveUpDistance)
        {
            Debug.Log("Grandma: Lost the cat.");
            agent.speed = walkSpeed;
            currentState = GrandmaState.Fleeing;
            agent.SetDestination(sofaPosition.position);
        }
    }

    private void TryKick()
    {
        kickTimer = 0f;
        Debug.Log("Grandma: *KICK*");

        if (playerRigidbody != null)
        {
            Vector3 dir = (playerTransform.position - transform.position).normalized;
            dir.y = 0.3f;
            playerRigidbody.AddForce(dir * kickForce, ForceMode.Impulse);
        }

        if (playerHealth != null && playerHealth.CurrentHealth <= 0f)
        {
            EnterCarryingState();
            return;
        }

        currentState = GrandmaState.Fleeing;
        agent.speed = walkSpeed * 2f;
        agent.SetDestination(sofaPosition.position);
        Debug.Log("Grandma: *RUNS AWAY*");
    }

    private void HandleFleeing()
    {
        if (agent.pathPending || agent.remainingDistance >= 0.4f) return;

        agent.speed = walkSpeed;
        agent.isStopped = true;

        if (stateBeforeInterruption == GrandmaState.ClosingWindows &&
            windowTransforms != null &&
            currentWindowIndex < windowTransforms.Length)
        {
            Debug.Log("Grandma: Calmed down. Resuming window closing.");
            agent.isStopped = false;
            atWindow = false;
            agent.SetDestination(windowTransforms[currentWindowIndex].position);
            currentState = GrandmaState.ClosingWindows;
            dogAI?.StartEscort(transform);
        }
        else if (stateBeforeInterruption == GrandmaState.GetWater)
        {
            Debug.Log("Grandma: Calmed down. Going back to get water.");
            agent.isStopped = false;
            agent.SetDestination(kitchenWaterPoint.position);
            currentState = GrandmaState.GetWater;
        }
        else
        {
            Debug.Log("Grandma: Made it back to sofa, calming down.");
            currentState = GrandmaState.WatchingTV;
            tvTimer = tvWatchDuration;
        }
    }

    private void HandleCautious()
    {
        if (!arrivedAtBarkPos)
        {
            if (!agent.pathPending && agent.remainingDistance < 0.4f)
            {
                arrivedAtBarkPos = true;
                cautiousTimer = 0f;
                Debug.Log("Grandma: Arrived. Looking around...");
            }
            return;
        }

        cautiousTimer += Time.deltaTime;
        if (cautiousTimer < cautiousLookDuration) return;

        if (!headingToRoomCheck && cautiousCheckQueue.Count > 0)
        {
            headingToRoomCheck = true;
            agent.SetDestination(cautiousCheckQueue.Dequeue());
            return;
        }

        if (headingToRoomCheck)
        {
            if (pausingAtCheckPoint)
            {
                checkPointPauseTimer += Time.deltaTime;
                if (checkPointPauseTimer >= cautiousRoomPauseTime)
                {
                    pausingAtCheckPoint = false;
                    if (cautiousCheckQueue.Count > 0)
                    {
                        agent.SetDestination(cautiousCheckQueue.Dequeue());
                    }
                    else
                    {
                        Debug.Log("Grandma: Nothing here. Going back to what I was doing.");
                        headingToRoomCheck = false;
                        ResumeAfterCautious();
                    }
                }
                return;
            }

            if (!agent.pathPending && agent.remainingDistance < 0.4f)
            {
                pausingAtCheckPoint = true;
                checkPointPauseTimer = 0f;
            }
            return;
        }

        Debug.Log("Grandma: Nothing here. Going back to what I was doing.");
        ResumeAfterCautious();
    }

    private void ResumeAfterCautious()
    {
        currentState = stateBeforeInterruption;
        switch (stateBeforeInterruption)
        {
            case GrandmaState.WatchingTV:
                agent.isStopped = true;
                break;
            case GrandmaState.GetWater:
                agent.isStopped = false;
                agent.SetDestination(kitchenWaterPoint.position);
                break;
            case GrandmaState.ClosingWindows:
                agent.isStopped = false;
                atWindow = false;
                if (currentWindowIndex < windowTransforms.Length)
                    agent.SetDestination(windowTransforms[currentWindowIndex].position);
                dogAI?.StartEscort(transform);
                break;
            default:
                agent.isStopped = true;
                currentState = GrandmaState.WatchingTV;
                tvTimer = tvWatchDuration;
                break;
        }
    }

    private void EnterCarryingState()
    {
        currentState = GrandmaState.Carrying;
        agent.isStopped = false;
        agent.speed = walkSpeed * 1.8f;

        if (playerTransform != null)
        {
            Transform parent = handTransform != null ? handTransform : transform;
            playerTransform.SetParent(parent);
            playerTransform.localPosition = Vector3.zero;
            playerHealth?.SetCarried(true);
        }

        if (frontDoor != null)
            agent.SetDestination(frontDoor.position);

        Debug.Log("Grandma: Picking up cat and taking it to the door!");
    }

    private void HandleCarrying()
    {
        if (frontDoor == null) return;

        if (!agent.pathPending && agent.remainingDistance < 0.4f)
        {
            if (playerTransform != null)
            {
                playerTransform.SetParent(null);
                playerHealth?.SetCarried(false);
            }
            Debug.Log("Grandma: Cat thrown out!");
            GameManager.Instance?.ReloadScene();
        }
    }

    public void AlertFromBark(Vector3 barkPos)
    {
        if (currentState == GrandmaState.Spotted ||
            currentState == GrandmaState.Kicking ||
            currentState == GrandmaState.Carrying) return;

        Debug.Log("Grandma: What's the dog barking at?!");
        stateBeforeInterruption = currentState;
        barkPosition = barkPos;
        arrivedAtBarkPos = false;
        cautiousTimer = 0f;
        headingToRoomCheck = false;
        pausingAtCheckPoint = false;
        cautiousCheckQueue.Clear();

        if (roomCheckPoints != null && roomCheckPoints.Length > 0)
        {
            var candidates = new List<Transform>(roomCheckPoints);
            candidates.RemoveAll(t => t == null);
            candidates.Sort((a, b) =>
                Vector3.Distance(a.position, barkPos)
                .CompareTo(Vector3.Distance(b.position, barkPos)));

            int count = Mathf.Min(cautiousRoomChecks, candidates.Count);
            for (int i = 0; i < count; i++)
                cautiousCheckQueue.Enqueue(candidates[i].position);
        }

        currentState = GrandmaState.Cautious;
        agent.isStopped = false;
        agent.speed = walkSpeed;
        agent.SetDestination(barkPos);
    }

    public void AlertFromNoise(Vector3 noisePosition, float noiseRadius)
    {
        if (currentState == GrandmaState.Spotted ||
            currentState == GrandmaState.Kicking ||
            currentState == GrandmaState.Carrying) return;

        if (noiseRadius < loudNoiseThreshold) return;

        float dist = Vector3.Distance(transform.position, noisePosition);
        if (dist > noiseRadius) return;

        Debug.Log("Grandma: What was that noise?!");
        agent.isStopped = false;
        agent.SetDestination(noisePosition);
    }

    private bool CanSeePlayer()
    {
        if (playerTransform == null) return false;

        float dist = Vector3.Distance(transform.position, playerTransform.position);
        if (dist > viewDistance) return false;

        Vector3 origin = transform.position + Vector3.up * 1.4f;
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

    private void CloseWindow(int index)
    {
        if (windowTransforms[index] == null) return;

        var blind = windowTransforms[index].GetComponent<BlindUnit>();
        if (blind != null) { blind.Close(); return; }

        var window = windowTransforms[index].GetComponent<Window>();
        if (window != null) { window.Close(); return; }

        var curtain = windowTransforms[index].GetComponent<CurtainWindow>();
        if (curtain != null) { curtain.Close(); return; }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, viewDistance);
        Vector3 left = Quaternion.Euler(0, -viewAngle, 0) * transform.forward;
        Vector3 right = Quaternion.Euler(0, viewAngle, 0) * transform.forward;
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.9f);
        Gizmos.DrawRay(transform.position + Vector3.up * 1.4f, left * viewDistance);
        Gizmos.DrawRay(transform.position + Vector3.up * 1.4f, right * viewDistance);

        Gizmos.color = new Color(1f, 1f, 0f, 0.15f);
        Gizmos.DrawWireSphere(transform.position, loudNoiseThreshold);

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, kickRange);

        if (frontDoor != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireCube(frontDoor.position, Vector3.one * 0.5f);
        }

        if (windowTransforms != null && windowTransforms.Length > 0)
        {
            Gizmos.color = Color.cyan;
            for (int i = 0; i < windowTransforms.Length; i++)
            {
                if (windowTransforms[i] == null) continue;
                Gizmos.DrawWireCube(windowTransforms[i].position, Vector3.one * 0.3f);
                if (i < windowTransforms.Length - 1 && windowTransforms[i + 1] != null)
                    Gizmos.DrawLine(windowTransforms[i].position, windowTransforms[i + 1].position);
            }
        }
    }
}