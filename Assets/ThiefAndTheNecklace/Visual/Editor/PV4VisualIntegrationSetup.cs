#if UNITY_EDITOR
using ThiefAndTheNecklace.PB1.Unity;
using ThiefAndTheNecklace.Visual.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ThiefAndTheNecklace.Visual.Editor
{
    /// <summary>Wires PV4 presentation components onto the selected production UIDocument host.</summary>
    public static class PV4VisualIntegrationSetup
    {
        private const string ArtRoot = "Assets/ThiefAndTheNecklace/Visual/ArtData/";

        [MenuItem("Thief & the Necklace/Visual/Install PV5 Final Polish On Selected UIDocument")]
        private static void InstallPV5OnSelection() => InstallOnSelection();

        [MenuItem("Thief & the Necklace/Visual/Install PV4 Runtime Integration On Selected UIDocument")]
        public static void InstallOnSelection()
        {
            var host = Selection.activeGameObject;
            if (host == null || host.GetComponent<UIDocument>() == null)
            {
                Debug.LogError("Select the GameObject that owns the production UIDocument and ScreenManager.");
                return;
            }

            var manager = host.GetComponent<ScreenManager>();
            if (manager == null)
            {
                Debug.LogError("The selected UIDocument needs PV2 ScreenManager installed first.", host);
                return;
            }

            var adapter = host.GetComponent<VisualCorePresentationAdapter>() ?? Undo.AddComponent<VisualCorePresentationAdapter>(host);
            var environment = host.GetComponent<EnvironmentPresentationController>() ?? Undo.AddComponent<EnvironmentPresentationController>(host);
            var lighting = host.GetComponent<LightingController>() ?? Undo.AddComponent<LightingController>(host);
            var quality = host.GetComponent<VisualQualityController>() ?? Undo.AddComponent<VisualQualityController>(host);
            var vfxPool = host.GetComponent<VFXPool>() ?? Undo.AddComponent<VFXPool>(host);
            var audioRouter = host.GetComponent<VisualAudioCueRouter>() ?? Undo.AddComponent<VisualAudioCueRouter>(host);
            var transitionProfile = AssetDatabase.LoadAssetAtPath<TransitionProfile>(ArtRoot + "TAN_TransitionProfile.asset");
            if (transitionProfile == null)
            {
                transitionProfile = ScriptableObject.CreateInstance<TransitionProfile>();
                AssetDatabase.CreateAsset(transitionProfile, ArtRoot + "TAN_TransitionProfile.asset");
            }
            var releaseProfile = PVFinalReleaseGate.EnsureProfileAsset();
            if (releaseProfile.transitions == null)
            {
                releaseProfile.transitions = transitionProfile;
                EditorUtility.SetDirty(releaseProfile);
            }
            var profileApplier = host.GetComponent<ReleasePresentationProfileApplier>() ?? Undo.AddComponent<ReleasePresentationProfileApplier>(host);

            var roleDatabase = AssetDatabase.LoadAssetAtPath<RoleVisualDatabase>(ArtRoot + "TAN_RoleVisualDatabase.asset");
            if (roleDatabase == null)
                Debug.LogWarning("PV3 role data asset not found. Runtime will safely show missing-art markers until PV3 visual data is built.", host);

            var adapterSerialized = new SerializedObject(adapter);
            adapterSerialized.FindProperty("bootstrap").objectReferenceValue = Object.FindObjectOfType<LocalGameBootstrap>();
            adapterSerialized.FindProperty("screens").objectReferenceValue = manager;
            adapterSerialized.FindProperty("roleVisuals").objectReferenceValue = roleDatabase;
            adapterSerialized.FindProperty("environment").objectReferenceValue = environment;
            adapterSerialized.FindProperty("vfxPool").objectReferenceValue = vfxPool;
            adapterSerialized.ApplyModifiedProperties();

            var audioSerialized = new SerializedObject(audioRouter);
            audioSerialized.FindProperty("audioService").objectReferenceValue = Object.FindObjectOfType<ThiefAndTheNecklace.UI.AudioService>();
            audioSerialized.ApplyModifiedProperties();

            var managerSerialized = new SerializedObject(manager);
            managerSerialized.FindProperty("transitionProfile").objectReferenceValue = transitionProfile;
            managerSerialized.ApplyModifiedProperties();

            var environmentSerialized = new SerializedObject(environment);
            environmentSerialized.FindProperty("document").objectReferenceValue = host.GetComponent<UIDocument>();
            environmentSerialized.ApplyModifiedProperties();

            var lightingSerialized = new SerializedObject(lighting);
            lightingSerialized.FindProperty("document").objectReferenceValue = host.GetComponent<UIDocument>();
            lightingSerialized.ApplyModifiedProperties();
            var qualitySerialized = new SerializedObject(quality);
            qualitySerialized.FindProperty("document").objectReferenceValue = host.GetComponent<UIDocument>();
            qualitySerialized.ApplyModifiedProperties();
            var vfxSerialized = new SerializedObject(vfxPool);
            vfxSerialized.FindProperty("releaseProfile").objectReferenceValue = releaseProfile;
            vfxSerialized.ApplyModifiedProperties();
            var profileSerialized = new SerializedObject(profileApplier);
            profileSerialized.FindProperty("profile").objectReferenceValue = releaseProfile;
            profileSerialized.ApplyModifiedProperties();

            EditorUtility.SetDirty(adapter);
            EditorUtility.SetDirty(environment);
            EditorUtility.SetDirty(lighting);
            EditorUtility.SetDirty(quality);
            EditorUtility.SetDirty(vfxPool);
            EditorUtility.SetDirty(profileApplier);
            EditorUtility.SetDirty(audioRouter);
            EditorUtility.SetDirty(manager);
            AssetDatabase.SaveAssets();
            EditorSceneManagerMarkDirty();
            Debug.Log("PV5 visual polish host installed: release profile, core adapter, responsive/accessibility classes, phase lighting, environment presentation, allowlisted pooled VFX and audio routing.", host);
        }

        private static void EditorSceneManagerMarkDirty()
        {
            var scene = Selection.activeGameObject.scene;
            if (scene.IsValid()) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
#endif
