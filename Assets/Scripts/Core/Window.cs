using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class Window : MonoBehaviour
{
    [Header("Settings")]
    public string closeClipName = "Close";

    [Header("State")]
    public bool isOpen = true;

    [Header("Events")]
    public UnityEvent onWindowClosed;
    public UnityEvent onWindowOpened;

    public event System.Action OnClosed;

    private Animation blindAnimation;

    private void Awake()
    {
        blindAnimation = GetComponentInChildren<Animation>();

        if (blindAnimation == null)
            Debug.LogWarning($"[Window] No Animation component found in children of {gameObject.name}.");
    }

    public void Close()
    {
        if (!isOpen) return;
        isOpen = false;
        StartCoroutine(PlayAndNotify());
    }

    public void Open()
    {
        if (isOpen || blindAnimation == null) return;
        isOpen = true;
        AnimationState state = blindAnimation[closeClipName];
        if (state == null) return;
        state.speed = -1f;
        state.time = state.length;
        blindAnimation.Play(closeClipName);
        onWindowOpened?.Invoke();
    }

    public bool CanUse() => isOpen;

    private IEnumerator PlayAndNotify()
    {
        if (blindAnimation != null)
        {
            AnimationState state = blindAnimation[closeClipName];
            if (state != null)
            {
                state.speed = 1f;
                state.time = 0f;
                blindAnimation.Play(closeClipName);
                yield return new WaitForSeconds(state.length);
            }
            else
            {
                foreach (AnimationState s in blindAnimation)
                {
                    s.speed = 1f;
                    s.time = 0f;
                    blindAnimation.Play(s.name);
                    yield return new WaitForSeconds(s.length);
                    break;
                }
            }
        }

        onWindowClosed?.Invoke();
        OnClosed?.Invoke();
        Debug.Log($"[Window] {gameObject.name} closed.");
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = isOpen ? Color.green : Color.red;
        Gizmos.DrawWireCube(transform.position, new Vector3(0.5f, 1f, 0.1f));
    }
}