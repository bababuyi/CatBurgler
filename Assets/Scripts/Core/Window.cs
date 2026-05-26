using UnityEngine;
using UnityEngine.Events;

public class Window : MonoBehaviour
{
    [Header("State")]
    public bool isOpen = true;

    [Header("Animation Positions")]
    [Tooltip("Transform position when window is open.")]
    public Transform openPosition;
    [Tooltip("Transform position when window is closed.")]
    public Transform closedPosition;
    [Tooltip("How fast the window closes (units per second).")]
    public float closeSpeed = 2f;

    [Header("Events")]
    public UnityEvent onWindowClosed;
    public UnityEvent onWindowOpened;

    private bool isAnimating = false;
    private Vector3 targetPosition;

    private void Start()
    {
        if (openPosition != null && isOpen)
            transform.position = openPosition.position;
    }

    private void Update()
    {
        if (!isAnimating) return;

        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPosition,
            closeSpeed * Time.deltaTime
        );

        if (Vector3.Distance(transform.position, targetPosition) < 0.01f)
        {
            transform.position = targetPosition;
            isAnimating = false;
        }
    }

    public void Close()
    {
        if (!isOpen) return;

        isOpen = false;
        Debug.Log($"Window {gameObject.name}: Closed.");

        if (closedPosition != null)
        {
            targetPosition = closedPosition.position;
            isAnimating = true;
        }

        onWindowClosed?.Invoke();
    }

    public void Open()
    {
        if (isOpen) return;

        isOpen = true;

        if (openPosition != null)
        {
            targetPosition = openPosition.position;
            isAnimating = true;
        }

        onWindowOpened?.Invoke();
    }

    public bool CanUse() => isOpen;

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = isOpen ? Color.green : Color.red;
        Gizmos.DrawWireCube(transform.position, transform.localScale);
    }
}