#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Linq;
using ThiefAndTheNecklace.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace ThiefAndTheNecklace.Visual.UI
{
    /// <summary>Development-only screen diagnostics. Release builds exclude this class.</summary>
    public sealed class VisualDebugHud
    {
        private readonly VisualElement _root;
        private readonly VisualElement _documentRoot;
        private readonly System.Func<ScreenId> _screen;
        private readonly System.Func<VisualOverlayId?> _overlay;
        private readonly Label _readout;

        public VisualDebugHud(VisualElement root, System.Func<ScreenId> screen, System.Func<VisualOverlayId?> overlay)
        {
            _documentRoot = root;
            _root = new VisualElement { name = "visual-debug-hud", pickingMode = PickingMode.Ignore };
            _root.style.position = Position.Absolute;
            _root.style.right = 8;
            _root.style.top = 8;
            _root.style.paddingLeft = 10;
            _root.style.paddingRight = 10;
            _root.style.paddingTop = 8;
            _root.style.paddingBottom = 8;
            _root.style.backgroundColor = new Color(0.08f, 0.06f, 0.05f, 0.9f);
            _readout = new Label();
            _readout.style.color = Color.white;
            _readout.style.fontSize = 11;
            _root.Add(_readout);
            root.Add(_root);
            _screen = screen;
            _overlay = overlay;
            _root.schedule.Execute(Refresh).Every(500);
            Refresh();
        }

        private void Refresh()
        {
            if (_root.panel == null) return;
            var overlay = _overlay();
            var count = _documentRoot.Query<VisualElement>().ToList().Count(IsVisible);
            var fps = Time.unscaledDeltaTime > 0 ? 1f / Time.unscaledDeltaTime : 0f;
            _readout.text = $"VISUAL DEBUG\nScreen: {_screen()}\nOverlay: {(overlay.HasValue ? overlay.Value.ToString() : "None")}\nResolution: {_documentRoot.layout.width:0}×{_documentRoot.layout.height:0}\nScale: {UIPresentationSettings.TextScale:0.00}\nFPS: {fps:0}\nActive Components: {count}";
        }

        private static bool IsVisible(VisualElement element)
        {
            for (var current = element; current != null; current = current.parent)
                if (current.resolvedStyle.display == DisplayStyle.None) return false;
            return true;
        }
    }
}
#endif
