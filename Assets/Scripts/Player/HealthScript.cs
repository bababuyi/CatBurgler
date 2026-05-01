using UnityEngine;

public class HealthScript : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Number of hits the cat can take before dying.")]
    public float maxHealth = 9f;

    public float CurrentHealth { get; private set; }

    public event System.Action<float, float> OnHealthChanged;
    public event System.Action OnDeath;

    private RespawnMenu respawnMenu;
    private bool isDead;

    private void Awake()
    {
        CurrentHealth = maxHealth;
    }

    private void Start()
    {
        respawnMenu = FindObjectOfType<RespawnMenu>();
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

        CurrentHealth = Mathf.Max(0f, CurrentHealth--);
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);

        if (CurrentHealth <= 0f) Die();
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;
        OnDeath?.Invoke();

        if (respawnMenu != null)
            respawnMenu.Show();
        else
            GameManager.Instance?.ReloadScene();
    }
}
