using UnityEngine;

/// <summary>
/// A collectible food item inside the fridge (fish, chicken, milk carton, etc.).
/// Bobs and rotates to draw the player's eye.
/// Collected on trigger contact with the player.
///
/// SETUP: Add a Trigger Collider. Tag player as "Player".
/// </summary>
public class FoodItem : MonoBehaviour
{
    [Header("Idle Animation")]
    public float bobHeight = 0.15f;
    public float bobSpeed = 2f;
    public float rotateSpeed = 80f;

    [Header("Audio")]
    public AudioClip collectSound;

    private Vector3 originPosition;
    private bool collected;

    private void Start()
    {
        originPosition = transform.position;
    }

    private void Update()
    {
        if (collected) return;
        // Gentle hover
        transform.position = originPosition + Vector3.up * (Mathf.Sin(Time.time * bobSpeed) * bobHeight);
        transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.World);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (collected || !other.CompareTag("Player")) return;
        Collect();
    }

    private void Collect()
    {
        collected = true;

        if (collectSound)
            AudioSource.PlayClipAtPoint(collectSound, transform.position);

        GameManager.Instance?.CollectFood();
        Destroy(gameObject);
    }
}
