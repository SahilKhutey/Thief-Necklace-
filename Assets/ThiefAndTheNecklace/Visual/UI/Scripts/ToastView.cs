using UnityEngine.UIElements;

namespace ThiefAndTheNecklace.Visual.UI
{
    public sealed class ToastView : VisualStateComponentBase
    {
        private readonly Label _message;
        private IVisualElementScheduledItem _dismissSchedule;

        public ToastView(VisualElement element) : base(element?.Q<VisualElement>("toast") ?? element)
        {
            _message = element?.Q<Label>("message");
            if (Root != null) Root.style.display = DisplayStyle.None;
        }

        public void Show(string message, long durationMilliseconds = 2800)
        {
            if (Root == null) return;
            if (_message != null) _message.text = message ?? string.Empty;
            Root.style.display = DisplayStyle.Flex;
            _dismissSchedule?.Pause();
            _dismissSchedule = Root.schedule.Execute(() => Root.style.display = DisplayStyle.None).StartingIn(durationMilliseconds);
            VisualEventBus.Raise(VisualEventType.ToastShown);
        }

        public void Dismiss()
        {
            _dismissSchedule?.Pause();
            if (Root != null) Root.style.display = DisplayStyle.None;
        }
    }
}
