using UnityEngine;
using UnityEngine.UIElements;
using ThiefAndTheNecklace.UI;

namespace ThiefAndTheNecklace.Visual
{
    /// <summary>Provides touch-safe, width-aware layout classes and applies accessibility classes.</summary>
    public sealed class VisualQualityController : MonoBehaviour
    {
        [SerializeField] private UIDocument document;
        [SerializeField, Min(320)] private int mobileLandscapeMaxWidth = 760;
        [SerializeField, Min(761)] private int tabletMaxWidth = 1100;
        private VisualElement _root;

        private void Awake()
        {
            if (document == null) document = GetComponent<UIDocument>();
            if (document == null) return;
            _root = document.rootVisualElement;
            UIPresentationSettings.Apply(_root);
            _root.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            ApplyLayout(_root.resolvedStyle.width);
        }

        private void OnDestroy()
        {
            if (_root != null) _root.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        }

        public void RefreshAccessibility()
        {
            if (_root != null) UIPresentationSettings.Apply(_root);
        }

        private void OnGeometryChanged(GeometryChangedEvent evt) => ApplyLayout(evt.newRect.width);

        private void ApplyLayout(float width)
        {
            if (_root == null || float.IsNaN(width) || width <= 0f) return;
            _root.EnableInClassList("tn-layout-mobile-landscape", width <= mobileLandscapeMaxWidth);
            _root.EnableInClassList("tn-layout-tablet", width > mobileLandscapeMaxWidth && width <= tabletMaxWidth);
            _root.EnableInClassList("tn-layout-desktop", width > tabletMaxWidth);
        }
    }
}
