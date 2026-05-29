using UnityEngine;

public class WindowEscapeZone : MonoBehaviour
{
    [Header("Window Link")]
    [Tooltip("The Window or CurtainWindow this zone belongs to. " +
             "If the window is closed, escape is blocked.")]
    public MonoBehaviour linkedWindow;

    [Header("Escape Settings")]
    public float escapeDuration = 2f;

    [Header("Audio")]
    public AudioClip escapeSound;

    public float EscapeProgress => playerInZone ? escapeTimer / escapeDuration : 0f;
    public bool PlayerInZone => playerInZone;

    private bool playerInZone;
    private float escapeTimer;
    private bool hasEscaped;

    private HealthScript playerHealth;
    private DogAI dogAI;
    private GrandmaAI grandmaAI;

    private void Awake()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player) playerHealth = player.GetComponent<HealthScript>();

        dogAI = FindObjectOfType<DogAI>();
        grandmaAI = FindObjectOfType<GrandmaAI>();
    }

    private void Update()
    {
        if (hasEscaped || !playerInZone) { escapeTimer = 0f; return; }

        if (!CanEscapeNow()) { escapeTimer = 0f; return; }

        bool eHeld = UnityEngine.InputSystem.Keyboard.current.eKey.isPressed;
        if (!eHeld) { escapeTimer = 0f; return; }

        escapeTimer += Time.deltaTime;

        if (escapeTimer >= escapeDuration)
            TriggerEscape();
    }

    private bool CanEscapeNow()
    {
        if (playerHealth != null && playerHealth.IsBeingCarried) return false;

        if (dogAI != null &&
            (dogAI.CurrentState == DogAI.DogState.Pinning ||
             dogAI.CurrentState == DogAI.DogState.Carrying))
            return false;

        if (grandmaAI != null &&
            (grandmaAI.currentState == GrandmaAI.GrandmaState.Kicking ||
             grandmaAI.currentState == GrandmaAI.GrandmaState.Carrying))
            return false;

        if (linkedWindow is Window w && !w.isOpen) return false;
        if (linkedWindow is CurtainWindow cw && !cw.isOpen) return false;

        if (GameManager.Instance != null && !GameManager.Instance.HasCollectedAnyFood)
            return false;

        return true;
    }

    private void TriggerEscape()
    {
        hasEscaped = true;
        escapeTimer = escapeDuration;

        if (escapeSound)
            AudioSource.PlayClipAtPoint(escapeSound, transform.position);

        GameManager.Instance?.TriggerEscape();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) playerInZone = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInZone = false;
            escapeTimer = 0f;
        }
    }

    private void OnDrawGizmosSelected()
    {
        bool open = true;
        if (linkedWindow is Window lw) open = lw.isOpen;
        if (linkedWindow is CurtainWindow lc) open = lc.isOpen;

        Gizmos.color = open
            ? new Color(0.2f, 1f, 0.4f, 0.35f)
            : new Color(1f, 0.2f, 0.1f, 0.35f);
        Gizmos.DrawCube(transform.position, transform.localScale);
    }
}