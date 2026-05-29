using UnityEngine;

[RequireComponent(typeof(Animator))]
public class AnimationStateController : MonoBehaviour
{
    private Animator anim;
    private PlayerController player;
    private Rigidbody playerRb;

    private static readonly int VertHash = Animator.StringToHash("Vert");
    private static readonly int StateHash = Animator.StringToHash("State");

    [Header("Animation Tuning")]
    [Tooltip("World speed at which the walk clip looks natural. Tune until feet stop sliding.")]
    public float walkReferenceSpeed = 1.5f;
    [Tooltip("World speed at which the run clip looks natural. Tune until feet stop sliding.")]
    public float runReferenceSpeed = 3.5f;
    [Tooltip("How quickly playback speed catches up to changes (higher = snappier).")]
    public float speedSmoothing = 8f;

    [Header("Animation Smoothing")]
    [Tooltip("How long Vert takes to catch up to its target (seconds). Higher = smoother but laggier.")]
    public float vertDampTime = 0.12f;
    [Tooltip("How long State (walk↔run) takes to catch up to its target (seconds).")]
    public float stateDampTime = 0.15f;

    private void Start()
    {
        anim = GetComponent<Animator>();
        player = GetComponentInParent<PlayerController>();
        if (player == null)
        {
            Debug.LogWarning("AnimationStateController: No PlayerController found in parent hierarchy.");
            return;
        }
        playerRb = player.GetComponent<Rigidbody>();
    }

    private void Update()
    {
        if (player == null || anim == null || playerRb == null) return;

        Vector3 horizontalVel = playerRb.linearVelocity;
        horizontalVel.y = 0f;
        float speed = horizontalVel.magnitude;

        float reference = player.IsSprinting ? player.sprintSpeed : player.walkSpeed;
        float vert = Mathf.Clamp01(speed / Mathf.Max(0.01f, reference));
        anim.SetFloat(VertHash, vert, vertDampTime, Time.deltaTime);


        float state = player.IsSprinting ? 1f : 0f;
        anim.SetFloat(StateHash, state, stateDampTime, Time.deltaTime);

        float playbackReference = player.IsSprinting ? runReferenceSpeed : walkReferenceSpeed;
        float targetSpeed = speed > 0.1f
            ? Mathf.Clamp(speed / playbackReference, 0.5f, 3f):1f;
        anim.speed = Mathf.Lerp(anim.speed, targetSpeed, Time.deltaTime * speedSmoothing);
    }
}