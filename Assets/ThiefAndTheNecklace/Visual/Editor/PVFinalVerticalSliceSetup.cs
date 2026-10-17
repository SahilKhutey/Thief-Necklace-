#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ThiefAndTheNecklace;
using ThiefAndTheNecklace.Visual;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ThiefAndTheNecklace.Visual.Editor
{
    /// <summary>Creates a runnable production entry scene and places it first in Build Settings.</summary>
    public static class PVFinalVerticalSliceSetup
    {
        private const string SceneFolder = "Assets/ThiefAndTheNecklace/Scenes";
        private const string ScenePath = SceneFolder + "/ProductionVerticalSlice.unity";

        [MenuItem("Thief & the Necklace/Visual/Setup Production Vertical Slice")]
        public static void Setup()
        {
            EnsureFolder(SceneFolder);
            var scene = File.Exists(ScenePath)
                ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var app = Object.FindObjectOfType<OfflinePrototypeApp>();
            if (app == null)
            {
                var host = new GameObject("Thief & the Necklace — Production Entry");
                Undo.RegisterCreatedObjectUndo(host, "Create production entry point");
                app = host.AddComponent<OfflinePrototypeApp>();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                Debug.LogError("PV-FINAL: could not save the production vertical-slice scene at " + ScenePath);
                return;
            }

            var scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };
            scenes.AddRange(EditorBuildSettings.scenes.Where(item => item.path != ScenePath));
            EditorBuildSettings.scenes = scenes.ToArray();

            PV3VisualProductionSetup.BuildVisualDataAssets();
            PVFinalReleaseGate.CreateOrUpdateProfile();
            AssetDatabase.Refresh();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            Debug.Log("PV-FINAL vertical-slice entry scene is configured. Run Validate PV-FINAL Release Gate; visual art and human approval remain separate requirements.", app);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var leaf = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            if (!string.IsNullOrEmpty(parent) && AssetDatabase.IsValidFolder(parent)) AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
#endif
