using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class WindowTester : MonoBehaviour
{
    [Header("Hotkeys")]
#if ENABLE_INPUT_SYSTEM
    public Key closeAllKey = Key.C;   // close every window at once
    public Key closeNextKey = Key.V;   // close them one at a time
    public Key openAllKey = Key.O;   // re-open / reset everything
#else
    public KeyCode closeAllKey  = KeyCode.C;   // close every window at once
    public KeyCode closeNextKey = KeyCode.V;   // close them one at a time
    public KeyCode openAllKey   = KeyCode.O;   // re-open / reset everything
#endif

    [Header("Sequence Options")]
    [Tooltip("Delay (seconds) between each window when using 'Close ALL in sequence'. 0 = simultaneous.")]
    public float sequenceDelay = 0f;

    [Header("On-Screen Overlay")]
    public bool showOverlay = true;

    private struct Closeable
    {
        public string name;
        public Action close;
        public Action open;
    }

    private readonly List<Closeable> _windows = new List<Closeable>();
    private int _nextIndex;

    private void Start() => Refresh();

    /// <summary>Re-scan the scene. Call again if windows are spawned at runtime.</summary>
    [ContextMenu("Refresh Window List")]
    public void Refresh()
    {
        _windows.Clear();

        foreach (var w in FindObjectsByType<Window>(FindObjectsSortMode.None))
            _windows.Add(new Closeable { name = w.name, close = w.Close, open = w.Open });

        foreach (var b in FindObjectsByType<BlindUnit>(FindObjectsSortMode.None))
            _windows.Add(new Closeable { name = b.name, close = b.Close, open = b.Open });

        foreach (var c in FindObjectsByType<CurtainWindow>(FindObjectsSortMode.None))
            _windows.Add(new Closeable { name = c.name, close = c.Close, open = c.Open });

        _nextIndex = 0;
        Debug.Log($"[WindowTester] Found {_windows.Count} window(s) in scene " +
                  $"(Window / BlindUnit / CurtainWindow combined).");
    }

    private void Update()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb[closeAllKey].wasPressedThisFrame) CloseAll();
        if (kb[closeNextKey].wasPressedThisFrame) CloseNext();
        if (kb[openAllKey].wasPressedThisFrame) OpenAll();
#else
        if (Input.GetKeyDown(closeAllKey))  CloseAll();
        if (Input.GetKeyDown(closeNextKey)) CloseNext();
        if (Input.GetKeyDown(openAllKey))   OpenAll();
#endif
    }

    [ContextMenu("Close ALL")]
    public void CloseAll()
    {
        if (sequenceDelay > 0f) { StartCoroutine(CloseAllSequenced()); return; }

        Debug.Log($"[WindowTester] Closing all {_windows.Count} windows simultaneously.");
        foreach (var w in _windows) w.close?.Invoke();
    }

    private IEnumerator CloseAllSequenced()
    {
        Debug.Log($"[WindowTester] Closing {_windows.Count} windows in sequence " +
                  $"({sequenceDelay}s apart).");
        foreach (var w in _windows)
        {
            Debug.Log($"[WindowTester] -> {w.name}");
            w.close?.Invoke();
            yield return new WaitForSeconds(sequenceDelay);
        }
    }

    [ContextMenu("Close NEXT")]
    public void CloseNext()
    {
        if (_windows.Count == 0) { Debug.LogWarning("[WindowTester] No windows found. Press Refresh."); return; }
        if (_nextIndex >= _windows.Count) _nextIndex = 0;

        var w = _windows[_nextIndex];
        Debug.Log($"[WindowTester] Closing window {_nextIndex}/{_windows.Count - 1}: {w.name}");
        w.close?.Invoke();
        _nextIndex++;
    }

    [ContextMenu("Re-open ALL")]
    public void OpenAll()
    {
        Debug.Log("[WindowTester] Re-opening / resetting all windows.");
        foreach (var w in _windows) w.open?.Invoke();
        _nextIndex = 0;
    }

    private void OnGUI()
    {
        if (!showOverlay) return;

        const int w = 280, h = 108;
        GUI.Box(new Rect(10, 10, w, h), "Window Tester");
        GUI.Label(new Rect(22, 34, w - 20, 20), $"[{closeAllKey}]  Close ALL  ({_windows.Count} found)");
        GUI.Label(new Rect(22, 54, w - 20, 20), $"[{closeNextKey}]  Close NEXT  (index {_nextIndex})");
        GUI.Label(new Rect(22, 74, w - 20, 20), $"[{openAllKey}]  Re-open / reset");
        GUI.Label(new Rect(22, 94, w - 20, 20), "Right-click component for the same actions.");
    }
}