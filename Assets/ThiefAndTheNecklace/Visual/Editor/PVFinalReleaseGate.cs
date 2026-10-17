#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ThiefAndTheNecklace.Visual.Editor
{
    /// <summary>Creates the release-presentation profile and produces a truthful, content-aware release gate report.</summary>
    public static class PVFinalReleaseGate
    {
        private const string VisualRoot = "Assets/ThiefAndTheNecklace/Visual/";
        private const string ProfilePath = VisualRoot + "Profiles/TAN_ReleasePresentationProfile.asset";
        private const string ReportPath = VisualRoot + "Validation/PVFinalReleaseGate.latest.md";
        private static readonly string[] CourtRoles = { "King", "Queen", "Minister", "Soldier", "Guard", "Servant" };
        private static readonly string[] Spaces = { "BanquetHall", "RoyalChamber", "Treasury", "Garden", "EastCorridor", "WestGate" };
        private static readonly string[] ScreenAssets =
        {
            "MainMenu", "CreateRoom", "JoinRoom", "Lobby", "ScenarioIntro", "PrivateRole", "Game", "Reveal",
            "RoundResults", "MatchResults", "Settings", "HowToPlay", "ExitConfirmation", "Error", "Reconnect", "Disconnected"
        };
        private static readonly string[] OverlayAssets =
        {
            "ChatOverlay", "VotingOverlay", "VoteLockedOverlay", "VoteResolutionOverlay", "SettingsOverlay", "ExitConfirmationOverlay", "ErrorOverlay", "ConnectionOverlay"
        };

        [MenuItem("Thief & the Necklace/Visual/Create or Update PV-FINAL Presentation Profile")]
        public static void CreateOrUpdateProfile() => EnsureProfileAsset();

        public static ReleasePresentationProfile EnsureProfileAsset()
        {
            EnsureFolder(VisualRoot + "Profiles");
            var profile = AssetDatabase.LoadAssetAtPath<ReleasePresentationProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<ReleasePresentationProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            AssignIfNull(profile, "transitions", AssetDatabase.LoadAssetAtPath<TransitionProfile>(VisualRoot + "ArtData/TAN_TransitionProfile.asset"));
            AssignIfNull(profile, "roles", AssetDatabase.LoadAssetAtPath<RoleVisualDatabase>(VisualRoot + "ArtData/TAN_RoleVisualDatabase.asset"));
            AssignIfNull(profile, "cards", AssetDatabase.LoadAssetAtPath<CardVisualDatabase>(VisualRoot + "ArtData/TAN_CardVisualDatabase.asset"));
            AssignIfNull(profile, "spaces", AssetDatabase.LoadAssetAtPath<SpaceVisualDatabase>(VisualRoot + "ArtData/TAN_SpaceVisualDatabase.asset"));
            AssignIfNull(profile, "props", AssetDatabase.LoadAssetAtPath<PropVisualDatabase>(VisualRoot + "ArtData/TAN_PropVisualDatabase.asset"));
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            Debug.Log("PV-FINAL release presentation profile is ready. Run Validate PV-FINAL Release Gate after importing approved art and configuring the production scene.", profile);
            return profile;
        }

        [MenuItem("Thief & the Necklace/Visual/Validate PV-FINAL Release Gate")]
        public static void ValidateAndWriteReport()
        {
            EnsureFolder(VisualRoot + "Validation");
            var passed = new List<string>();
            var blockers = new List<string>();
            var profile = AssetDatabase.LoadAssetAtPath<ReleasePresentationProfile>(ProfilePath);
            if (profile == null) blockers.Add("Release presentation profile is missing. Run Create or Update PV-FINAL Presentation Profile.");
            else
            {
                CheckReference(profile.transitions, "TransitionProfile", blockers);
                CheckReference(profile.roles, "RoleVisualDatabase", blockers);
                CheckReference(profile.cards, "CardVisualDatabase", blockers);
                CheckReference(profile.spaces, "SpaceVisualDatabase", blockers);
                CheckReference(profile.props, "PropVisualDatabase", blockers);
                if (profile.approvedEffects == null || profile.approvedEffects.Length == 0) blockers.Add("No approved VFX IDs are configured in the release profile.");
                else passed.Add("Release profile has an explicit approved VFX allowlist.");
                if (profile.approvedForRelease) passed.Add("Release approval flag is set.");
                else blockers.Add("Release approval is not recorded in the presentation profile.");
            }

            foreach (var name in ScreenAssets)
            {
                var path = VisualRoot + "UI/Screens/" + name + ".uxml";
                if (AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.VisualTreeAsset>(path) == null) blockers.Add("Missing required screen: " + path);
                else passed.Add("Screen present: " + name);
            }
            foreach (var name in OverlayAssets)
            {
                var path = VisualRoot + "UI/Overlays/" + name + ".uxml";
                if (AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.VisualTreeAsset>(path) == null) blockers.Add("Missing required overlay: " + path);
                else passed.Add("Overlay present: " + name);
            }

            ValidateRoleData(profile != null ? profile.roles : null, passed, blockers);
            ValidateCardData(profile != null ? profile.cards : null, passed, blockers);
            ValidateSpaceData(profile != null ? profile.spaces : null, passed, blockers);
            ValidatePropData(profile != null ? profile.props : null, blockers);

            var sceneAssets = AssetDatabase.FindAssets("t:SceneAsset", new[] { "Assets" });
            var enabledScenes = EditorBuildSettings.scenes.Where(scene => scene.enabled && !string.IsNullOrWhiteSpace(scene.path)).ToArray();
            if (sceneAssets.Length == 0) blockers.Add("No Unity scene assets exist under Assets; a production vertical-slice scene has not been created.");
            else passed.Add("Unity scenes found under Assets: " + sceneAssets.Length);
            if (enabledScenes.Length == 0) blockers.Add("No enabled scenes are configured in Build Settings.");
            else passed.Add("Enabled Build Settings scenes: " + string.Join(", ", enabledScenes.Select(scene => Path.GetFileNameWithoutExtension(scene.path))));

            var textures = AssetDatabase.FindAssets("t:Texture2D", new[] { VisualRoot });
            if (textures.Length == 0) blockers.Add("No imported production texture assets exist in the Visual tree; art placeholders would appear.");
            else passed.Add("Imported visual textures found: " + textures.Length);
            passed.Add("Development diagnostic HUD is guarded by Editor/Development build symbols.");
            passed.Add("Non-development player builds already run the PV3 required-art build validator.");

            var report = new StringBuilder();
            report.AppendLine("# PV-FINAL Release Gate Report");
            report.AppendLine();
            report.AppendLine("Generated: " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'"));
            report.AppendLine("Status: **" + (blockers.Count == 0 ? "READY FOR HUMAN RELEASE REVIEW" : "BLOCKED — NOT A RELEASE SIGN-OFF") + "**");
            report.AppendLine();
            report.AppendLine("This automated editor report checks asset references and project configuration. It does not replace Unity compilation, play-mode golden paths, responsive/accessibility inspection, performance profiling, or visual-director approval.");
            report.AppendLine();
            WriteSection(report, "Blocking findings", blockers);
            WriteSection(report, "Checks observed", passed);
            WriteSection(report, "Manual QA required", new[]
            {
                "Open the production scene and verify all 20 screens/states and overlays without manual scene switching.",
                "Complete caught, escaped-after-second-tie, invalid self-vote, rapid repeat-input, reconnect/recovery, and reduced-motion paths.",
                "Inspect desktop WebGL, tablet, and mobile-landscape layouts with Normal, Large Text, High Contrast, and Reduced Motion enabled.",
                "Profile cold launch through multiple rounds for memory, GC allocations, loading spikes, draw calls, texture memory, and VFX reuse.",
                "Review approved final art in context and sign the visual freeze; no visual freeze is approved by this automated report."
            });
            File.WriteAllText(ReportPath, report.ToString(), new UTF8Encoding(false));
            AssetDatabase.Refresh();
            if (blockers.Count == 0) Debug.Log("PV-FINAL checks have no automated blockers. Human play-mode and visual review are still required. Report: " + ReportPath);
            else Debug.LogError("PV-FINAL is blocked by " + blockers.Count + " finding(s). See " + ReportPath);
        }

        private static void ValidateRoleData(RoleVisualDatabase db, List<string> passed, List<string> blockers)
        {
            if (db == null) return;
            if (db.entries == null) { blockers.Add("Role database entries list is null."); return; }
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in db.entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.roleId)) { blockers.Add("Role database has an empty entry or role ID."); continue; }
                if (!ids.Add(entry.roleId)) blockers.Add("Duplicate role ID: " + entry.roleId);
                if (string.Equals(entry.roleId, "Thief", StringComparison.OrdinalIgnoreCase)) blockers.Add("Thief must not appear in normal pre-reveal role entries.");
                if (entry.visual == null) blockers.Add("Role entry has no visual data: " + entry.roleId);
            }
            foreach (var role in CourtRoles)
            {
                var data = db.Find(role);
                if (data == null) { blockers.Add("Missing court role data: " + role); continue; }
                Require(data.portrait, role + " portrait", blockers); Require(data.fullBody, role + " full body", blockers);
                Require(data.cardArt, role + " card art", blockers); Require(data.avatar, role + " avatar", blockers);
                Require(data.roleIcon, role + " icon", blockers); Require(data.silhouette, role + " silhouette", blockers);
            }
            var thief = db.thiefObjectiveRevealData;
            if (thief == null) blockers.Add("Missing reveal-only Thief art data.");
            else
            {
                Require(thief.portrait, "Thief reveal portrait", blockers); Require(thief.fullBody, "Thief reveal full body", blockers);
                Require(thief.cardArt, "Thief reveal card art", blockers); Require(thief.avatar, "Thief reveal avatar", blockers);
                Require(thief.roleIcon, "Thief reveal icon", blockers); Require(thief.silhouette, "Thief reveal silhouette", blockers);
            }
            Require(db.thiefObjectiveRevealArt, "Thief reveal-only objective art", blockers);
            if (db.Find("Thief") == null) passed.Add("Thief identity is excluded from normal role lookup and remains reveal-only.");
        }

        private static void ValidateCardData(CardVisualDatabase db, List<string> passed, List<string> blockers)
        {
            if (db == null) return;
            Require(db.universalBack, "Universal card back", blockers); Require(db.roleFrame, "Role card frame", blockers);
            Require(db.clueFrame, "Clue card frame", blockers); Require(db.scenarioFrame, "Scenario card frame", blockers);
            Require(db.revealFrame, "Reveal card frame", blockers); Require(db.clueArt, "Clue art", blockers);
            Require(db.scenarioArt, "Scenario art", blockers); Require(db.necklaceMaster, "Necklace master", blockers);
            Require(db.necklaceIcon, "Necklace icon", blockers); Require(db.necklaceReveal, "Necklace reveal art", blockers);
        }

        private static void ValidateSpaceData(SpaceVisualDatabase db, List<string> passed, List<string> blockers)
        {
            if (db == null) return;
            if (db.entries == null) { blockers.Add("Space database entries list is null."); return; }
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in db.entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.spaceId)) { blockers.Add("Space database has an empty entry or space ID."); continue; }
                if (!ids.Add(entry.spaceId)) blockers.Add("Duplicate space ID: " + entry.spaceId);
                if (entry.visual == null) blockers.Add("Space entry has no visual data: " + entry.spaceId);
            }
            foreach (var space in Spaces)
            {
                var data = db.Find(space);
                if (data == null) { blockers.Add("Missing palace space data: " + space); continue; }
                Require(data.background, space + " background", blockers); Require(data.midground, space + " midground", blockers);
                Require(data.foreground, space + " foreground", blockers); Require(data.thumbnail, space + " thumbnail", blockers);
                Require(data.lightingOverlay, space + " lighting overlay", blockers); Require(data.atmosphere, space + " atmosphere", blockers);
            }
        }

        private static void ValidatePropData(PropVisualDatabase db, List<string> blockers)
        {
            if (db == null) return;
            if (db.entries == null) { blockers.Add("Prop database entries list is null."); return; }
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in db.entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.propId)) { blockers.Add("Prop database has an empty entry or prop ID."); continue; }
                if (!ids.Add(entry.propId)) blockers.Add("Duplicate prop ID: " + entry.propId);
                if (entry.visual == null) blockers.Add("Prop entry has no visual data: " + entry.propId);
            }
            var necklace = db.Find("Necklace");
            if (necklace == null) { blockers.Add("Missing Necklace prop data."); return; }
            Require(necklace.worldSprite, "Necklace world prop", blockers);
            Require(necklace.cardSprite, "Necklace card prop", blockers);
            Require(necklace.icon, "Necklace icon", blockers);
        }

        private static void Require(UnityEngine.Object value, string label, List<string> blockers)
        {
            if (value == null) blockers.Add("Missing production reference: " + label);
        }

        private static void CheckReference(UnityEngine.Object value, string label, List<string> blockers)
        {
            if (value == null) blockers.Add("Release profile reference is empty: " + label);
        }

        private static void AssignIfNull<T>(ReleasePresentationProfile profile, string property, T value) where T : UnityEngine.Object
        {
            if (value == null) return;
            var serialized = new SerializedObject(profile);
            var field = serialized.FindProperty(property);
            if (field != null && field.objectReferenceValue == null)
            {
                field.objectReferenceValue = value;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void WriteSection(StringBuilder report, string title, IEnumerable<string> items)
        {
            report.AppendLine("## " + title);
            report.AppendLine();
            var list = items.ToArray();
            if (list.Length == 0) report.AppendLine("- None.");
            else foreach (var item in list) report.AppendLine("- " + item);
            report.AppendLine();
        }

        private static void EnsureFolder(string path)
        {
            path = path.TrimEnd('/');
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var leaf = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            if (!string.IsNullOrEmpty(parent) && AssetDatabase.IsValidFolder(parent)) AssetDatabase.CreateFolder(parent, leaf);
        }
    }

    /// <summary>Stops non-development builds unless the PV-FINAL release profile and visual art validate.</summary>
    public sealed class PVFinalReleaseBuildGate : IPreprocessBuildWithReport
    {
        public int callbackOrder => 10;
        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.options.HasFlag(BuildOptions.Development)) return;
            var profile = AssetDatabase.LoadAssetAtPath<ReleasePresentationProfile>("Assets/ThiefAndTheNecklace/Visual/Profiles/TAN_ReleasePresentationProfile.asset");
            if (profile == null) throw new BuildFailedException("PV-FINAL: release presentation profile is missing. Create it from the Visual menu.");
            if (profile.transitions == null || profile.roles == null || profile.cards == null || profile.spaces == null || profile.props == null)
                throw new BuildFailedException("PV-FINAL: release presentation profile has missing production references.");
            if (!profile.approvedForRelease)
                throw new BuildFailedException("PV-FINAL: visual release approval is not recorded in the presentation profile.");
            if (!EditorBuildSettings.scenes.Any(scene => scene.enabled && !string.IsNullOrWhiteSpace(scene.path)))
                throw new BuildFailedException("PV-FINAL: no enabled production scene is configured in Build Settings.");
            if (!PV3VisualProductionSetup.ValidateVisualArtContent())
                throw new BuildFailedException("PV-FINAL: production visual art is incomplete; see the Console and PV-FINAL release gate report.");
        }
    }
}
#endif
