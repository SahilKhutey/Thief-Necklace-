using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using ThiefAndTheNecklace.Visual;
using ThiefAndTheNecklace.Visual.UI;
using UnityEditor;
using UnityEngine;

namespace ThiefAndTheNecklace.Visual.Editor
{
    public static class PV1AssetSetup
    {
        private const string VisualRoot = "Assets/ThiefAndTheNecklace/Visual/";

        [MenuItem("Thief & the Necklace/Visual/Create PV1 Foundation Assets")]
        public static void CreateMissingAssets()
        {
            EnsureAsset<TanThemeTokens>("UI/Foundations/Theme/TAN_ThemeTokens.asset");
            EnsureAsset<TanColorPalette>("UI/Foundations/Colors/TAN_ColorPalette.asset");
            EnsureAsset<RoleVisualDatabase>("ArtData/TAN_RoleVisualDatabase.asset");
            EnsureAsset<CardVisualDatabase>("ArtData/TAN_CardVisualDatabase.asset");
            EnsureAsset<SpaceVisualDatabase>("ArtData/TAN_SpaceVisualDatabase.asset");
            AssetDatabase.SaveAssets();
            SyncThemeToRuntimeStylesheet();
            AssetDatabase.Refresh();
            Debug.Log("PV1 theme and visual database assets are ready.");
        }

        [MenuItem("Thief & the Necklace/Visual/Sync Theme To Runtime Stylesheet")]
        public static void SyncThemeToRuntimeStylesheet()
        {
            var palette = AssetDatabase.LoadAssetAtPath<TanColorPalette>(VisualRoot + "UI/Foundations/Colors/TAN_ColorPalette.asset");
            var theme = AssetDatabase.LoadAssetAtPath<TanThemeTokens>(VisualRoot + "UI/Foundations/Theme/TAN_ThemeTokens.asset");
            if (palette == null || theme == null)
            {
                Debug.LogWarning("Create the PV1 foundation assets before syncing the runtime theme.");
                return;
            }

            var path = VisualRoot + "UI/Foundations/Resources/DesignTokens.uss";
            var source = File.ReadAllText(path);
            var variables = new StringBuilder();
            variables.AppendLine(":root {");
            AddColor(variables, "royal-ink", palette.Ink);
            AddColor(variables, "royal-ink-soft", VisualDesignTokens.InkSoft);
            AddColor(variables, "royal-surface", VisualDesignTokens.RoyalSurface);
            AddColor(variables, "royal-surface-raised", VisualDesignTokens.SurfaceRaised);
            AddColor(variables, "royal-ivory", palette.Ivory);
            AddColor(variables, "royal-parchment", palette.TextSecondary);
            AddColor(variables, "royal-gold", palette.Gold);
            AddColor(variables, "royal-burgundy", palette.Burgundy);
            AddColor(variables, "royal-forest", palette.Forest);
            AddColor(variables, "royal-midnight", palette.Midnight);
            AddColor(variables, "royal-success", palette.Success);
            AddColor(variables, "royal-warning", palette.Warning);
            AddColor(variables, "royal-error", palette.Error);
            AddColor(variables, "royal-info", palette.Info);
            AddColor(variables, "royal-disabled", palette.Disabled);
            AddColor(variables, "royal-border", new Color32(137, 107, 61, 255));
            AddColor(variables, "royal-muted-surface", new Color32(81, 72, 62, 255));
            AddColor(variables, "royal-clue-surface", new Color32(241, 229, 197, 255));
            variables.AppendLine("    --royal-overlay: rgba(33,24,19,0.94);");
            variables.AppendLine("    --royal-gold-line: rgba(198,154,69,0.55);");
            var spacing = new[] { theme.Space1, theme.Space2, theme.Space3, theme.Space4, theme.Space5, theme.Space6, theme.Space7, theme.Space8, theme.Space9 };
            for (var i = 0; i < spacing.Length; i++) variables.AppendLine($"    --space-{i + 1}: {spacing[i]:0.##}px;");
            variables.AppendLine($"    --radius-sm: {theme.RadiusSmall:0.##}px;");
            variables.AppendLine($"    --radius-md: {theme.RadiusMedium:0.##}px;");
            variables.AppendLine($"    --radius-lg: {theme.RadiusLarge:0.##}px;");
            variables.AppendLine($"    --motion-fast: {theme.MotionFast:0.###}s;");
            variables.AppendLine($"    --motion-normal: {theme.MotionNormal:0.###}s;");
            variables.AppendLine($"    --motion-slow: {theme.MotionSlow:0.###}s;");
            variables.AppendLine($"    --motion-cinematic: {theme.MotionCinematic:0.###}s;");
            variables.Append("}");

            var updated = Regex.Replace(source, @":root\s*\{.*?\}", variables.ToString(), RegexOptions.Singleline);
            File.WriteAllText(path, updated, new UTF8Encoding(false));
            AssetDatabase.Refresh();
        }

        private static void EnsureAsset<T>(string relativePath) where T : ScriptableObject
        {
            var path = VisualRoot + relativePath;
            if (AssetDatabase.LoadAssetAtPath<T>(path) != null) return;
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
        }

        private static void AddColor(StringBuilder builder, string token, Color color)
        {
            builder.AppendLine($"    --{token}: #{ColorUtility.ToHtmlStringRGB(color)};");
        }
    }
}
