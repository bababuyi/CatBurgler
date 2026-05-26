using UnityEngine;

[RequireComponent(typeof(Animator))]
public class AnimationStateController : MonoBehaviour
{
    private Animator anim;
    private PlayerController player;

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
