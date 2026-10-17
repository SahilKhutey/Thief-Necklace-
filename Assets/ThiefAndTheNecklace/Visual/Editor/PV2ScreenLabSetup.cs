#if UNITY_EDITOR
using System.IO;
using ThiefAndTheNecklace.Visual.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace ThiefAndTheNecklace.Visual.Editor
{
    public static class PV2ScreenLabSetup
    {
        private const string UiRoot = "Assets/ThiefAndTheNecklace/Visual/UI/";
        private const string LabScene = UiRoot + "VisualScreenLab.unity";

        [MenuItem("Thief & the Necklace/Visual/Install PV2 Screens On Selected UIDocument")]
        public static void InstallOnSelection()
        {
            var target = Selection.activeGameObject;
            if (target == null) { Debug.LogError("Select a GameObject with a UIDocument first."); return; }
            var document = target.GetComponent<UIDocument>();
            if (document == null) { Debug.LogError("The selected GameObject needs a UIDocument.", target); return; }
            var manager = target.GetComponent<ScreenManager>() ?? Undo.AddComponent<ScreenManager>(target);
            Configure(manager, document);
            Debug.Log("PV2 screens and overlays assigned to the selected UIDocument.", target);
        }

        [MenuItem("Thief & the Necklace/Visual/Create PV2 Visual Screen Lab Scene")]
        public static void CreateLabScene()
        {
            if (File.Exists(LabScene)) { Debug.LogError("VisualScreenLab.unity already exists. Open it or remove it before recreating."); return; }
            var settingsPath = UiRoot + "PV2PanelSettings.asset";
            var settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(settingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(settings, settingsPath);
            }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            EditorSceneManager.SetActiveScene(scene);
            var host = new GameObject("PV2 Visual Screen Lab");
            var document = host.AddComponent<UIDocument>();
            document.panelSettings = settings;
            var manager = host.AddComponent<ScreenManager>();
            Configure(manager, document);
            EditorSceneManager.SaveScene(scene, LabScene);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = host;
            Debug.Log("Created the PV2 visual traversal scene at " + LabScene, host);
        }

        private static void Configure(ScreenManager manager, UIDocument document)
        {
            var serialized = new SerializedObject(manager);
            serialized.FindProperty("document").objectReferenceValue = document;
            var screenList = serialized.FindProperty("screens");
            var screenValues = (ScreenId[])System.Enum.GetValues(typeof(ScreenId));
            screenList.arraySize = screenValues.Length;
            for (var i = 0; i < screenValues.Length; i++)
            {
                var item = screenList.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("Id").enumValueIndex = (int)screenValues[i];
                item.FindPropertyRelative("View").objectReferenceValue = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UiRoot + "Screens/" + screenValues[i] + ".uxml");
            }
            var overlayValues = (VisualOverlayId[])System.Enum.GetValues(typeof(VisualOverlayId));
            var overlayList = serialized.FindProperty("overlays");
            overlayList.arraySize = overlayValues.Length;
            for (var i = 0; i < overlayValues.Length; i++)
            {
                var item = overlayList.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("Id").enumValueIndex = (int)overlayValues[i];
                item.FindPropertyRelative("View").objectReferenceValue = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UiRoot + "Overlays/" + overlayValues[i] + "Overlay.uxml");
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(manager);
        }
    }
}
#endif
