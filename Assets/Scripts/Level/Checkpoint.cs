using UnityEngine;

/// <summary>
/// Saves the player's respawn position when walked through.
/// Can only be activated once — place multiple in the level for progression.
///
/// SETUP: Add a Trigger Collider. Optionally assign activatedVisual (e.g., a glowing effect).
/// </summary>
public class Checkpoint : MonoBehaviour
{
    [Header("Visuals")]
    [Tooltip("Activated once the checkpoint is hit (e.g., a particle system or light change).")]
    public GameObject activatedVisual;
    public GameObject inactiveVisual;

    [Header("Audio")]
    public AudioClip activateSound;

    private bool activated;

    private void Start()
    {
        if (activatedVisual) activatedVisual.SetActive(false);
        if (inactiveVisual)  inactiveVisual.SetActive(true);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (activated || !other.CompareTag("Player")) return;
        activated = true;

        GameManager.Instance?.SetCheckpoint(transform.position, transform.rotation);

        if (activatedVisual) activatedVisual.SetActive(true);
        if (inactiveVisual)  inactiveVisual.SetActive(false);
        if (activateSound)   AudioSource.PlayClipAtPoint(activateSound, transform.position);
    }
}
