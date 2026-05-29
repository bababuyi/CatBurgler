using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Ground Check")]
    [Tooltip("Distance below collider bottom to consider grounded. Small positive value.")]
    public float groundCheckDistance = 0.15f;
    public LayerMask groundMask = ~0;

    [Header("Movement Speeds")]
    public float walkSpeed = 4f;
    public float sprintSpeed = 8f;
    public float crouchSpeed = 2f;

    [Header("Jump")]
    [Tooltip("Extra upward force applied each second while jump is held (variable height).")]
    public float jumpHoldForce = 22f;
    [Tooltip("Maximum seconds of hold-boost after pressing jump.")]
    public float maxJumpHoldTime = 0.25f;
    public float jumpForce = 7.5f;
    public float jumpCooldown = 0.3f;
    public float airMultiplier = 0.4f;

    [Header("Ground")]
    public float groundDrag = 5f;

    [Header("Crouch")]
    public float crouchColliderHeight = 0.9f;
    public float standColliderHeight = 1.8f;

    [Header("Noise Radii")]
    public float walkNoiseRadius = 5f;
    public float sprintNoiseRadius = 12f;
    public float landNoiseRadius = 8f;
    public float footstepInterval = 0.45f;

    [Header("References")]
    public Transform orientation;
    public InputActionReference interactAction;
    public DogAI dogAI;

    [Header("Input Actions")]
    public InputActionReference moveAction;
    public InputActionReference jumpAction;
    public InputActionReference sprintAction;
    public InputActionReference crouchAction;

    public bool IsGrounded { get; private set; }
    public bool IsCrouching { get; private set; }
    public bool IsSprinting { get; private set; }
    public bool IsMoving => new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z).sqrMagnitude > 0.1f;

    private Rigidbody rb;
    private CapsuleCollider col;

    private Vector2 moveInput;
    private bool jumpPressed;
    private bool jumpHeld;
    private bool isJumping;
    private float jumpHoldTimer;
    private bool sprintHeld;
    private bool crouchHeld;

    private bool readyToJump = true;
    private float footstepTimer;
    private float lastGroundedY;
    private bool wasGrounded;

    public Vector2 MoveInput => moveInput;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<CapsuleCollider>();
        rb.freezeRotation = true;
    }

    private void OnEnable()
    {
        moveAction.action.Enable();
        jumpAction.action.Enable();
        sprintAction.action.Enable();
        crouchAction.action.Enable();
        interactAction.action.Enable();
        interactAction.action.performed += OnInteract;

        jumpAction.action.performed += OnJump;
    }

    private void OnDisable()
    {
        moveAction.action.Disable();
        jumpAction.action.Disable();
        sprintAction.action.Disable();
        crouchAction.action.Disable();
        interactAction.action.Disable();
        interactAction.action.performed -= OnInteract;

        jumpAction.action.performed -= OnJump;
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        if (GetComponent<HealthScript>().IsBeingCarried) return;
        moveInput = moveAction.action.ReadValue<Vector2>();
        sprintHeld = sprintAction.action.IsPressed();
        crouchHeld = crouchAction.action.IsPressed();
        jumpHeld = jumpAction.action.IsPressed();

        CheckGrounded();
        HandleCrouch();
        HandleDrag();
        SpeedControl();
        EmitFootstepNoise();
        CheckLanding();
    }

    private void FixedUpdate()
    {
        MovePlayer();
        HandleVariableJumpHeight();
    }

    private void MovePlayer()
    {
        float h = moveInput.x;
        float v = moveInput.y;

        IsSprinting = sprintHeld && IsGrounded && !IsCrouching && (h != 0 || v != 0);

        Vector3 dir = orientation.forward * v + orientation.right * h;
        float speed = IsCrouching ? crouchSpeed : IsSprinting ? sprintSpeed : walkSpeed;
        float multiplier = IsGrounded ? 1f : airMultiplier;

        rb.AddForce(dir.normalized * speed * 10f * multiplier, ForceMode.Force);
    }

    private void SpeedControl()
    {
        Vector3 flatVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        float limit = IsCrouching ? crouchSpeed : IsSprinting ? sprintSpeed : walkSpeed;

        if (flatVel.magnitude > limit)
        {
            Vector3 capped = flatVel.normalized * limit;
            rb.linearVelocity = new Vector3(capped.x, rb.linearVelocity.y, capped.z);
        }
    }

    private void HandleDrag()
    {
        rb.linearDamping = IsGrounded ? groundDrag : 0f;
    }

    private void CheckGrounded()
    {
        if (col == null) return;
        Vector3 origin = transform.position + Vector3.up * 0.05f;
        float distance = 0.05f + groundCheckDistance;
        IsGrounded = Physics.Raycast(
            origin, Vector3.down, distance, groundMask, QueryTriggerInteraction.Ignore);
    }

    private void OnJump(InputAction.CallbackContext context)
    {
        if (readyToJump && IsGrounded)
        {
            readyToJump = false;
            isJumping = true;
            jumpHoldTimer = 0f;
            lastGroundedY = transform.position.y;

            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);

            Invoke(nameof(ResetJump), jumpCooldown);
        }
    }

    private void HandleVariableJumpHeight()
    {
        if (!isJumping) return;

        if (jumpHeld && jumpHoldTimer < maxJumpHoldTime && rb.linearVelocity.y > 0f)
        {
            jumpHoldTimer += Time.fixedDeltaTime;
            rb.AddForce(Vector3.up * jumpHoldForce, ForceMode.Force);
        }
        else
        {
            isJumping = false;
        }
    }

    private void ResetJump()
    {
        readyToJump = true;
    }

    private void HandleCrouch()
    {
        if (crouchHeld && !IsCrouching)
        {
            IsCrouching = true;
            col.height = crouchColliderHeight;
            col.center = new Vector3(0f, crouchColliderHeight / 2f, 0f);
        }
        else if (!crouchHeld && IsCrouching)
        {
            if (!Physics.Raycast(transform.position, Vector3.up, standColliderHeight + 0.1f))
            {
                IsCrouching = false;
                col.height = standColliderHeight;
                col.center = new Vector3(0f, standColliderHeight / 2f, 0f);
            }
        }
    }

    private void EmitFootstepNoise()
    {
        if (!IsMoving || !IsGrounded) return;

        footstepTimer -= Time.deltaTime;
        if (footstepTimer > 0f) return;

        if (IsCrouching)
        {
            footstepTimer = footstepInterval * 2f;
        }
        else if (IsSprinting)
        {
            footstepTimer = footstepInterval * 0.55f;
            NoiseSystem.Emit(transform.position, sprintNoiseRadius, NoiseType.Sprint);
        }
        else
        {
            footstepTimer = footstepInterval;
            NoiseSystem.Emit(transform.position, walkNoiseRadius, NoiseType.Footstep);
        }
    }

    private void CheckLanding()
    {
        if (!wasGrounded && IsGrounded)
        {
            float fallDistance = lastGroundedY - transform.position.y;
            if (fallDistance > 1.5f)
            {
                float scaledRadius = landNoiseRadius * Mathf.Clamp01(fallDistance / 6f);
                NoiseSystem.Emit(transform.position, scaledRadius, NoiseType.Land);
            }
        }

        wasGrounded = IsGrounded;
        if (IsGrounded) lastGroundedY = transform.position.y;
    }

    private void OnCollisionStay(Collision collision)
    {
        foreach (ContactPoint contact in collision.contacts)
        {
            if (Vector3.Dot(contact.normal, Vector3.up) > 0.7f)
            {
                IsGrounded = true;
                return;
            }
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        IsGrounded = false;
    }

    private void OnInteract(InputAction.CallbackContext context)
    {
        dogAI?.TryEscape();

        Collider[] nearby = Physics.OverlapSphere(transform.position, 2f);
        foreach (Collider col in nearby)
            col.GetComponent<FoodItem>()?.TryCollect();
    }
}