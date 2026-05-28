using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class CurtainWindow : MonoBehaviour
{
    public enum CloseMode { Slide, Scale }

    [Header("Mode")]
    [Tooltip("Slide = panels move together. Scale = panels grow to meet.")]
    public CloseMode closeMode = CloseMode.Slide;

    [Header("Panels (existing references reused — no need to reassign)")]
    public Animation leftCurtain;
    public Animation rightCurtain;

    [Header("Slide Settings")]
    [Tooltip("Local axis the panels slide along.")]
    public Vector3 slideAxis = Vector3.right;
    [Tooltip("How far each panel travels toward the centre when closing. " +
             "Flip the sign if they move apart instead of together.")]
    public float slideDistance = 0.5f;

    [Header("Scale Settings")]
    [Tooltip("Closed scale = open scale multiplied by this, per-axis. " +
             "e.g. (2,1,1) doubles width on X.")]
    public Vector3 closedScaleMultiplier = new Vector3(2f, 1f, 1f);

    [Header("Timing")]
    public float animationDuration = 1f;

    [Header("State")]
    public bool isOpen = true;

    [Header("Events")]
    public UnityEvent onWindowClosed;
    public UnityEvent onWindowOpened;

    public event System.Action OnClosed;

    private Transform _left, _right;
    private Vector3 _leftOpenPos, _rightOpenPos;
    private Vector3 _leftOpenScale, _rightOpenScale;
    private Coroutine _active;

    private void Awake()
    {
        _left = leftCurtain != null ? leftCurtain.transform : null;
        _right = rightCurtain != null ? rightCurtain.transform : null;

        if (_left == null || _right == null)
            Debug.LogWarning($"[CurtainWindow] {gameObject.name}: assign both Left Curtain and Right Curtain.");

        if (_left != null) { _leftOpenPos = _left.localPosition; _leftOpenScale = _left.localScale; }
        if (_right != null) { _rightOpenPos = _right.localPosition; _rightOpenScale = _right.localScale; }
    }

    public void Close()
    {
        if (!isOpen) return;
        isOpen = false;
        Debug.Log($"[CurtainWindow] Closing {gameObject.name} ({closeMode}).");
        StartMotion(true);
    }

    public void Open()
    {
        if (isOpen) return;
        isOpen = true;
        Debug.Log($"[CurtainWindow] Opening {gameObject.name}.");
        StartMotion(false);
    }

    public bool CanUse() => isOpen;

    private void StartMotion(bool closing)
    {
        if (_active != null) StopCoroutine(_active);
        _active = StartCoroutine(Animate(closing));
    }

    private IEnumerator Animate(bool closing)
    {
        Vector3 lStartP = _left ? _left.localPosition : Vector3.zero;
        Vector3 rStartP = _right ? _right.localPosition : Vector3.zero;
        Vector3 lStartS = _left ? _left.localScale : Vector3.one;
        Vector3 rStartS = _right ? _right.localScale : Vector3.one;

        Vector3 lTargetP = _leftOpenPos, rTargetP = _rightOpenPos;
        Vector3 lTargetS = _leftOpenScale, rTargetS = _rightOpenScale;

        if (closeMode == CloseMode.Slide)
        {
            Vector3 axis = slideAxis.sqrMagnitude > 0f ? slideAxis.normalized : Vector3.right;
            lTargetP = closing ? _leftOpenPos + axis * slideDistance : _leftOpenPos;
            rTargetP = closing ? _rightOpenPos - axis * slideDistance : _rightOpenPos;
        }
        else
        {
            lTargetS = closing ? Vector3.Scale(_leftOpenScale, closedScaleMultiplier) : _leftOpenScale;
            rTargetS = closing ? Vector3.Scale(_rightOpenScale, closedScaleMultiplier) : _rightOpenScale;
        }

        float duration = Mathf.Max(0.01f, animationDuration);
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            float e = Mathf.SmoothStep(0f, 1f, t);

            if (closeMode == CloseMode.Slide)
            {
                if (_left) _left.localPosition = Vector3.Lerp(lStartP, lTargetP, e);
                if (_right) _right.localPosition = Vector3.Lerp(rStartP, rTargetP, e);
            }
            else
            {
                if (_left) _left.localScale = Vector3.Lerp(lStartS, lTargetS, e);
                if (_right) _right.localScale = Vector3.Lerp(rStartS, rTargetS, e);
            }
            yield return null;
        }

        if (closeMode == CloseMode.Slide)
        {
            if (_left) _left.localPosition = lTargetP;
            if (_right) _right.localPosition = rTargetP;
        }
        else
        {
            if (_left) _left.localScale = lTargetS;
            if (_right) _right.localScale = rTargetS;
        }

        if (closing) { onWindowClosed?.Invoke(); OnClosed?.Invoke(); }
        else { onWindowOpened?.Invoke(); }

        _active = null;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = isOpen ? Color.green : Color.red;
        Gizmos.DrawWireCube(transform.position, new Vector3(0.8f, 1f, 0.1f));
    }
}