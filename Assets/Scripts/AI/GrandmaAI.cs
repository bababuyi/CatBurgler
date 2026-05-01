using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Grandma NPC — the secondary hazard.
/// She patrols a fixed route slowly and is deaf (does not respond to noise).
/// If she spots the player it is an instant game over — no second chances.
/// This creates a different kind of tension from the dog; she's predictable
/// but unforgiving, so the player must time their movement around her route.
///
/// SETUP CHECKLIST:
///   1. Add NavMeshAgent component.
///   2. Assign waypoints[] along her route (kitchen → living room → back, etc.)
///   3. Set obstructionMask to wall/furniture layers.
///   4. Player must have the "Player" tag.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class GrandmaAI : MonoBehaviour
{
    [Header("Patrol")]
    public Transform[] waypoints;
    public float walkSpeed = 1.1f;
    [Tooltip("How long grandma lingers at each stop (checking the fridge, etc.)")]
    public float waypointWaitTime = 3f;

    [Header("Vision")]
    [Tooltip("She can't see very far — she left her glasses somewhere.")]
    public float viewDistance = 5f;
    [Tooltip("Narrow cone — she has tunnel vision.")]
    public float viewAngle = 45f;
    public LayerMask obstructionMask;

    private NavMeshAgent agent;
    private Transform playerTransform;
    private int waypointIndex;
    private float waitTimer;
    private bool waiting;
    private bool triggered;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.speed = walkSpeed;

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player) playerTransform = player.transform;
    }

    private void Start()
    {
        if (waypoints.Length > 0)
            agent.SetDestination(waypoints[0].position);
    }

    private void Update()
    {
        if (triggered) return;

        if (CanSeePlayer())
        {
            triggered = true;
            agent.isStopped = true;
            // Grandma sees you — treat as instant death / caught
            FindObjectOfType<RespawnMenu>()?.Show();
            return;
        }

        HandlePatrol();
    }

    private void HandlePatrol()
    {
        if (waypoints == null || waypoints.Length == 0) return;

        if (waiting)
        {
            waitTimer += Time.deltaTime;
            if (waitTimer >= waypointWaitTime)
            {
                waiting = false;
                waypointIndex = (waypointIndex + 1) % waypoints.Length;
                agent.SetDestination(waypoints[waypointIndex].position);
            }
            return;
        }

        if (!agent.pathPending && agent.remainingDistance < 0.4f)
        {
            waiting = true;
            waitTimer = 0f;
        }
    }

    private bool CanSeePlayer()
    {
        if (playerTransform == null) return false;

        float dist = Vector3.Distance(transform.position, playerTransform.position);
        if (dist > viewDistance) return false;

        Vector3 dir = (playerTransform.position - transform.position).normalized;
        if (Vector3.Angle(transform.forward, dir) > viewAngle) return false;

        // Raycast from approximate eye height
        Vector3 origin = transform.position + Vector3.up * 1.4f;
        if (Physics.Raycast(origin, dir, dist, obstructionMask)) return false;

        return true;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, viewDistance);

        Vector3 left  = Quaternion.Euler(0, -viewAngle, 0) * transform.forward;
        Vector3 right = Quaternion.Euler(0,  viewAngle, 0) * transform.forward;
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.9f);
        Gizmos.DrawRay(transform.position + Vector3.up * 1.4f, left  * viewDistance);
        Gizmos.DrawRay(transform.position + Vector3.up * 1.4f, right * viewDistance);

        if (waypoints != null && waypoints.Length > 1)
        {
            Gizmos.color = Color.magenta;
            for (int i = 0; i < waypoints.Length; i++)
            {
                if (waypoints[i] == null) continue;
                Gizmos.DrawSphere(waypoints[i].position, 0.15f);
                Gizmos.DrawLine(waypoints[i].position,
                    waypoints[(i + 1) % waypoints.Length].position);
            }
        }
    }
}
