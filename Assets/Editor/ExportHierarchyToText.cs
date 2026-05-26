using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using System.IO;
using System.Text;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;

public class ExportHierarchyToText : EditorWindow
{
    private bool filterByName = false;
    private string nameFilter = "";
    private bool filterByComponent = false;
    private string componentFilter = "";
    private bool onlyFlagged = false;
    private bool exportAllScenes = false;

    private bool showTransform = true;
    private bool showPosition = true;
    private bool showRotation = true;
    private bool showScale = true;
    private bool showComponents = true;
    private bool showBuiltinComponents = true;
    private bool showScripts = true;
    private bool showPublicFields = true;
    private bool showSerializedFields = true;

    private bool foldFilters = true;
    private bool foldColumns = true;
    private bool foldTransSub = true;

    private Vector2 scroll;

    private static int totalObjects;
    private static int totalComponents;
    private static List<string> auditIssues;

    private static readonly string[] PresetNames = {
        "Full",
        "Names Only",
        "Names + Transform",
        "Names + Components",
        "Names + Scripts",
        "Scripts + Fields",
    };

    [MenuItem("Window/Export Hierarchy to Text")]
    public static void ShowWindow()
    {
        var win = GetWindow<ExportHierarchyToText>("Export Hierarchy");
        win.minSize = new Vector2(300, 420);
    }

    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        GUILayout.Label("Scene Scope", EditorStyles.boldLabel);
        exportAllScenes = EditorGUILayout.Toggle("All Open Scenes", exportAllScenes);

        EditorGUILayout.Space(6);

        GUILayout.Label("Quick Presets", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();

        for (int i = 0; i < 3; i++)
        {
            if (GUILayout.Button(PresetNames[i], GUILayout.Height(22)))
                ApplyPreset(i);
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();

        for (int i = 3; i < PresetNames.Length; i++)
        {
            if (GUILayout.Button(PresetNames[i], GUILayout.Height(22)))
                ApplyPreset(i);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6);

        foldFilters = EditorGUILayout.Foldout(foldFilters, "Object Filters", true, EditorStyles.foldoutHeader);
        if (foldFilters)
        {
            EditorGUI.indentLevel++;

            filterByName = EditorGUILayout.Toggle("Name Contains", filterByName);
            if (filterByName)
                nameFilter = EditorGUILayout.TextField(nameFilter);

            filterByComponent = EditorGUILayout.Toggle("Has Component", filterByComponent);
            if (filterByComponent)
                componentFilter = EditorGUILayout.TextField(componentFilter);

            onlyFlagged = EditorGUILayout.Toggle("Issues Only ⚠", onlyFlagged);

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(6);

        foldColumns = EditorGUILayout.Foldout(foldColumns, "Data Columns", true, EditorStyles.foldoutHeader);
        if (foldColumns)
        {
            EditorGUI.indentLevel++;

            foldTransSub = EditorGUILayout.Foldout(foldTransSub, "Transform", true);
            if (foldTransSub)
            {
                EditorGUI.indentLevel++;
                bool newShowTransform = EditorGUILayout.Toggle("Include Transform", showTransform);
                if (newShowTransform != showTransform)
                {
                    showTransform = newShowTransform;

                    if (!showTransform) { showPosition = showRotation = showScale = false; }
                }
                EditorGUI.BeginDisabledGroup(!showTransform);
                showPosition = EditorGUILayout.Toggle("  Position", showPosition);
                showRotation = EditorGUILayout.Toggle("  Rotation", showRotation);
                showScale = EditorGUILayout.Toggle("  Scale", showScale);
                EditorGUI.EndDisabledGroup();
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(2);

            bool newShowComponents = EditorGUILayout.Toggle("Components", showComponents);
            if (newShowComponents != showComponents)
            {
                showComponents = newShowComponents;
                if (!showComponents)
                    showBuiltinComponents = showScripts = showPublicFields = showSerializedFields = false;
            }
            EditorGUI.BeginDisabledGroup(!showComponents);
            EditorGUI.indentLevel++;
            showBuiltinComponents = EditorGUILayout.Toggle("Built-in Components", showBuiltinComponents);

            bool newShowScripts = EditorGUILayout.Toggle("Scripts (MonoBehaviour)", showScripts);
            if (newShowScripts != showScripts)
            {
                showScripts = newShowScripts;
                if (!showScripts) { showPublicFields = showSerializedFields = false; }
            }
            EditorGUI.BeginDisabledGroup(!showScripts);
            EditorGUI.indentLevel++;
            showPublicFields = EditorGUILayout.Toggle("Public Fields [pub]", showPublicFields);
            showSerializedFields = EditorGUILayout.Toggle("[SerializeField] [ser]", showSerializedFields);
            EditorGUI.indentLevel--;
            EditorGUI.EndDisabledGroup();

            EditorGUI.indentLevel--;
            EditorGUI.EndDisabledGroup();

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(12);

        GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
        if (GUILayout.Button("Export Scene Hierarchy", GUILayout.Height(34)))
            ExportHierarchy();
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndScrollView();
    }

    void ApplyPreset(int index)
    {
        showTransform = showPosition = showRotation = showScale = true;
        showComponents = showBuiltinComponents = showScripts = true;
        showPublicFields = showSerializedFields = true;

        switch (index)
        {
            case 0:
                break;

            case 1:
                showTransform = showPosition = showRotation = showScale = false;
                showComponents = showBuiltinComponents = showScripts = false;
                showPublicFields = showSerializedFields = false;
                break;

            case 2:
                showComponents = showBuiltinComponents = showScripts = false;
                showPublicFields = showSerializedFields = false;
                break;

            case 3:
                showPublicFields = showSerializedFields = false;
                break;

            case 4:
                showTransform = showPosition = showRotation = showScale = false;
                showBuiltinComponents = false;
                showPublicFields = showSerializedFields = false;
                break;

            case 5:
                showTransform = showPosition = showRotation = showScale = false;
                showBuiltinComponents = false;
                break;
        }

        Repaint();
    }

    void ExportHierarchy()
    {
        totalObjects = 0;
        totalComponents = 0;
        auditIssues = new List<string>();

        ExportConfig cfg = new ExportConfig
        {
            transform = showTransform,
            position = showPosition,
            rotation = showRotation,
            scale = showScale,
            components = showComponents,
            builtins = showBuiltinComponents,
            scripts = showScripts,
            publicFields = showPublicFields,
            serializedFields = showSerializedFields,
        };

        List<Scene> scenes = new List<Scene>();
        if (exportAllScenes)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (s.isLoaded) scenes.Add(s);
            }
        }
        else
        {
            scenes.Add(SceneManager.GetActiveScene());
        }

        StringBuilder body = new StringBuilder();
        foreach (Scene scene in scenes)
        {
            body.AppendLine("╔══════════════════════════════════════════╗");
            body.AppendLine($"  Scene: {scene.name}  ({scene.path})");
            body.AppendLine("╚══════════════════════════════════════════╝");
            body.AppendLine();
            foreach (GameObject go in scene.GetRootGameObjects())
                AppendObjectAndChildren(go.transform, body, 0, cfg);
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== Scene Hierarchy Export ===");
        sb.AppendLine($"Exported : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Columns  : {ActiveColumnSummary(cfg)}");
        sb.AppendLine();
        sb.AppendLine("── Audit Summary ──────────────────────────────");
        sb.AppendLine($"  GameObjects : {totalObjects}");
        sb.AppendLine($"  Components  : {totalComponents}");
        sb.AppendLine($"  Issues      : {auditIssues.Count}");
        if (auditIssues.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("  ⚠ Issues:");
            foreach (string issue in auditIssues)
                sb.AppendLine($"    • {issue}");
        }
        sb.AppendLine("───────────────────────────────────────────────");
        sb.AppendLine();
        sb.Append(body);

        string defaultName = exportAllScenes
            ? "AllScenes_Hierarchy.txt"
            : $"{SceneManager.GetActiveScene().name}_Hierarchy.txt";

        string path = EditorUtility.SaveFilePanel("Save Hierarchy Text", "", defaultName, "txt");
        if (!string.IsNullOrEmpty(path))
        {
            File.WriteAllText(path, sb.ToString());
            Debug.Log($"Exported: {path}  ({totalObjects} objects, {auditIssues.Count} issues)");
        }
    }

    void AppendObjectAndChildren(Transform t, StringBuilder sb, int level, ExportConfig cfg)
    {
        if (filterByName && !string.IsNullOrEmpty(nameFilter))
        {
            if (t.name.IndexOf(nameFilter, StringComparison.OrdinalIgnoreCase) < 0)
            {
                for (int i = 0; i < t.childCount; i++)
                    AppendObjectAndChildren(t.GetChild(i), sb, level + 1, cfg);
                return;
            }
        }

        if (filterByComponent && !string.IsNullOrEmpty(componentFilter))
        {
            bool has = t.GetComponents<Component>().Any(c =>
                c != null && c.GetType().Name.IndexOf(componentFilter, StringComparison.OrdinalIgnoreCase) >= 0);
            if (!has)
            {
                for (int i = 0; i < t.childCount; i++)
                    AppendObjectAndChildren(t.GetChild(i), sb, level + 1, cfg);
                return;
            }
        }

        Component[] components = t.GetComponents<Component>();
        bool objectHasIssue = components.Any(c => c == null);

        if (!objectHasIssue)
        {
            foreach (Component comp in components)
            {
                if (comp is MonoBehaviour mono)
                {
                    foreach (FieldInfo field in GetAuditableFields(mono, true, true))
                    {
                        object val = field.GetValue(mono);
                        if (val == null || (val is UnityEngine.Object uo && uo == null))
                        {
                            objectHasIssue = true;
                            break;
                        }
                    }
                    if (objectHasIssue) break;
                }
            }
        }

        if (onlyFlagged && !objectHasIssue)
        {
            for (int i = 0; i < t.childCount; i++)
                AppendObjectAndChildren(t.GetChild(i), sb, level + 1, cfg);
            return;
        }

        totalObjects++;
        string indent = new string(' ', level * 4);
        sb.AppendLine($"{indent}▸ {t.name}{(objectHasIssue ? "  ⚠" : "")}");

        if (cfg.transform)
        {
            bool anyTransSub = cfg.position || cfg.rotation || cfg.scale;
            if (anyTransSub)
            {
                sb.AppendLine($"{indent}  [Transform]");
                if (cfg.position) sb.AppendLine($"{indent}    Position : {t.localPosition}");
                if (cfg.rotation) sb.AppendLine($"{indent}    Rotation : {t.localEulerAngles}");
                if (cfg.scale) sb.AppendLine($"{indent}    Scale    : {t.localScale}");
            }
        }

        if (cfg.components)
        {
            bool headerWritten = false;

            foreach (Component comp in components)
            {
                totalComponents++;

                if (comp == null)
                {
                    EnsureComponentHeader(sb, indent, ref headerWritten);
                    sb.AppendLine($"{indent}    ⚠ Missing Script");
                    auditIssues.Add($"Missing script on: {GetGameObjectPath(t)}");
                    continue;
                }

                if (comp is MonoBehaviour mono)
                {
                    if (!cfg.scripts) continue;
                    EnsureComponentHeader(sb, indent, ref headerWritten);
                    sb.AppendLine($"{indent}    ◆ {mono.GetType().Name} (MonoBehaviour)");
                    AppendFields(mono, sb, indent + "        ", cfg, auditIssues);
                }
                else
                {
                    if (!cfg.builtins) continue;
                    EnsureComponentHeader(sb, indent, ref headerWritten);
                    sb.AppendLine($"{indent}    ◇ {comp.GetType().Name}");
                }
            }
        }

        sb.AppendLine();

        for (int i = 0; i < t.childCount; i++)
            AppendObjectAndChildren(t.GetChild(i), sb, level + 1, cfg);
    }

    static void EnsureComponentHeader(StringBuilder sb, string indent, ref bool written)
    {
        if (!written) { sb.AppendLine($"{indent}  [Components]"); written = true; }
    }

    static void AppendFields(MonoBehaviour mono, StringBuilder sb, string indent,
                             ExportConfig cfg, List<string> issues)
    {
        foreach (FieldInfo field in GetAuditableFields(mono, cfg.publicFields, cfg.serializedFields))
        {
            object val = field.GetValue(mono);
            string valueStr = FormatValue(val);
            string tag = field.IsPublic ? "pub" : "ser";
            bool isNull = val == null || (val is UnityEngine.Object uo && uo == null);

            sb.AppendLine($"{indent}[{tag}] {field.Name} = {valueStr}{(isNull ? "  ⚠ NULL" : "")}");

            if (isNull)
                issues.Add($"Null ref '{field.Name}' on {mono.GetType().Name} @ {GetGameObjectPath(mono.transform)}");
        }
    }

    static IEnumerable<FieldInfo> GetAuditableFields(MonoBehaviour mono,
                                                     bool includePublic,
                                                     bool includeSerialized)
    {
        Type type = mono.GetType();
        var result = Enumerable.Empty<FieldInfo>();

        if (includePublic)
            result = result.Concat(
                type.GetFields(BindingFlags.Instance | BindingFlags.Public)
                    .Where(f => !Attribute.IsDefined(f, typeof(HideInInspector))));

        if (includeSerialized)
            result = result.Concat(
                type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                    .Where(f => Attribute.IsDefined(f, typeof(SerializeField))
                              && !Attribute.IsDefined(f, typeof(HideInInspector))));

        return result;
    }

    static string FormatValue(object value)
    {
        if (value == null) return "null";
        Type type = value.GetType();

        if (type.IsPrimitive || value is string || type.IsEnum)
            return value.ToString();

        if (value is UnityEngine.Object uo)
            return uo != null ? $"{uo.name} ({type.Name})" : "null";

        if (value is IList list) return $"[{list.Count} elements]";
        if (value is IEnumerable ienu) { int n = 0; foreach (var _ in ienu) n++; return $"[{n} elements]"; }

        return $"({type.Name})";
    }

    static string GetGameObjectPath(Transform t)
    {
        var parts = new List<string>();
        while (t != null) { parts.Insert(0, t.name); t = t.parent; }
        return string.Join("/", parts);
    }

    static string ActiveColumnSummary(ExportConfig cfg)
    {
        var parts = new List<string>();
        if (cfg.transform)
        {
            var sub = new List<string>();
            if (cfg.position) sub.Add("pos");
            if (cfg.rotation) sub.Add("rot");
            if (cfg.scale) sub.Add("scale");
            parts.Add(sub.Count > 0 ? $"Transform({string.Join(",", sub)})" : "Transform");
        }
        if (cfg.builtins) parts.Add("BuiltinComponents");
        if (cfg.scripts)
        {
            var sub = new List<string>();
            if (cfg.publicFields) sub.Add("pub");
            if (cfg.serializedFields) sub.Add("ser");
            parts.Add(sub.Count > 0 ? $"Scripts({string.Join(",", sub)})" : "Scripts");
        }
        return parts.Count > 0 ? string.Join(" | ", parts) : "Names only";
    }

    private struct ExportConfig
    {
        public bool transform, position, rotation, scale;
        public bool components, builtins, scripts;
        public bool publicFields, serializedFields;
    }
}
