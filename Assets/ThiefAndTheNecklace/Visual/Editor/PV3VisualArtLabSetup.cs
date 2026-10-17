#if UNITY_EDITOR
using System.IO;
using ThiefAndTheNecklace.Visual.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace ThiefAndTheNecklace.Visual.Editor
{
    public static class PV3VisualArtLabSetup
    {
        private const string Root = "Assets/ThiefAndTheNecklace/Visual/";
        private const string ScenePath = "Assets/ThiefAndTheNecklace/Scenes/VisualArtLab.unity";

        [MenuItem("Thief & the Necklace/Visual/Create PV3 Visual Art Lab Scene")]
        public static void CreateScene()
        {
            if (File.Exists(ScenePath))
            {
                EditorUtility.DisplayDialog("Visual Art Lab", "VisualArtLab.unity already exists. Open the existing scene to review it.", "OK");
                return;
            }

            EnsureFolder("Assets/ThiefAndTheNecklace/Scenes");
            PV3VisualProductionSetup.BuildVisualDataAssets();
            var settingsPath = Root + "UI/PV3ArtLabPanelSettings.asset";
            var settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(settingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(settings, settingsPath);
            }

            var roleDb = AssetDatabase.LoadAssetAtPath<RoleVisualDatabase>(Root + "ArtData/TAN_RoleVisualDatabase.asset");
            var cardDb = AssetDatabase.LoadAssetAtPath<CardVisualDatabase>(Root + "ArtData/TAN_CardVisualDatabase.asset");
            var spaceDb = AssetDatabase.LoadAssetAtPath<SpaceVisualDatabase>(Root + "ArtData/TAN_SpaceVisualDatabase.asset");
            var propDb = AssetDatabase.LoadAssetAtPath<PropVisualDatabase>(Root + "ArtData/TAN_PropVisualDatabase.asset");

            var activeScene = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var host = new GameObject("PV3 Visual Art Lab");
            var document = host.AddComponent<UIDocument>();
            document.panelSettings = settings;
            var controller = host.AddComponent<VisualArtLabController>();
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("document").objectReferenceValue = document;
            serialized.FindProperty("roleDatabase").objectReferenceValue = roleDb;
            serialized.FindProperty("cardDatabase").objectReferenceValue = cardDb;
            serialized.FindProperty("spaceDatabase").objectReferenceValue = spaceDb;
            serialized.FindProperty("propDatabase").objectReferenceValue = propDb;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            EditorSceneManager.CloseScene(scene, true);
            if (activeScene.IsValid()) SceneManager.SetActiveScene(activeScene);
            AssetDatabase.Refresh();
            Debug.Log("Created PV3 visual review scene: " + ScenePath);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var leaf = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            if (!string.IsNullOrEmpty(parent) && AssetDatabase.IsValidFolder(parent)) AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
#endif
