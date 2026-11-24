using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SoloKnight.EditorTools
{
    /// <summary>Checks generated scenes and prefabs for missing serialized references.</summary>
    public static class SoloKnightValidator
    {
        // Fields that are allowed to be empty.
        static readonly HashSet<string> Optional = new HashSet<string>
        {
            "AudioManager.overrideClips",
            "EnemyAI.projectilePrefab", // melee enemies don't shoot
            "CameraFollow.target",      // auto-finds the player
        };

        [MenuItem("Solo Knight/Validate Generated Content", priority = 10)]
        public static void ValidateMenu()
        {
            int problems = Validate();
            EditorUtility.DisplayDialog("Solo Knight Validator",
                problems == 0 ? "All references are assigned. Ready to play!" : $"{problems} missing reference(s). See the Console.", "OK");
        }

        public static void ValidateBatch()
        {
            int problems = Validate();
            if (problems > 0) EditorApplication.Exit(1);
        }

        static int Validate()
        {
            int problems = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Prefabs" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true)) problems += Check(mb, path);
            }
            foreach (var scenePath in new[] { SoloKnightSceneBuilder.MenuScenePath, SoloKnightSceneBuilder.DungeonScenePath })
            {
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true)) problems += Check(mb, scenePath);
            }
            Debug.Log(problems == 0 ? "[SoloKnight] Validation passed: no missing references." : $"[SoloKnight] Validation found {problems} problem(s).");
            return problems;
        }

        static int Check(MonoBehaviour mb, string context)
        {
            if (mb == null) { Debug.LogError($"[SoloKnight] Missing script in {context}"); return 1; }
            if (mb.GetType().Namespace == null || !mb.GetType().Namespace.StartsWith("SoloKnight")) return 0;
            int problems = 0;
            var so = new SerializedObject(mb);
            var it = so.GetIterator();
            bool enter = true;
            while (it.NextVisible(enter))
            {
                enter = it.propertyType != SerializedPropertyType.String;
                if (it.propertyType != SerializedPropertyType.ObjectReference || it.name == "m_Script") continue;
                string field = it.propertyPath.Split('.')[0];
                if (Optional.Contains($"{mb.GetType().Name}.{field}")) continue;
                if (it.objectReferenceValue == null)
                {
                    Debug.LogError($"[SoloKnight] Missing reference: {mb.GetType().Name}.{it.propertyPath} on '{mb.name}' in {context}");
                    problems++;
                }
            }
            return problems;
        }
    }
}
