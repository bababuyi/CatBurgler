using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class BlindUnit : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("How long the close animation takes in seconds.")]
    public float animationDuration = 1f;
    [Tooltip("Name of the animation clip on each slate — must match the clip name in Unity.")]
    public string closeClipName = "Close";

    [Header("State")]
    public bool isClosed = false;

    [Header("Events")]
    public UnityEvent onWindowClosed;
    public UnityEvent onWindowOpened;

    public event System.Action OnClosed;
    public event System.Action OnOpened;

    private Animation[] slateAnimations;

    private void Awake()
    {
        slateAnimations = GetComponentsInChildren<Animation>();
        if (slateAnimations.Length == 0)
            Debug.LogWarning($"[BlindUnit] No Animation components found on children of {gameObject.name}. " +
                             "Make sure slates have Animation components attached.");
        else
            Debug.Log($"[BlindUnit] Found {slateAnimations.Length} slate animations on {gameObject.name}.");
    }

    public void Close()
    {
        if (isClosed) return;
        isClosed = true;
        Debug.Log($"[BlindUnit] Closing {gameObject.name}.");
        StartCoroutine(PlayAllAnimations(closeClipName));
    }

    public void Open()
    {
        if (!isClosed) return;
        isClosed = false;
        foreach (Animation anim in slateAnimations)
        {
            if (anim == null) continue;
            AnimationState state = anim[closeClipName];
            if (state == null) continue;
            state.speed = -1f;
            state.time = state.length;
            anim.Play(closeClipName);
        }
        onWindowOpened?.Invoke();
        OnOpened?.Invoke();
        Debug.Log($"[BlindUnit] Opening {gameObject.name}.");
    }

    public bool CanUse() => !isClosed;

    private IEnumerator PlayAllAnimations(string clipName)
    {
        int successCount = 0;
        foreach (Animation anim in slateAnimations)
        {
            if (anim == null) continue;
            AnimationState state = anim[clipName];
            if (state != null)
            {
                state.speed = 1f;
                state.time = 0f;
                anim.Play(clipName);
                successCount++;
            }
            else
            {
                foreach (AnimationState s in anim)
                {
                    s.speed = 1f;
                    s.time = 0f;
                    anim.Play(s.name);
                    successCount++;
                    break;
                }
            }
        }
        if (successCount == 0)
            Debug.LogWarning($"[BlindUnit] No clips played on {gameObject.name}. " +
                             $"Check that clip name '{clipName}' matches the imported clip name.");
        else
            Debug.Log($"[BlindUnit] Playing {successCount} slate animations simultaneously.");

        yield return new WaitForSeconds(animationDuration);

        onWindowClosed?.Invoke();
        OnClosed?.Invoke();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = isClosed ? Color.red : Color.green;
        Gizmos.DrawWireCube(transform.position, new Vector3(1.5f, 0.6f, 0.1f));
    }
}