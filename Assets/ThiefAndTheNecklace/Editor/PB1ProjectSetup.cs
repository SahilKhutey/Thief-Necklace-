#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ThiefAndTheNecklace.EditorTools;

namespace ThiefAndTheNecklace.Editor
{
    [InitializeOnLoad]
    internal static class PB1ProjectSetup
    {
        private const string ScenePath = "Assets/ThiefAndTheNecklace/Scenes/Bootstrap.unity";
        private const string ScenarioRegistryPath = "Assets/ThiefAndTheNecklace/Resources/ScenarioRegistry.asset";

        static PB1ProjectSetup()
        {
            EditorApplication.delayCall += EnsureBootstrapScene;
        }

        private static void EnsureBootstrapScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!File.Exists(ScenarioRegistryPath)) ScenarioBuilderWindow.CreateDefaultAssets();
            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var root = new GameObject("PB1 Offline Prototype");
                root.AddComponent<OfflinePrototypeApp>();
                EditorSceneManager.SaveScene(scene, ScenePath);
            }

            var scenes = EditorBuildSettings.scenes;
            var included = false;
            foreach (var scene in scenes)
                if (scene.path == ScenePath && scene.enabled) included = true;
            if (!included)
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }
    }
}
#endif
