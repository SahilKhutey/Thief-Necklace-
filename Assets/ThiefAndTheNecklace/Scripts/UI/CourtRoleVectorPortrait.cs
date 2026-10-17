using UnityEngine;
using UnityEngine.UIElements;
using ThiefAndTheNecklace.Visual;

namespace ThiefAndTheNecklace.UI
{
    /// <summary>Small, consistent court-character busts drawn as scalable vector paths.</summary>
    public sealed class CourtRoleVectorPortrait : VisualElement
    {
        private readonly string _roleId;

        public CourtRoleVectorPortrait(string roleId)
        {
            _roleId = roleId ?? string.Empty;
            style.width = 112;
            style.height = 136;
            style.alignSelf = Align.Center;
            style.marginTop = 8;
            style.marginBottom = 8;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        private void Draw(MeshGenerationContext context)
        {
            var p = context.painter2D;
            var w = contentRect.width;
            var h = contentRect.height;
            if (w < 1 || h < 1) return;
            var cloth = ClothColor(_roleId);
            p.lineWidth = 1.5f;
            p.strokeColor = VisualDesignTokens.AntiqueGold;

            // A shared face, collar, and shoulder silhouette keeps every role in one art family.
            FillShape(p, cloth,
                new Vector2(w * .12f, h * .96f), new Vector2(w * .17f, h * .70f),
                new Vector2(w * .34f, h * .59f), new Vector2(w * .66f, h * .59f),
                new Vector2(w * .83f, h * .70f), new Vector2(w * .88f, h * .96f));
            FillShape(p, VisualDesignTokens.WarmIvory,
                new Vector2(w * .42f, h * .62f), new Vector2(w * .40f, h * .49f),
                new Vector2(w * .60f, h * .49f), new Vector2(w * .58f, h * .62f));
            FillOval(p, new Color(.72f, .48f, .32f), w * .5f, h * .36f, w * .19f, h * .25f);
            FillShape(p, new Color(.20f, .14f, .13f),
                new Vector2(w * .31f, h * .34f), new Vector2(w * .33f, h * .20f),
                new Vector2(w * .43f, h * .12f), new Vector2(w * .61f, h * .14f),
                new Vector2(w * .69f, h * .26f), new Vector2(w * .65f, h * .31f),
                new Vector2(w * .57f, h * .23f), new Vector2(w * .43f, h * .25f));
            DrawLine(p, w * .43f, h * .37f, w * .46f, h * .37f);
            DrawLine(p, w * .54f, h * .37f, w * .57f, h * .37f);
            DrawLine(p, w * .47f, h * .46f, w * .53f, h * .46f);
            DrawLine(p, w * .5f, h * .69f, w * .5f, h * .90f);
            DrawLine(p, w * .40f, h * .69f, w * .5f, h * .77f);
            DrawLine(p, w * .60f, h * .69f, w * .5f, h * .77f);

            // A restrained role seal differentiates court stations without using a Thief-specific mark.
            var badgeY = h * .83f;
            p.fillColor = VisualDesignTokens.AntiqueGold;
            p.BeginPath();
            p.MoveTo(new Vector2(w * .5f, badgeY - h * .055f));
            p.LineTo(new Vector2(w * .55f, badgeY));
            p.LineTo(new Vector2(w * .5f, badgeY + h * .055f));
            p.LineTo(new Vector2(w * .45f, badgeY));
            p.ClosePath();
            p.Fill();
        }

        private static Color ClothColor(string id)
        {
            switch (id.ToLowerInvariant())
            {
                case "king": return new Color(.34f, .22f, .12f);
                case "queen": return new Color(.42f, .18f, .25f);
                case "minister": return new Color(.20f, .30f, .28f);
                case "soldier": return new Color(.25f, .31f, .40f);
                case "guard": return new Color(.27f, .29f, .34f);
                case "servant": return new Color(.37f, .29f, .20f);
                default: return VisualDesignTokens.RoyalSurface;
            }
        }

        private static void FillOval(Painter2D p, Color color, float cx, float cy, float rx, float ry)
        {
            const float k = .5522848f;
            p.fillColor = color;
            p.BeginPath();
            p.MoveTo(new Vector2(cx, cy - ry));
            p.BezierCurveTo(new Vector2(cx + k * rx, cy - ry), new Vector2(cx + rx, cy - k * ry), new Vector2(cx + rx, cy));
            p.BezierCurveTo(new Vector2(cx + rx, cy + k * ry), new Vector2(cx + k * rx, cy + ry), new Vector2(cx, cy + ry));
            p.BezierCurveTo(new Vector2(cx - k * rx, cy + ry), new Vector2(cx - rx, cy + k * ry), new Vector2(cx - rx, cy));
            p.BezierCurveTo(new Vector2(cx - rx, cy - k * ry), new Vector2(cx - k * rx, cy - ry), new Vector2(cx, cy - ry));
            p.ClosePath();
            p.Fill();
            p.Stroke();
        }

        private static void FillShape(Painter2D p, Color color, params Vector2[] points)
        {
            if (points == null || points.Length < 3) return;
            p.fillColor = color;
            p.BeginPath();
            p.MoveTo(points[0]);
            for (var i = 1; i < points.Length; i++) p.LineTo(points[i]);
            p.ClosePath();
            p.Fill();
            p.Stroke();
        }

        private static void DrawLine(Painter2D p, float x1, float y1, float x2, float y2)
        {
            p.BeginPath();
            p.MoveTo(new Vector2(x1, y1));
            p.LineTo(new Vector2(x2, y2));
            p.Stroke();
        }
    }
}
