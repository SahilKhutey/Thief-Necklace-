using System;

namespace ThiefAndTheNecklace.Visual.UI
{
    public sealed class PageController
    {
        public int CurrentPage { get; private set; }
        public event Action<int> PageChanged;
        public void SetPage(int page, int pageCount)
        {
            if (pageCount <= 0) throw new ArgumentOutOfRangeException(nameof(pageCount));
            var next = Math.Max(0, Math.Min(page, pageCount - 1));
            if (next == CurrentPage) return;
            CurrentPage = next;
            PageChanged?.Invoke(next);
        }
        public void Next(int pageCount) => SetPage(CurrentPage + 1, pageCount);
        public void Previous()
        {
            if (CurrentPage <= 0) return;
            CurrentPage--;
            PageChanged?.Invoke(CurrentPage);
        }
    }
}
