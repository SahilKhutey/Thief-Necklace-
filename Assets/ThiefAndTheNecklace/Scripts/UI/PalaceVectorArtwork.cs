using UnityEngine;
using UnityEngine.UIElements;
using ThiefAndTheNecklace.Visual;

namespace ThiefAndTheNecklace.UI
{
    /// <summary>Code-drawn, resolution-independent palace ornament for builds that do not ship raster art.</summary>
    public sealed class PalaceVectorArtwork : VisualElement
    {
        public enum Motif { Palace, Necklace }

        private readonly Motif _motif;
        private readonly string _spaceId;

        public PalaceVectorArtwork(Motif motif = Motif.Palace)
        {
            _motif = motif;
            _spaceId = string.Empty;
            style.width = Length.Percent(100);
            style.height = motif == Motif.Palace ? 190 : 150;
            style.minHeight = motif == Motif.Palace ? 140 : 110;
            style.marginTop = 12;
            style.marginBottom = 12;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        public PalaceVectorArtwork(string spaceId) : this(Motif.Palace)
        {
            _spaceId = spaceId ?? string.Empty;
        }

        private void Draw(MeshGenerationContext context)
        {
            var width = contentRect.width;
            var height = contentRect.height;
            if (width < 1 || height < 1) return;
            var painter = context.painter2D;
            painter.lineWidth = Mathf.Max(1.25f, width / 520f);
            painter.strokeColor = VisualDesignTokens.AntiqueGold;
            painter.fillColor = new Color(0.18f, 0.13f, 0.10f, 0.72f);

            if (_motif == Motif.Necklace)
            {
                DrawNecklace(painter, width, height);
                return;
            }

            var cx = width * 0.5f;
            var baseline = height * 0.82f;
            DrawLine(painter, width * 0.08f, baseline, width * 0.92f, baseline);
            DrawPalace(painter, cx, baseline, width, height);
            DrawArch(painter, cx, baseline, width * 0.33f, height * 0.67f);
            DrawArch(painter, cx, baseline, width * 0.20f, height * 0.46f);
            DrawWindow(painter, cx - width * 0.34f, baseline, width * 0.075f, height * 0.25f);
            DrawWindow(painter, cx + width * 0.34f, baseline, width * 0.075f, height * 0.25f);
            DrawSpaceDetail(painter, _spaceId, cx, baseline, width, height);
            DrawStar(painter, width * 0.18f, height * 0.24f, width * 0.012f);
            DrawStar(painter, width * 0.82f, height * 0.20f, width * 0.009f);
            DrawStar(painter, width * 0.74f, height * 0.38f, width * 0.006f);
        }

        private static void DrawPalace(Painter2D p, float cx, float baseline, float width, float height)
        {
            var left = width * 0.20f;
            var right = width * 0.80f;
            var roof = height * 0.34f;
            p.BeginPath();
            p.MoveTo(new Vector2(left, baseline));
            p.LineTo(new Vector2(left, roof + height * 0.13f));
            p.LineTo(new Vector2(cx - width * 0.12f, roof + height * 0.13f));
            p.LineTo(new Vector2(cx, roof));
            p.LineTo(new Vector2(cx + width * 0.12f, roof + height * 0.13f));
            p.LineTo(new Vector2(right, roof + height * 0.13f));
            p.LineTo(new Vector2(right, baseline));
            p.ClosePath();
            p.Fill();
            p.Stroke();

            DrawLine(p, left - width * 0.035f, roof + height * 0.13f, right + width * 0.035f, roof + height * 0.13f);
            DrawLine(p, left - width * 0.025f, baseline - height * 0.06f, right + width * 0.025f, baseline - height * 0.06f);
            for (var i = 0; i < 7; i++)
            {
                var x = Mathf.Lerp(left + width * 0.035f, right - width * 0.035f, i / 6f);
                DrawLine(p, x, roof + height * 0.16f, x, baseline - height * 0.07f);
            }
            DrawLine(p, cx, roof - height * 0.02f, cx, roof - height * 0.13f);
            DrawLine(p, cx - width * 0.035f, roof - height * 0.04f, cx + width * 0.035f, roof - height * 0.04f);
        }

        private static void DrawArch(Painter2D p, float cx, float baseline, float archWidth, float archHeight)
        {
            p.BeginPath();
            p.MoveTo(new Vector2(cx - archWidth * 0.5f, baseline));
            p.LineTo(new Vector2(cx - archWidth * 0.5f, baseline - archHeight * 0.55f));
            p.BezierCurveTo(new Vector2(cx - archWidth * 0.5f, baseline - archHeight * 0.92f),
                new Vector2(cx + archWidth * 0.5f, baseline - archHeight * 0.92f), new Vector2(cx + archWidth * 0.5f, baseline - archHeight * 0.55f));
            p.LineTo(new Vector2(cx + archWidth * 0.5f, baseline));
            p.Stroke();
        }

        private static void DrawWindow(Painter2D p, float cx, float baseline, float radius, float height)
        {
            p.BeginPath();
            p.MoveTo(new Vector2(cx - radius, baseline));
            p.LineTo(new Vector2(cx - radius, baseline - height * 0.65f));
            p.BezierCurveTo(new Vector2(cx - radius, baseline - height), new Vector2(cx + radius, baseline - height), new Vector2(cx + radius, baseline - height * 0.65f));
            p.LineTo(new Vector2(cx + radius, baseline));
            p.Stroke();
        }

        private static void DrawNecklace(Painter2D p, float width, float height)
        {
            var cx = width * 0.5f;
            var cy = height * 0.45f;
            p.BeginPath();
            p.MoveTo(new Vector2(cx - width * 0.22f, height * 0.08f));
            p.BezierCurveTo(new Vector2(cx - width * 0.18f, height * 0.78f), new Vector2(cx + width * 0.18f, height * 0.78f), new Vector2(cx + width * 0.22f, height * 0.08f));
            p.Stroke();
            var gem = new Vector2(cx, cy);
            p.BeginPath();
            p.MoveTo(new Vector2(gem.x, gem.y - height * 0.20f));
            p.LineTo(new Vector2(gem.x + width * 0.09f, gem.y - height * 0.03f));
            p.LineTo(new Vector2(gem.x + width * 0.07f, gem.y + height * 0.13f));
            p.LineTo(new Vector2(gem.x, gem.y + height * 0.21f));
            p.LineTo(new Vector2(gem.x - width * 0.07f, gem.y + height * 0.13f));
            p.LineTo(new Vector2(gem.x - width * 0.09f, gem.y - height * 0.03f));
            p.ClosePath();
            p.fillColor = VisualDesignTokens.Burgundy;
            p.Fill();
            p.Stroke();
            p.fillColor = new Color(0.18f, 0.13f, 0.10f, 0.72f);
            DrawStar(p, cx + width * 0.16f, height * 0.48f, width * 0.018f);
        }

        private static void DrawSpaceDetail(Painter2D p, string spaceId, float cx, float baseline, float width, float height)
        {
            switch ((spaceId ?? string.Empty).ToLowerInvariant())
            {
                case "chamber":
                case "royalchamber":
                    FillOval(p, new Color(.78f, .60f, .27f), cx, height * .31f, width * .026f, height * .055f);
                    DrawLine(p, cx - width * .10f, height * .44f, cx - width * .10f, baseline);
                    DrawLine(p, cx + width * .10f, height * .44f, cx + width * .10f, baseline);
                    break;
                case "treasury":
                    FillShape(p, new Color(.43f, .28f, .14f),
                        new Vector2(cx - width * .13f, baseline - height * .16f), new Vector2(cx - width * .13f, baseline - height * .29f),
                        new Vector2(cx - width * .07f, baseline - height * .36f), new Vector2(cx + width * .07f, baseline - height * .36f),
                        new Vector2(cx + width * .13f, baseline - height * .29f), new Vector2(cx + width * .13f, baseline - height * .16f));
                    DrawLine(p, cx - width * .13f, baseline - height * .23f, cx + width * .13f, baseline - height * .23f);
                    DrawStar(p, cx, baseline - height * .29f, width * .014f);
                    break;
                case "minister_office":
                    for (var i = 0; i < 3; i++)
                    {
                        var x = cx - width * .10f + i * width * .10f;
                        DrawLine(p, x, baseline - height * .39f, x, baseline - height * .12f);
                        DrawLine(p, x - width * .025f, baseline - height * .32f, x + width * .025f, baseline - height * .32f);
                        DrawLine(p, x - width * .025f, baseline - height * .24f, x + width * .025f, baseline - height * .24f);
                        DrawLine(p, x - width * .025f, baseline - height * .16f, x + width * .025f, baseline - height * .16f);
                    }
                    break;
                case "east_corridor":
                case "eastcorridor":
                    DrawLine(p, cx - width * .25f, baseline - height * .34f, cx - width * .08f, baseline);
                    DrawLine(p, cx + width * .25f, baseline - height * .34f, cx + width * .08f, baseline);
                    DrawLine(p, cx - width * .25f, baseline - height * .34f, cx + width * .25f, baseline - height * .34f);
                    break;
                case "garden":
                    DrawLine(p, cx - width * .30f, baseline, cx - width * .30f, baseline - height * .32f);
                    DrawLine(p, cx + width * .30f, baseline, cx + width * .30f, baseline - height * .32f);
                    p.fillColor = new Color(.24f, .39f, .29f);
                    FillOval(p, p.fillColor, cx - width * .30f, baseline - height * .35f, width * .075f, height * .13f);
                    FillOval(p, p.fillColor, cx + width * .30f, baseline - height * .35f, width * .075f, height * .13f);
                    DrawLine(p, cx - width * .12f, baseline, cx + width * .12f, baseline);
                    break;
                case "west_gate":
                case "westgate":
                    for (var i = -2; i <= 2; i++)
                    {
                        var x = cx + i * width * .045f;
                        DrawLine(p, x, baseline - height * .31f, x, baseline);
                    }
                    DrawLine(p, cx - width * .12f, baseline - height * .22f, cx + width * .12f, baseline - height * .22f);
                    break;
                default: // Grand hall / banquet hall: a formal chandelier above the central arch.
                    DrawLine(p, cx, height * .12f, cx, height * .28f);
                    DrawLine(p, cx - width * .09f, height * .28f, cx + width * .09f, height * .28f);
                    for (var i = -1; i <= 1; i++)
                    {
                        var x = cx + i * width * .075f;
                        DrawLine(p, x, height * .28f, x, height * .37f);
                        DrawStar(p, x, height * .39f, width * .009f);
                    }
                    break;
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
            p.fillColor = color;
            p.BeginPath();
            p.MoveTo(points[0]);
            for (var i = 1; i < points.Length; i++) p.LineTo(points[i]);
            p.ClosePath();
            p.Fill();
            p.Stroke();
        }

        private static void DrawStar(Painter2D p, float x, float y, float r)
        {
            p.BeginPath();
            p.MoveTo(new Vector2(x, y - r));
            p.LineTo(new Vector2(x, y + r));
            p.MoveTo(new Vector2(x - r, y));
            p.LineTo(new Vector2(x + r, y));
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
