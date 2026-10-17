using UnityEngine.UIElements;

namespace ThiefAndTheNecklace.Visual.UI
{
    public sealed class ScoreViewModel
    {
        public int RoundDelta;
        public int TotalScore;
    }

    public sealed class ScoreView : VisualStateComponentBase
    {
        private readonly Label _roundDelta;
        private readonly Label _total;
        public ScoreView(VisualElement element) : base(element?.Q<VisualElement>("score") ?? element)
        {
            _roundDelta = element?.Q<Label>("round-delta");
            _total = element?.Q<Label>("total");
        }

        public void Bind(int roundDelta, int totalScore)
        {
            if (_roundDelta != null) _roundDelta.text = roundDelta > 0 ? "+" + roundDelta : roundDelta.ToString();
            if (_total != null) _total.text = totalScore.ToString();
            VisualEventBus.Raise(VisualEventType.ScoreUpdated);
        }

        public void Bind(ScoreViewModel data)
        {
            if (data != null) Bind(data.RoundDelta, data.TotalScore);
        }
    }
}
