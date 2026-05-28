using UnityEngine;
using UnityEngine.Events;

public class CurtainWindow : MonoBehaviour
{
    [Header("Curtain Panels")]
    public Animation leftCurtain;
    public Animation rightCurtain;

    [Tooltip("Name of the close animation clip — must match the imported clip name in Unity.")]
    public string closeClipName = "Close";

    [Header("State")]
    public bool isOpen = true;

    [Header("Events")]
    public UnityEvent onWindowClosed;
    public UnityEvent onWindowOpened;

    public event System.Action OnClosed;

    public bool CanUse() => isOpen;

    public void Close()
    {
        if (!isOpen) return;
        isOpen = false;
        StartCoroutine(CloseSequentially());
    }

    public void Open()
    {
        if (isOpen) return;
        isOpen = true;
        PlayReverse(leftCurtain);
        PlayReverse(rightCurtain);
        onWindowOpened?.Invoke();
    }

    private System.Collections.IEnumerator CloseSequentially()
    {
        // Left panel first
        if (leftCurtain != null)
        {
            PlayForward(leftCurtain);
            yield return new WaitForSeconds(GetClipLength(leftCurtain));
        }

        // Then right panel
        if (rightCurtain != null)
        {
            PlayForward(rightCurtain);
            yield return new WaitForSeconds(GetClipLength(rightCurtain));
        }

        onWindowClosed?.Invoke();
        OnClosed?.Invoke();
        Debug.Log($"[CurtainWindow] {gameObject.name} fully closed.");
    }

    private void PlayForward(Animation anim)
    {
        AnimationState state = anim[closeClipName];
        if (state == null) { anim.Play(); return; }
        state.speed = 1f;
        state.time = 0f;
        anim.Play(closeClipName);
    }

    private void PlayReverse(Animation anim)
    {
        if (anim == null) return;
        AnimationState state = anim[closeClipName];
        if (state == null) return;
        state.speed = -1f;
        state.time = state.length;
        anim.Play(closeClipName);
    }

    private float GetClipLength(Animation anim)
    {
        AnimationState state = anim[closeClipName];
        if (state != null) return state.length;
        foreach (AnimationState s in anim) return s.length;
        return 1f;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = isOpen ? Color.green : Color.red;
        Gizmos.DrawWireCube(transform.position, Vector3.one * 0.5f);
    }
}