#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ThiefAndTheNecklace.PB1;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ThiefAndTheNecklace.Visual.Editor
{
    /// <summary>Creates the editable PV3 data assets and validates content before a player build.</summary>
    public static class PV3VisualProductionSetup
    {
        private const string Root = "Assets/ThiefAndTheNecklace/Visual/";
        private static readonly RoleType[] CourtRoles =
        {
            RoleType.King, RoleType.Queen, RoleType.Minister,
            RoleType.Soldier, RoleType.Guard, RoleType.Servant
        };
        private static readonly string[] SpaceIds =
        {
            "BanquetHall", "RoyalChamber", "Treasury", "Garden", "EastCorridor", "WestGate"
        };
        private static readonly string[] PropIds =
        {
            "Necklace", "RoyalCrown", "Chair", "Table", "Candle", "Goblet", "Chest",
            "Cabinet", "Curtain", "Torch", "Keys", "GuardEquipment", "Documents", "Books"
        };

        [MenuItem("Thief & the Necklace/Visual/Build PV3 Visual Data Assets")]
        public static void BuildVisualDataAssets()
        {
            EnsureFolders();
            ConfigureArtworkImporters();
            var roleDb = EnsureAsset<RoleVisualDatabase>("ArtData/TAN_RoleVisualDatabase.asset");
            var cardDb = EnsureAsset<CardVisualDatabase>("ArtData/TAN_CardVisualDatabase.asset");
            var spaceDb = EnsureAsset<SpaceVisualDatabase>("ArtData/TAN_SpaceVisualDatabase.asset");
            var propDb = EnsureAsset<PropVisualDatabase>("ArtData/TAN_PropVisualDatabase.asset");

            foreach (var role in CourtRoles)
            {
                var id = role.ToString();
                var visual = EnsureAsset<RoleVisualData>($"ArtData/Roles/TAN_Role_{id}.asset");
                visual.roleId = id;
                visual.artDirection = ArtDirectionFor(role);
                PopulateRoleArt(visual, "Characters/" + id + "/TAN_Role_" + id + "_");
                EditorUtility.SetDirty(visual);
                Upsert(roleDb.entries, id, visual);
            }

            var thiefReveal = EnsureAsset<RoleVisualData>("ArtData/Roles/TAN_Role_Thief_RevealOnly.asset");
            thiefReveal.roleId = "ThiefObjectiveReveal";
            thiefReveal.artDirection = "A plausible courtly appearance drawn in the same visual family as the six court roles. No mask, dark costume, suspicious lighting, unique palette, or other pre-reveal guilt tell.";
            PopulateRoleArt(thiefReveal, "Characters/Thief/TAN_Thief_Reveal_");
            roleDb.thiefObjectiveRevealData = thiefReveal;
            roleDb.thiefObjectiveRevealArt = thiefReveal.cardArt;
            EditorUtility.SetDirty(thiefReveal);

            foreach (var id in SpaceIds)
            {
                var visual = EnsureAsset<SpaceVisualData>($"ArtData/Spaces/TAN_Space_{id}.asset");
                visual.spaceId = id;
                PopulateSpaceArt(visual, id);
                EditorUtility.SetDirty(visual);
                Upsert(spaceDb.entries, id, visual);
            }

            foreach (var id in PropIds)
            {
                var visual = EnsureAsset<PropVisualData>($"ArtData/Props/TAN_Prop_{id}.asset");
                visual.propId = id;
                var sprite = LoadSprite(PropSpritePath(id));
                if (sprite != null) visual.worldSprite = sprite;
                if (id == "Necklace")
                {
                    if (sprite != null) visual.cardSprite = sprite;
                    var icon = LoadSprite(Root + "Props/Necklace/TAN_Necklace_Icon.png");
                    if (icon != null) visual.icon = icon;
                }
                Upsert(propDb.entries, id, visual);
                EditorUtility.SetDirty(visual);
            }

            // The Thief is an objective over a court role. Keep its art in the reveal-only slot,
            // never in the normal role lookup list that feeds private role/player views.
            if (thiefReveal.cardArt != null) roleDb.thiefObjectiveRevealArt = thiefReveal.cardArt;

            AssignIfPresent(LoadSprite(Root + "Cards/Common/TAN_Card_UniversalBack.png"), value => cardDb.universalBack = value);
            AssignIfPresent(LoadSprite(Root + "Cards/Role/TAN_Card_RoleFrame.png"), value => cardDb.roleFrame = value);
            AssignIfPresent(LoadSprite(Root + "Cards/Clue/TAN_Card_ClueFrame.png"), value => cardDb.clueFrame = value);
            AssignIfPresent(LoadSprite(Root + "Cards/Scenario/TAN_Card_ScenarioFrame.png"), value => cardDb.scenarioFrame = value);
            AssignIfPresent(LoadSprite(Root + "Cards/Role/TAN_Card_RevealFrame.png"), value => cardDb.revealFrame = value);
            AssignIfPresent(LoadSprite(Root + "Cards/Clue/TAN_Card_ClueArt.png"), value => cardDb.clueArt = value);
            AssignIfPresent(LoadSprite(Root + "Cards/Scenario/TAN_Card_ScenarioArt.png"), value => cardDb.scenarioArt = value);
            AssignIfPresent(LoadSprite(Root + "Props/Necklace/TAN_Necklace_Master.png"), value => cardDb.necklaceMaster = value);
            AssignIfPresent(LoadSprite(Root + "Props/Necklace/TAN_Necklace_Icon.png"), value => cardDb.necklaceIcon = value);
            AssignIfPresent(LoadSprite(Root + "Props/Necklace/TAN_Necklace_Reveal.png"), value => cardDb.necklaceReveal = value);
            EditorUtility.SetDirty(roleDb);
            EditorUtility.SetDirty(cardDb);
            EditorUtility.SetDirty(spaceDb);
            EditorUtility.SetDirty(propDb);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("PV3 data assets are ready. Populate the source PNG slots, then run Validate PV3 Visual Art.");
        }

        [MenuItem("Thief & the Necklace/Visual/Validate PV3 Visual Art")]
        public static void ValidateVisualArt()
            => ValidateVisualArtContent();

        internal static bool ValidateVisualArtContent()
        {
            EnsureFolders();
            var errors = new List<string>();
            var roleDb = AssetDatabase.LoadAssetAtPath<RoleVisualDatabase>(Root + "ArtData/TAN_RoleVisualDatabase.asset");
            var cardDb = AssetDatabase.LoadAssetAtPath<CardVisualDatabase>(Root + "ArtData/TAN_CardVisualDatabase.asset");
            var spaceDb = AssetDatabase.LoadAssetAtPath<SpaceVisualDatabase>(Root + "ArtData/TAN_SpaceVisualDatabase.asset");
            var propDb = AssetDatabase.LoadAssetAtPath<PropVisualDatabase>(Root + "ArtData/TAN_PropVisualDatabase.asset");
            if (roleDb == null) errors.Add("Missing TAN_RoleVisualDatabase.asset; run Build PV3 Visual Data Assets.");
            if (cardDb == null) errors.Add("Missing TAN_CardVisualDatabase.asset; run Build PV3 Visual Data Assets.");
            if (spaceDb == null) errors.Add("Missing TAN_SpaceVisualDatabase.asset; run Build PV3 Visual Data Assets.");
            if (propDb == null) errors.Add("Missing TAN_PropVisualDatabase.asset; run Build PV3 Visual Data Assets.");

            if (roleDb != null)
            {
                foreach (var role in CourtRoles)
                {
                    var data = roleDb.Find(role.ToString());
                    if (data == null) { errors.Add($"Missing role data: {role}"); continue; }
                    Require(data.portrait, $"{role} portrait", errors);
                    Require(data.cardArt, $"{role} card art", errors);
                    Require(data.avatar, $"{role} avatar", errors);
                    Require(data.roleIcon, $"{role} icon", errors);
                    Require(data.fullBody, $"{role} full body", errors);
                    Require(data.silhouette, $"{role} silhouette", errors);
                }
                if (roleDb.thiefObjectiveRevealData == null) errors.Add("Missing reveal-only Thief visual data.");
                else
                {
                    Require(roleDb.thiefObjectiveRevealData.portrait, "Thief reveal portrait", errors);
                    Require(roleDb.thiefObjectiveRevealData.fullBody, "Thief reveal full body", errors);
                    Require(roleDb.thiefObjectiveRevealData.cardArt, "Thief reveal card art", errors);
                    Require(roleDb.thiefObjectiveRevealData.avatar, "Thief reveal avatar", errors);
                    Require(roleDb.thiefObjectiveRevealData.roleIcon, "Thief reveal icon", errors);
                    Require(roleDb.thiefObjectiveRevealData.silhouette, "Thief reveal silhouette", errors);
                }
                Require(roleDb.thiefObjectiveRevealArt, "Thief objective reveal art (reveal-only)", errors);
            }

            if (cardDb != null)
            {
                Require(cardDb.roleFrame, "Role card frame", errors);
                Require(cardDb.clueFrame, "Clue card frame", errors);
                Require(cardDb.scenarioFrame, "Scenario card frame", errors);
                Require(cardDb.universalBack, "Universal card back", errors);
                Require(cardDb.clueArt, "Clue card art", errors);
                Require(cardDb.scenarioArt, "Scenario card art", errors);
                Require(cardDb.necklaceMaster, "Necklace master art", errors);
                Require(cardDb.necklaceIcon, "Necklace icon", errors);
                Require(cardDb.necklaceReveal, "Necklace reveal art", errors);
            }

            if (spaceDb != null)
            {
                foreach (var id in SpaceIds)
                {
                    var data = spaceDb.Find(id);
                    if (data == null) { errors.Add($"Missing space data: {id}"); continue; }
                    Require(data.background, $"{id} background", errors);
                    Require(data.midground, $"{id} midground", errors);
                    Require(data.thumbnail, $"{id} thumbnail", errors);
                }
            }

            if (propDb != null)
            {
                foreach (var id in PropIds)
                {
                    var data = propDb.Find(id);
                    if (data == null) { errors.Add($"Missing prop data: {id}"); continue; }
                    if (id == "Necklace")
                    {
                        Require(data.worldSprite, "Necklace world prop", errors);
                        Require(data.cardSprite, "Necklace card illustration", errors);
                        Require(data.icon, "Necklace icon", errors);
                    }
                }
            }

            if (errors.Count == 0) Debug.Log("PV3 visual art validation passed.");
            else foreach (var error in errors) Debug.LogError("PV3 visual art: " + error);
            return errors.Count == 0;
        }

        private static void EnsureFolders()
        {
            foreach (var path in new[]
            {
                "ArtData/Roles", "ArtData/Spaces", "ArtData/Props", "Characters/Thief", "Cards/Common", "Cards/Role",
                "Cards/Clue", "Cards/Scenario", "Props/Necklace", "Spaces/BanquetHall", "Spaces/RoyalChamber",
                "Spaces/Treasury", "Spaces/Garden", "Spaces/EastCorridor", "Spaces/WestGate",
                "Props/Royal", "Props/Furniture", "Props/Environment", "Backgrounds"
            })
            {
                var full = Root + path;
                if (!AssetDatabase.IsValidFolder(full))
                    Directory.CreateDirectory(full);
            }
            AssetDatabase.Refresh();
        }

        private static T EnsureAsset<T>(string relativePath) where T : ScriptableObject
        {
            var path = Root + relativePath;
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void Upsert(List<RoleVisualEntry> entries, string id, RoleVisualData data)
        {
            var entry = entries.FirstOrDefault(item => item != null && string.Equals(item.roleId, id, StringComparison.OrdinalIgnoreCase));
            if (entry == null) entries.Add(new RoleVisualEntry { roleId = id, visual = data });
            else { entry.roleId = id; entry.visual = data; }
        }

        private static void Upsert(List<SpaceVisualEntry> entries, string id, SpaceVisualData data)
        {
            var entry = entries.FirstOrDefault(item => item != null && string.Equals(item.spaceId, id, StringComparison.OrdinalIgnoreCase));
            if (entry == null) entries.Add(new SpaceVisualEntry { spaceId = id, visual = data });
            else { entry.spaceId = id; entry.visual = data; }
        }

        private static void Upsert(List<PropVisualEntry> entries, string id, PropVisualData data)
        {
            var entry = entries.FirstOrDefault(item => item != null && string.Equals(item.propId, id, StringComparison.OrdinalIgnoreCase));
            if (entry == null) entries.Add(new PropVisualEntry { propId = id, visual = data });
            else { entry.propId = id; entry.visual = data; }
        }

        private static Sprite LoadSprite(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

        private static string PropSpritePath(string id)
        {
            if (id == "Necklace") return Root + "Props/Necklace/TAN_Necklace_Master.png";
            var folder = id == "RoyalCrown" ? "Royal" :
                id == "Chair" || id == "Table" || id == "Candle" || id == "Goblet" || id == "Chest" || id == "Cabinet" ? "Furniture" : "Environment";
            return Root + "Props/" + folder + "/TAN_Prop_" + id + ".png";
        }

        private static void PopulateRoleArt(RoleVisualData data, string filenamePrefix)
        {
            AssignIfPresent(LoadSprite(Root + filenamePrefix + "Portrait.png"), value => data.portrait = value);
            AssignIfPresent(LoadSprite(Root + filenamePrefix + "FullBody.png"), value => data.fullBody = value);
            AssignIfPresent(LoadSprite(Root + filenamePrefix + "CardArt.png"), value => data.cardArt = value);
            AssignIfPresent(LoadSprite(Root + filenamePrefix + "Avatar.png"), value => data.avatar = value);
            AssignIfPresent(LoadSprite(Root + filenamePrefix + "Icon.png"), value => data.roleIcon = value);
            AssignIfPresent(LoadSprite(Root + filenamePrefix + "Silhouette.png"), value => data.silhouette = value);
        }

        private static void PopulateSpaceArt(SpaceVisualData data, string id)
        {
            var prefix = Root + "Spaces/" + id + "/TAN_Space_" + id + "_";
            AssignIfPresent(LoadSprite(prefix + "Background.png"), value => data.background = value);
            AssignIfPresent(LoadSprite(prefix + "Midground.png"), value => data.midground = value);
            AssignIfPresent(LoadSprite(prefix + "Foreground.png"), value => data.foreground = value);
            AssignIfPresent(LoadSprite(prefix + "Thumbnail.png"), value => data.thumbnail = value);
            AssignIfPresent(LoadSprite(prefix + "Lighting.png"), value => data.lightingOverlay = value);
            AssignIfPresent(LoadSprite(prefix + "Atmosphere.png"), value => data.atmosphere = value);
        }

        private static void ConfigureArtworkImporters()
        {
            var paths = AssetDatabase.FindAssets("t:Texture2D", new[] { Root })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase));
            foreach (var path in paths)
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                var changed = importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single ||
                              importer.filterMode != FilterMode.Bilinear || importer.wrapMode != TextureWrapMode.Clamp ||
                              importer.mipmapEnabled || importer.textureCompression != TextureImporterCompression.Compressed || importer.maxTextureSize > 2048;
                if (!changed) continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.compressionQuality = 90;
                importer.maxTextureSize = 2048;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
        }

        private static void AssignIfPresent(Sprite sprite, Action<Sprite> assign)
        {
            if (sprite != null) assign(sprite);
        }

        private static void Require(Sprite sprite, string name, List<string> errors)
        {
            if (sprite == null || sprite.texture == null) errors.Add("Missing sprite: " + name);
        }

        private static string ArtDirectionFor(RoleType role)
        {
            switch (role)
            {
                case RoleType.King: return "Broad shoulders, long royal coat, crown and ceremonial staff; composed authority.";
                case RoleType.Queen: return "Elegant flowing formal dress, tiara, jewelry and court fan; graceful, observant composure.";
                case RoleType.Minister: return "Layered formal robe, document folio and measured posture; strategic court official.";
                case RoleType.Soldier: return "Field-ready armor, shoulder protection, sword and shield insignia; disciplined military stance.";
                case RoleType.Guard: return "Short ceremonial palace armor, helmet and keys or lantern; visibly distinct from combat Soldier.";
                case RoleType.Servant: return "Simple court uniform, apron and service tray or folded cloth; lighter silhouette.";
                case RoleType.Thief: return "Reveal-only objective appearance: plausible courtly costume, identical rendering and visual status to a normal court role; no guilt signaling.";
                default: return string.Empty;
            }
        }
    }

    public sealed class PV3BuildArtValidator : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;
        public void OnPreprocessBuild(BuildReport report)
        {
            // Keep development builds available while art is in production. Release builds must be complete.
            if (report.summary.options.HasFlag(BuildOptions.Development)) return;
            if (!PV3VisualProductionSetup.ValidateVisualArtContent())
                throw new BuildFailedException("PV3 visual art validation failed; see the Console for missing assets.");
        }
    }
}
#endif
