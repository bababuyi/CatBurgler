using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages all HUD elements by listening to game events.
/// Uses event subscriptions rather than polling — no Update() overhead.
///
/// CANVAS SETUP:
///   - healthIcons[]     : array of heart/paw Image components
///   - foodCounterText   : "Food: 0 / 5" TMP label
///   - hissChargeIcons[] : array of Image components for hiss charges
///   - hissRegenBar      : a fill Image showing regen progress (filled = Image)
///   - allFoodBanner     : brief "All food collected!" panel (auto-hides)
/// </summary>
public class HUDController : MonoBehaviour
{
    [Header("Health")]
    public Image[] healthIcons;

    [Header("Food Counter")]
    public TMP_Text foodCounterText;
    public GameObject allFoodCollectedBanner;

    [Header("Hiss Charges")]
    public Image[] hissChargeIcons;
    [Tooltip("A fill-type Image that fills up as the next charge regenerates.")]
    public Image hissRegenBar;

    private HealthScript playerHealth;
    private PlayerCombat playerCombat;
    private GameManager gm;

    private void Start()
    {
        gm            = GameManager.Instance;
        playerHealth  = FindObjectOfType<HealthScript>();
        playerCombat  = FindObjectOfType<PlayerCombat>();

        // Subscribe to events
        if (playerHealth != null) playerHealth.OnHealthChanged += UpdateHealth;
        if (playerCombat != null) playerCombat.OnChargesChanged += UpdateHissCharges;
        if (gm != null)
        {
            gm.OnFoodCollected    += UpdateFoodCounter;
            gm.OnAllFoodCollected += ShowAllFoodBanner;
        }

        // Set initial state
        if (gm != null) UpdateFoodCounter(gm.CollectedFood, gm.totalFoodItems);
        if (playerCombat != null) UpdateHissCharges(playerCombat.CurrentCharges);
        if (playerHealth != null) UpdateHealth(playerHealth.CurrentHealth, playerHealth.CurrentHealth);

        if (allFoodCollectedBanner) allFoodCollectedBanner.SetActive(false);
    }

    private void OnDestroy()
    {
        if (playerHealth != null) playerHealth.OnHealthChanged  -= UpdateHealth;
        if (playerCombat != null) playerCombat.OnChargesChanged -= UpdateHissCharges;
        if (gm != null)
        {
            gm.OnFoodCollected    -= UpdateFoodCounter;
            gm.OnAllFoodCollected -= ShowAllFoodBanner;
        }
    }

    private void Update()
    {
        // Regen bar needs per-frame update since it's a continuous fill
        if (hissRegenBar != null && playerCombat != null)
            hissRegenBar.fillAmount = playerCombat.RegenProgress;
    }

    // ── Event Handlers ────────────────────────────────────────────────────────

    private void UpdateHealth(float current, float max)
    {
        for (int i = 0; i < healthIcons.Length; i++)
            if (healthIcons[i]) healthIcons[i].enabled = i < current;
    }

    private void UpdateFoodCounter(int current, int total)
    {
        if (foodCounterText) foodCounterText.text = $"Food: {current} / {total}";
    }

    private void UpdateHissCharges(int charges)
    {
        for (int i = 0; i < hissChargeIcons.Length; i++)
            if (hissChargeIcons[i]) hissChargeIcons[i].enabled = i < charges;
    }

    private void ShowAllFoodBanner()
    {
        if (!allFoodCollectedBanner) return;
        allFoodCollectedBanner.SetActive(true);
        Invoke(nameof(HideAllFoodBanner), 3f);
    }

    private void HideAllFoodBanner()
    {
        if (allFoodCollectedBanner) allFoodCollectedBanner.SetActive(false);
    }
}
