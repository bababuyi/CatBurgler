using UnityEngine;
using UnityEditor;

/// <summary>
/// Diagnostic tool. Select the head-rail (or any parent of the slates) and run
/// Tools -> Inspect Slate Clips. For each Animation component found in children
/// it logs the assigned clip, whether it's legacy, and — crucially — the PATH
/// each curve is bound to.
///
/// If the logged path is empty ("&lt;root&gt;"), the clip animates the object the
/// Animation component sits on (correct). If the path points to a nested
/// transform, the binding won't resolve and the slate will never move even
/// though Play() succeeds.
///
/// Lives in an Editor folder on purpose — it is an editor-only tool.
/// </summary>
public class SlateClipInspector : EditorWindow
{
    private GameObject root;

    [MenuItem("Tools/Inspect Slate Clips")]
    public static void ShowWindow() => GetWindow<SlateClipInspector>("Inspect Slate Clips");

    private void OnGUI()
    {
        GUILayout.Label("Slate Clip Binding Inspector", EditorStyles.boldLabel);
        GUILayout.Space(8);
        root = (GameObject)EditorGUILayout.ObjectField("Root (head rail)", root, typeof(GameObject), true);
        GUILayout.Space(8);

        EditorGUILayout.HelpBox(
            "Logs each slate's clip name, legacy flag, and the transform path each " +
            "curve targets. Empty path = animates the object the component is on (good). " +
            "Non-empty path = binding will not resolve = no movement.",
            MessageType.Info);

        GUILayout.Space(8);
        if (GUILayout.Button("Inspect", GUILayout.Height(36)))
        {
            if (root == null) { Debug.LogWarning("[SlateClipInspector] Assign a root first."); return; }
            Inspect();
        }
    }

    private void Inspect()
    {
        Animation[] anims = root.GetComponentsInChildren<Animation>();
        Debug.Log($"[SlateClipInspector] Found {anims.Length} Animation component(s) under {root.name}.");

        foreach (Animation anim in anims)
        {
            if (anim == null) continue;
            AnimationClip clip = anim.clip;

            if (clip == null)
            {
                Debug.LogWarning($"[SlateClipInspector] {anim.gameObject.name}: NO clip assigned.");
                continue;
            }

            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
            Debug.Log($"[SlateClipInspector] {anim.gameObject.name}: clip='{clip.name}', " +
                      $"legacy={clip.legacy}, length={clip.length:0.00}s, curves={bindings.Length}");

            // Show up to 4 bindings so the path/property pattern is obvious.
            int shown = 0;
            foreach (var b in bindings)
            {
                string path = string.IsNullOrEmpty(b.path) ? "<root>" : b.path;
                Debug.Log($"    -> path='{path}'  type={b.type.Name}  prop='{b.propertyName}'");
                if (++shown >= 4) break;
            }
        }
    }
}