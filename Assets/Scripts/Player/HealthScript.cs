using UnityEngine;

public class HealthScript : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Number of hits the cat can take before dying.")]
    public float maxHealth = 3f;

    public float CurrentHealth { get; private set; }

    public event System.Action<float, float> OnHealthChanged;
    public event System.Action OnDeath;
    public bool IsBeingCarried { get; private set; }
    public event System.Action OnCarried;

    private RespawnMenu respawnMenu;
    private bool isDead;

    private void Awake()
    {
        CurrentHealth = maxHealth;
    }

    private void Start()
    {
        respawnMenu = FindObjectOfType<RespawnMenu>();
        if (respawnMenu != null) OnDeath += respawnMenu.Show;
    }

    public void ResetHealth()
    {
        isDead = false;
        CurrentHealth = maxHealth;
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    public void TakeDamage(float amount)
    {
        if (isDead || amount <= 0f) return;

        CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);

        if (CurrentHealth <= 0f) Die();
    }

    public void SetCarried(bool carried)
    {
        IsBeingCarried = carried;
        if (carried) OnCarried?.Invoke();
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;
        OnDeath?.Invoke();
    }
}
