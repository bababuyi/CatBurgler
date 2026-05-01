using UnityEngine;

/// <summary>
/// The cat's bed — the level exit.
/// Stays locked (visually and functionally) until all food items are collected.
/// Triggers the win condition when the player steps into it.
///
/// SETUP:
///   - Add a Trigger Collider.
///   - Assign lockedVisual (e.g., a padlock particle or dim glow) and
///     unlockedVisual (e.g., a warm glowing bed) in the inspector.
/// </summary>
public class LevelExit : MonoBehaviour
{
    [Header("Visuals")]
    [Tooltip("Shown while food is still uncollected (e.g., a locked indicator).")]
    public GameObject lockedVisual;
    [Tooltip("Shown once all food is collected (e.g., a glowing warm bed).")]
    public GameObject unlockedVisual;

    [Header("Audio")]
    public AudioClip unlockSound;
    public AudioClip winSound;

    private bool isUnlocked;
    private bool hasTriggered;

    private void Start()
    {
        SetLocked(true);

        if (GameManager.Instance != null)
            GameManager.Instance.OnAllFoodCollected += Unlock;
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnAllFoodCollected -= Unlock;
    }

    private void Unlock()
    {
        isUnlocked = true;
        SetLocked(false);

        if (unlockSound)
            AudioSource.PlayClipAtPoint(unlockSound, transform.position);
    }

    private void SetLocked(bool locked)
    {
        if (lockedVisual)   lockedVisual.SetActive(locked);
        if (unlockedVisual) unlockedVisual.SetActive(!locked);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasTriggered || !isUnlocked || !other.CompareTag("Player")) return;
        hasTriggered = true;

        if (winSound)
            AudioSource.PlayClipAtPoint(winSound, transform.position);

        GameManager.Instance?.TriggerLevelComplete();
        FindObjectOfType<WinMenu>()?.ShowWin();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = isUnlocked
            ? new Color(0f, 1f, 0.5f, 0.3f)
            : new Color(1f, 0.3f, 0f, 0.3f);
        Gizmos.DrawCube(transform.position, transform.localScale);
    }
}
