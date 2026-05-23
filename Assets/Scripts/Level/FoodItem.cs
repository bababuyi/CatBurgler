using UnityEngine;
using UnityEngine.InputSystem;

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
    private bool playerInRange;

    private void Start()
    {
        originPosition = transform.position;
    }

    private void Update()
    {
        if (collected) return;

        transform.position = originPosition + Vector3.up * (Mathf.Sin(Time.time * bobSpeed) * bobHeight);
        transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.World);
        if (playerInRange && Keyboard.current.eKey.wasPressedThisFrame)
            Collect();
        Debug.Log("PlayerInRange: " + playerInRange);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            playerInRange = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
            playerInRange = false;
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
