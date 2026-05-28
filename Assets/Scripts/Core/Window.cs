using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class Window : MonoBehaviour
{
    [Header("Target")]
    public Transform scaleTarget;

    [Header("Scale Settings")]
    public Vector3 closedScale = Vector3.one;

    [Header("Timing")]
    public float animationDuration = 1f;

    [Header("State")]
    public bool isOpen = true;

    [Header("Events")]
    public UnityEvent onWindowClosed;
    public UnityEvent onWindowOpened;

    public event System.Action OnClosed;

    private Vector3 _openScale;
    private Coroutine _active;

    private void Awake()
    {
        if (scaleTarget == null)
        {
            var anim = GetComponentInChildren<Animation>();
            scaleTarget = anim != null ? anim.transform : transform;
        }

        _openScale = scaleTarget.localScale;
        Debug.Log($"[Window] {gameObject.name}: scaling '{scaleTarget.name}', " +
                  $"open={_openScale}, closed={closedScale}.");
    }

    public void Close()
    {
        if (!isOpen) return;
        isOpen = false;
        Debug.Log($"[Window] Closing {gameObject.name}.");
        StartMotion(closedScale, true);
    }

    public void Open()
    {
        if (isOpen) return;
        isOpen = true;
        Debug.Log($"[Window] Opening {gameObject.name}.");
        StartMotion(_openScale, false);
    }

    public bool CanUse() => isOpen;

    private void StartMotion(Vector3 target, bool closing)
    {
        if (_active != null) StopCoroutine(_active);
        _active = StartCoroutine(ScaleTo(target, closing));
    }

    private IEnumerator ScaleTo(Vector3 target, bool closing)
    {
        Vector3 start = scaleTarget.localScale;
        float duration = Mathf.Max(0.01f, animationDuration);
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            float e = Mathf.SmoothStep(0f, 1f, t);
            scaleTarget.localScale = Vector3.Lerp(start, target, e);
            yield return null;
        }

        scaleTarget.localScale = target;

        if (closing) { onWindowClosed?.Invoke(); OnClosed?.Invoke(); }
        else { onWindowOpened?.Invoke(); }

        _active = null;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = isOpen ? Color.green : Color.red;
        Gizmos.DrawWireCube(transform.position, new Vector3(0.5f, 1f, 0.1f));
    }
}