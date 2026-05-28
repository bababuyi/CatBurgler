using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class BlindUnit : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("How long the open/close motion takes, in seconds.")]
    public float animationDuration = 1f;

    [Tooltip("Closed tilt for each slat, in degrees, about the slat's LOCAL X axis. " +
             "Flip the sign if the slats tilt the wrong way.")]
    public float closedAngle = 75f;

    [Tooltip("Only children whose name contains this are treated as slats.")]
    public string slateNameContains = "Slate";

    [Header("State")]
    public bool isClosed = false;

    [Header("Events")]
    public UnityEvent onWindowClosed;
    public UnityEvent onWindowOpened;

    public event System.Action OnClosed;
    public event System.Action OnOpened;

    private Transform[] _slats;
    private Quaternion[] _openRotations;
    private Coroutine _active;

    private void Awake()
    {
        var found = new List<Transform>();
        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            if (t == transform) continue;
            if (t.name.Contains(slateNameContains)) found.Add(t);
        }

        _slats = found.ToArray();
        _openRotations = new Quaternion[_slats.Length];
        for (int i = 0; i < _slats.Length; i++)
            _openRotations[i] = _slats[i].localRotation;

        if (_slats.Length == 0)
            Debug.LogWarning($"[BlindUnit] No slats found under {gameObject.name} " +
                             $"(looking for children whose name contains '{slateNameContains}').");
        else
            Debug.Log($"[BlindUnit] Found {_slats.Length} slats on {gameObject.name}.");
    }

    public void Close()
    {
        if (isClosed) return;
        isClosed = true;
        Debug.Log($"[BlindUnit] Closing {gameObject.name}.");
        StartMotion(true);
    }

    public void Open()
    {
        if (!isClosed) return;
        isClosed = false;
        Debug.Log($"[BlindUnit] Opening {gameObject.name}.");
        StartMotion(false);
    }

    public bool CanUse() => !isClosed;

    private void StartMotion(bool closing)
    {
        if (_active != null) StopCoroutine(_active);
        _active = StartCoroutine(RotateSlats(closing));
    }

    private IEnumerator RotateSlats(bool closing)
    {
        var startRots = new Quaternion[_slats.Length];
        var targetRots = new Quaternion[_slats.Length];
        for (int i = 0; i < _slats.Length; i++)
        {
            if (_slats[i] == null) continue;
            startRots[i] = _slats[i].localRotation;
            targetRots[i] = closing
                ? _openRotations[i] * Quaternion.AngleAxis(closedAngle, Vector3.right)
                : _openRotations[i];
        }

        float duration = Mathf.Max(0.01f, animationDuration);
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            float eased = Mathf.SmoothStep(0f, 1f, t);
            for (int i = 0; i < _slats.Length; i++)
            {
                if (_slats[i] == null) continue;
                _slats[i].localRotation = Quaternion.Slerp(startRots[i], targetRots[i], eased);
            }
            yield return null;
        }

        for (int i = 0; i < _slats.Length; i++)
            if (_slats[i] != null) _slats[i].localRotation = targetRots[i];

        if (closing) { onWindowClosed?.Invoke(); OnClosed?.Invoke(); }
        else { onWindowOpened?.Invoke(); OnOpened?.Invoke(); }

        _active = null;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = isClosed ? Color.red : Color.green;
        Gizmos.DrawWireCube(transform.position, new Vector3(1.5f, 0.6f, 0.1f));
    }
}