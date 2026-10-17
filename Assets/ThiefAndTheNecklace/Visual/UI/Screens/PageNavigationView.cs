using System.Collections.Generic;
using UnityEngine.UIElements;

namespace ThiefAndTheNecklace.Visual.UI
{
    /// <summary>Keyboard and pointer navigation for a screen containing child pages.</summary>
    public sealed class PageNavigationView
    {
        private readonly List<VisualElement> _pages = new List<VisualElement>();
        private readonly PageController _controller = new PageController();
        private readonly Label _indicator;
        private readonly Button _previous;
        private readonly Button _next;

        public PageNavigationView(VisualElement root)
        {
            var container = root?.Q<VisualElement>("page-container");
            _indicator = root?.Q<Label>("page-indicator");
            _previous = root?.Q<Button>("page-prev");
            _next = root?.Q<Button>("page-next");
            if (container != null) _pages.AddRange(container.Children());
            _controller.PageChanged += ShowPage;
            if (_previous != null) _previous.clicked += _controller.Previous;
            if (_next != null) _next.clicked += () => _controller.Next(_pages.Count);
            ShowPage(0);
        }

        private void ShowPage(int page)
        {
            for (var i = 0; i < _pages.Count; i++) _pages[i].style.display = i == page ? DisplayStyle.Flex : DisplayStyle.None;
            if (_indicator != null) _indicator.text = _pages.Count == 0 ? "0 / 0" : (page + 1) + " / " + _pages.Count;
            if (_previous != null) _previous.SetEnabled(page > 0);
            if (_next != null) _next.SetEnabled(page < _pages.Count - 1);
        }
    }
}
