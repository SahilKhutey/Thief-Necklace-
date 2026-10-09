using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ThiefAndTheNecklace;
using ThiefAndTheNecklace.Content;
using UnityEditor;
using UnityEngine;

namespace ThiefAndTheNecklace.EditorTools
{
    public sealed class ScenarioBuilderWindow : EditorWindow
    {
        private ScenarioDataAsset _scenario;
        private int _playerCount = 4;
        private int _seed = 73918244;
        private ScenarioDifficulty _difficulty = ScenarioDifficulty.Normal;
        private RuntimeScenario _preview;
        private ValidationResult _validation;
        private Vector2 _scroll;

        [MenuItem("Thief & the Necklace/Scenario Builder")]
        private static void Open() => GetWindow<ScenarioBuilderWindow>("Scenario Builder");

        [MenuItem("Thief & the Necklace/Create Default Scenario Assets")]
        public static void CreateDefaultAssets()
        {
            const string root = "Assets/ThiefAndTheNecklace/Content/Scenarios";
            Directory.CreateDirectory(root);
            Directory.CreateDirectory("Assets/ThiefAndTheNecklace/Resources");
            AssetDatabase.Refresh();
            var templates = DefaultScenarioCatalog.CreateRegistry().All;
            var assets = new System.Collections.Generic.List<ScenarioDataAsset>();
            foreach (var template in templates)
            {
                var path = root + "/" + template.scenarioId + ".asset";
                var asset = AssetDatabase.LoadAssetAtPath<ScenarioDataAsset>(path);
                if (asset == null)
                {
                    asset = CreateInstance<ScenarioDataAsset>();
                    AssetDatabase.CreateAsset(asset, path);
                }
                asset.template = template;
                EditorUtility.SetDirty(asset);
                assets.Add(asset);
            }
            var registryPath = "Assets/ThiefAndTheNecklace/Resources/ScenarioRegistry.asset";
            var registry = AssetDatabase.LoadAssetAtPath<ScenarioRegistryAsset>(registryPath);
            if (registry == null)
            {
                registry = CreateInstance<ScenarioRegistryAsset>();
                AssetDatabase.CreateAsset(registry, registryPath);
            }
            registry.scenarios = assets.ToArray();
            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = registry;
            UnityEngine.Debug.Log("Created or updated six default scenario assets and the scenario registry.");
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Author and validate structured mysteries before a round is committed. Private secrets and full world truth stay in the authority runtime package.", MessageType.Info);
            _scenario = (ScenarioDataAsset)EditorGUILayout.ObjectField("Scenario", _scenario, typeof(ScenarioDataAsset), false);
            _playerCount = EditorGUILayout.IntPopup("Players", _playerCount, new[] { "4", "5", "6" }, new[] { 4, 5, 6 });
            _difficulty = (ScenarioDifficulty)EditorGUILayout.EnumPopup("Difficulty", _difficulty);
            _seed = EditorGUILayout.IntField("Replay Seed", _seed);
            using (new EditorGUI.DisabledScope(_scenario == null || _scenario.template == null))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Validate Template")) ValidateTemplate();
                if (GUILayout.Button("Generate Preview")) GeneratePreview();
                if (GUILayout.Button("Run 10,000 Seeds")) RunBatch();
                EditorGUILayout.EndHorizontal();
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            if (_validation != null)
            {
                EditorGUILayout.HelpBox(_validation.IsValid ? "VALIDATION PASSED" : "VALIDATION FAILED", _validation.IsValid ? MessageType.Info : MessageType.Error);
                foreach (var error in _validation.Errors) EditorGUILayout.LabelField("• " + error, EditorStyles.wordWrappedLabel);
            }
            if (_preview != null) DrawPreview(_preview);
            EditorGUILayout.EndScrollView();
        }

        private void ValidateTemplate()
        {
            _preview = null;
            _validation = ScenarioTemplateValidator.Validate(_scenario.template, _playerCount);
        }

        private void GeneratePreview()
        {
            try
            {
                var players = Enumerable.Range(1, _playerCount).Select(i => new PlayerData("P" + i, "Player " + i)).ToList();
                _preview = new ScenarioGenerator(new ScenarioValidator()).Generate(_scenario.template, players, _seed, _difficulty);
                _validation = new ScenarioValidator().Validate(_scenario.template, _preview,
                    _scenario.template.difficulties.FirstOrDefault(p => p.difficulty == _difficulty));
            }
            catch (Exception ex)
            {
                _preview = null;
                _validation = new ValidationResult();
                _validation.AddError(ex.Message);
            }
        }

        private void RunBatch()
        {
            var generator = new ScenarioGenerator(new ScenarioValidator());
            var watch = Stopwatch.StartNew();
            var passed = 0;
            var failures = 0;
            var firstFailure = string.Empty;
            for (var i = 0; i < 10000; i++)
            {
                if (i % 250 == 0 && EditorUtility.DisplayCancelableProgressBar("Validating scenario seeds", (i + 1) + " / 10000", i / 10000f)) break;
                try
                {
                    var players = Enumerable.Range(1, _playerCount).Select(n => new PlayerData("P" + n, "Player " + n)).ToList();
                    generator.Generate(_scenario.template, players, unchecked(_seed + i), _difficulty);
                    passed++;
                }
                catch (Exception ex) { failures++; if (firstFailure.Length == 0) firstFailure = ex.Message; }
            }
            watch.Stop();
            EditorUtility.ClearProgressBar();
            UnityEngine.Debug.Log("Scenario batch: passed=" + passed + ", failed=" + failures + ", elapsed=" + watch.ElapsedMilliseconds + "ms" + (firstFailure.Length == 0 ? string.Empty : ", first=" + firstFailure));
        }

        private static void DrawPreview(RuntimeScenario scenario)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField(scenario.title + "  ·  " + scenario.playerCount + " players  ·  " + scenario.difficulty, EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Scenario " + scenario.scenarioId + " v" + scenario.scenarioVersion + " | Seed " + scenario.seed + " | Hash " + scenario.scenarioHash);
            EditorGUILayout.LabelField("Target: " + scenario.targetObjectId + " | Authority-only Thief: " + scenario.thiefPlayerId);
            var quality = ScenarioQualityReport.From(scenario);
            EditorGUILayout.LabelField("Quality: " + quality.ClueCount + " clues | " + quality.DistinctEvidencePaths + " evidence paths | "
                + quality.FalseLeadCount + " false leads | " + quality.AmbiguousClueCount + " partial truths");
            EditorGUILayout.LabelField("Timeline", EditorStyles.boldLabel);
            foreach (var e in scenario.events) EditorGUILayout.LabelField("21:" + e.minute.ToString("00") + "  " + e.eventId + " — " + e.description, EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("Private clue distribution", EditorStyles.boldLabel);
            foreach (var secret in scenario.playerSecrets)
            {
                EditorGUILayout.LabelField(secret.playerId + " / " + secret.roleId + (secret.isThief ? " [THIEF — AUTHORITY ONLY]" : string.Empty));
                foreach (var clueId in secret.clueIds)
                {
                    var clue = scenario.clues.First(c => c.clueId == clueId);
                    EditorGUILayout.LabelField("    " + clue.clueId + ": " + clue.text, EditorStyles.wordWrappedLabel);
                }
            }
        }
    }
}
