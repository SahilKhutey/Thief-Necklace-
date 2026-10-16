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
        private const string Pb1ScenarioPath = "Assets/ThiefAndTheNecklace/Resources/PB1/RoyalNecklace.asset";
        private const string Pb1RoleDirectory = "Assets/ThiefAndTheNecklace/Data/Roles/PB1";

        static PB1ProjectSetup()
        {
            EditorApplication.delayCall += EnsureBootstrapScene;
        }

        private static void EnsureBootstrapScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!File.Exists(ScenarioRegistryPath)) ScenarioBuilderWindow.CreateDefaultAssets();
            EnsurePb1Assets();
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

        private static void EnsurePb1Assets()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Pb1ScenarioPath));
            Directory.CreateDirectory(Pb1RoleDirectory);
            if (!File.Exists(Pb1ScenarioPath))
                AssetDatabase.CreateAsset(ThiefAndTheNecklace.PB1.ScenarioDefinition.CreateDefault(), Pb1ScenarioPath);
            foreach (ThiefAndTheNecklace.PB1.RoleType role in System.Enum.GetValues(typeof(ThiefAndTheNecklace.PB1.RoleType)))
            {
                if (role == ThiefAndTheNecklace.PB1.RoleType.Thief) continue;
                var rolePath = Path.Combine(Pb1RoleDirectory, role + ".asset").Replace('\\', '/');
                if (!File.Exists(rolePath)) AssetDatabase.CreateAsset(ThiefAndTheNecklace.PB1.RoleDefinition.CreateDefault(role), rolePath);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
#endif
