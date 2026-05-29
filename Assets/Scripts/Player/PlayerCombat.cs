using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCombat : MonoBehaviour
{
    [Header("Hiss Settings")]
    [Tooltip("How far the hiss reaches.")]
    public float hissRadius = 6f;
    [Tooltip("How long the dog stays stunned after a hiss.")]
    public float hissStunDuration = 4f;
    [Tooltip("Maximum number of hiss charges stored at once.")]
    public int maxHissCharges = 3;
    [Tooltip("Seconds to regenerate one charge.")]
    public float chargeRegenTime = 15f;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip hissClip;

    public int CurrentCharges { get; private set; }
    public float RegenProgress { get; private set; }

    public event System.Action<int> OnChargesChanged;

    private float regenTimer;

    private void Update()
    {
        HandleHissInput();
        TickRegeneration();
    }

    private void Awake()
    {
        CurrentCharges = maxHissCharges;
    }

    private void HandleHissInput()
    {
        if (Keyboard.current.qKey.wasPressedThisFrame)
            TryHiss();
    }

    private void TryHiss()
    {
        if (CurrentCharges <= 0) return;

        CurrentCharges--;
        regenTimer = 0f;
        RegenProgress = 0f;
        OnChargesChanged?.Invoke(CurrentCharges);

        if (audioSource && hissClip)
            audioSource.PlayOneShot(hissClip);

        Collider[] hits = Physics.OverlapSphere(transform.position, hissRadius);
        foreach (Collider hit in hits)
        {
            DogAI dog = hit.GetComponent<DogAI>();
            if (dog != null) dog.Stun(hissStunDuration);
        }
    }

    private void TickRegeneration()
    {
        if (CurrentCharges >= maxHissCharges) return;

        regenTimer += Time.deltaTime;
        RegenProgress = regenTimer / chargeRegenTime;

        if (regenTimer >= chargeRegenTime)
        {
            CurrentCharges++;
            regenTimer = 0f;
            RegenProgress = 0f;
            OnChargesChanged?.Invoke(CurrentCharges);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.25f);
        Gizmos.DrawSphere(transform.position, hissRadius);
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, hissRadius);
    }
}
