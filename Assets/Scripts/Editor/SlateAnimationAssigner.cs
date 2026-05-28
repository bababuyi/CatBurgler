using UnityEngine;
using UnityEditor;

public class SlateAnimationAssigner : EditorWindow
{
    private GameObject headRail;
    private string clipSearchTerm = "Slate";

    [MenuItem("Tools/Assign Slate Animations")]
    public static void ShowWindow()
    {
        GetWindow<SlateAnimationAssigner>("Assign Slate Animations");
    }

    private void OnGUI()
    {
        GUILayout.Label("Slate Animation Auto-Assigner", EditorStyles.boldLabel);
        GUILayout.Space(10);

        headRail = (GameObject)EditorGUILayout.ObjectField(
            "Head Rail Object",
            headRail,
            typeof(GameObject),
            true
        );

        GUILayout.Space(5);
        clipSearchTerm = EditorGUILayout.TextField("Clip Search Term", clipSearchTerm);
        GUILayout.Space(5);

        EditorGUILayout.HelpBox(
            "This will find all Animation components on children of the selected headrail " +
            "and assign the first matching clip it finds in the project.",
            MessageType.Info
        );

        GUILayout.Space(10);

        if (GUILayout.Button("Assign Animations", GUILayout.Height(40)))
        {
            if (headRail == null)
            {
                EditorUtility.DisplayDialog("Error", "Please assign a Head Rail object first.", "OK");
                return;
            }
            AssignAnimations();
        }
    }

    private void AssignAnimations()
    {
        Animation[] animations = headRail.GetComponentsInChildren<Animation>();

        if (animations.Length == 0)
        {
            EditorUtility.DisplayDialog("Error",
                "No Animation components found on children.",
                "OK");
            return;
        }

        // Load ALL assets from the FBX directly
        string fbxPath = "Assets/Mesh/CatHouse.fbx";
        Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);

        // Filter for AnimationClips only
        System.Collections.Generic.List<AnimationClip> clips =
            new System.Collections.Generic.List<AnimationClip>();

        foreach (Object asset in allAssets)
        {
            if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                clips.Add(clip);
        }

        Debug.Log($"[SlateAnimationAssigner] Found {clips.Count} clips in FBX.");

        int assignedCount = 0;

        foreach (Animation anim in animations)
        {
            if (anim == null) continue;
            string objectName = anim.gameObject.name;

            foreach (AnimationClip clip in clips)
            {
                if (clip.name == objectName + "|SlateAction")
                {
                    anim.clip = clip;
                    anim.AddClip(clip, clip.name);
                    assignedCount++;
                    Debug.Log($"Assigned '{clip.name}' to {objectName}");
                    break;
                }
            }
        }

        EditorUtility.DisplayDialog("Done",
            $"Assigned {assignedCount} / {animations.Length} slates.", "OK");
    }
}