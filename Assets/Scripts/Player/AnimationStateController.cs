using UnityEngine;

/// <summary>
/// Drives the cat's Animator from PlayerController state.
/// Uses hashed parameter IDs for performance — no magic strings at runtime.
///
/// ANIMATOR SETUP — create these Bool parameters:
///   IsMoving    — true when the cat is walking or running
///   IsSprinting — true when sprinting
///   IsCrouching — true when crouching
///   IsGrounded  — true when on the ground
/// </summary>
[RequireComponent(typeof(Animator))]
public class AnimationStateController : MonoBehaviour
{
    private Animator anim;
    private PlayerController player;

    // Cache parameter hashes once at startup — faster than string lookup every frame
    private static readonly int IsMovingHash    = Animator.StringToHash("IsMoving");
    private static readonly int IsSprintingHash = Animator.StringToHash("IsSprinting");
    private static readonly int IsCrouchingHash = Animator.StringToHash("IsCrouching");
    private static readonly int IsGroundedHash  = Animator.StringToHash("IsGrounded");

    private void Start()
    {
        anim   = GetComponent<Animator>();
        player = GetComponentInParent<PlayerController>();

        if (player == null)
            Debug.LogWarning("AnimationStateController: No PlayerController found in parent hierarchy.");
    }

    private void Update()
    {
        if (player == null || anim == null) return;

        anim.SetBool(IsMovingHash,    player.IsMoving);
        anim.SetBool(IsSprintingHash, player.IsSprinting);
        anim.SetBool(IsCrouchingHash, player.IsCrouching);
        anim.SetBool(IsGroundedHash,  player.IsGrounded);
    }
}
