using UnityEngine.UIElements;

namespace ThiefAndTheNecklace.Visual.UI
{
    /// <summary>Common presentation state contract. Implementations never own gameplay state.</summary>
    public interface IVisualStateComponent
    {
        void SetEnabledState(bool enabled);
        void SetSelectedState(bool selected);
        void SetLoadingState(bool loading);
    }

    public abstract class VisualStateComponentBase : IVisualStateComponent
    {
        private bool _enabled = true;
        private bool _loading;
        protected VisualElement Root { get; }
        public VisualElement Element => Root;

        protected VisualStateComponentBase(VisualElement root)
        {
            Root = root;
        }

        public virtual void SetEnabledState(bool enabled)
        {
            _enabled = enabled;
            if (Root != null) Root.SetEnabled(_enabled && !_loading);
        }

        public virtual void SetSelectedState(bool selected)
        {
            if (Root != null) Root.EnableInClassList("tan-is-selected", selected);
        }

        public virtual void SetLoadingState(bool loading)
        {
            if (Root == null) return;
            _loading = loading;
            Root.EnableInClassList("tan-is-loading", loading);
            Root.SetEnabled(_enabled && !_loading);
        }
    }
}
