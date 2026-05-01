using UnityEngine;

/// <summary>
/// Manages the cat's hiss ability.
/// Stuns all dogs within hissRadius and sends them retreating to their basket.
/// Limited charges that regenerate over time, shown in HUD.
///
/// Key bindings: Right Mouse Button or Q
/// </summary>
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

    // ── Public state (read by HUDController) ─────────────────────────────
    public int CurrentCharges { get; private set; }
    public float RegenProgress { get; private set; }   // 0–1

    public event System.Action<int> OnChargesChanged;

    private float regenTimer;

    private void Start()
    {
        CurrentCharges = maxHissCharges;
    }

    private void Update()
    {
        HandleHissInput();
        TickRegeneration();
    }

    private void HandleHissInput()
    {
        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Q))
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

        // Stun every dog in range
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
