using UnityEngine;
using UnityEngine.UI;
using TMPro;

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

        if (playerHealth != null) playerHealth.OnHealthChanged += UpdateHealth;
        if (playerCombat != null) playerCombat.OnChargesChanged += UpdateHissCharges;
        if (gm != null)
        {
            gm.OnFoodCollected    += UpdateFoodCounter;
            gm.OnAllFoodCollected += ShowAllFoodBanner;
        }

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
        if (hissRegenBar != null && playerCombat != null)
        {
            bool isRegening = playerCombat.CurrentCharges < playerCombat.maxHissCharges;
            if (hissRegenBar.gameObject.activeSelf != isRegening)
                hissRegenBar.gameObject.SetActive(isRegening);
            if (isRegening)
                hissRegenBar.fillAmount = playerCombat.RegenProgress;
        }
    }

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
