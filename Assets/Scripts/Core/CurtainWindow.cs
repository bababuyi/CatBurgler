using UnityEngine;
using UnityEngine.Events;

public class CurtainWindow : MonoBehaviour
{
    [Header("State")]
    public bool isOpen = true;

    [Header("Left Curtain")]
    public Transform leftCurtain;
    public Transform leftOpenPosition;
    public Transform leftClosedPosition;

    [Header("Right Curtain")]
    public Transform rightCurtain;
    public Transform rightOpenPosition;
    public Transform rightClosedPosition;

    [Tooltip("How fast the curtains slide closed (units per second).")]
    public float closeSpeed = 1.5f;

    [Header("Events")]
    public UnityEvent onWindowClosed;
    public UnityEvent onWindowOpened;

    private bool isAnimating;
    private bool closing;

    private void Start()
    {
        if (leftCurtain && leftOpenPosition) leftCurtain.position = leftOpenPosition.position;
        if (rightCurtain && rightOpenPosition) rightCurtain.position = rightOpenPosition.position;
    }

    private void Update()
    {
        if (!isAnimating) return;

        Vector3 leftTarget = closing ? leftClosedPosition.position : leftOpenPosition.position;
        Vector3 rightTarget = closing ? rightClosedPosition.position : rightOpenPosition.position;

        if (leftCurtain)
            leftCurtain.position = Vector3.MoveTowards(
                leftCurtain.position, leftTarget, closeSpeed * Time.deltaTime);

        if (rightCurtain)
            rightCurtain.position = Vector3.MoveTowards(
                rightCurtain.position, rightTarget, closeSpeed * Time.deltaTime);

        bool leftDone = !leftCurtain || Vector3.Distance(leftCurtain.position, leftTarget) < 0.01f;
        bool rightDone = !rightCurtain || Vector3.Distance(rightCurtain.position, rightTarget) < 0.01f;

        if (leftDone && rightDone)
        {
            isAnimating = false;
            if (closing) onWindowClosed?.Invoke();
            else onWindowOpened?.Invoke();
        }
    }

    public void Close()
    {
        if (!isOpen) return;
        isOpen = false;
        closing = true;
        isAnimating = true;
        Debug.Log($"CurtainWindow {gameObject.name}: Closing.");
    }

    public void Open()
    {
        if (isOpen) return;
        isOpen = true;
        closing = false;
        isAnimating = true;
    }

    public bool CanUse() => isOpen;

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = isOpen ? Color.green : Color.red;
        Gizmos.DrawWireCube(transform.position, Vector3.one * 0.5f);

        if (leftClosedPosition && rightClosedPosition)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(leftClosedPosition.position, rightClosedPosition.position);
        }
    }
}