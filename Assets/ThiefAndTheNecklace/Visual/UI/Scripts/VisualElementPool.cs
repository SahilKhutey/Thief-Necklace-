using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace ThiefAndTheNecklace.Visual.UI
{
    /// <summary>Small pool for frequently reused UXML instances such as chat rows and cards.</summary>
    public sealed class VisualElementPool<T> where T : VisualElement
    {
        private readonly Func<T> _factory;
        private readonly Action<T> _reset;
        private readonly int _capacity;
        private readonly Stack<T> _available = new Stack<T>();
        private readonly HashSet<T> _pooled = new HashSet<T>();

        public VisualElementPool(Func<T> factory, int prewarm = 0, int capacity = 64, Action<T> reset = null)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _capacity = Math.Max(1, capacity);
            _reset = reset;
            for (var i = 0; i < Math.Min(Math.Max(0, prewarm), _capacity); i++) Release(_factory());
        }

        public T Acquire()
        {
            if (_available.Count == 0) return _factory();
            var element = _available.Pop();
            _pooled.Remove(element);
            element.style.display = DisplayStyle.Flex;
            return element;
        }

        public void Release(T element)
        {
            if (element == null || _pooled.Contains(element)) return;
            _reset?.Invoke(element);
            element.RemoveFromHierarchy();
            if (_available.Count >= _capacity) return;
            element.style.display = DisplayStyle.None;
            _available.Push(element);
            _pooled.Add(element);
        }
    }
}
