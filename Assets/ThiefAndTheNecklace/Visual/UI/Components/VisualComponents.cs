using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ThiefAndTheNecklace.Visual
{
    /// <summary>UI Toolkit building blocks. Components render presentation only; callers own game rules.</summary>
    public enum VisualButtonVariant { Primary, Secondary, Ghost, Danger }
    public enum VisualPanelVariant { Standard, Information, Cinematic }

    public static class VisualComponents
    {
        public static VisualElement Panel(string className = null)
            => Panel(VisualPanelVariant.Standard, className);

        public static VisualElement Panel(VisualPanelVariant variant, string className = null)
        {
            var panel = new VisualElement();
            panel.AddToClassList("tn-panel");
            panel.AddToClassList("tn-card");
            panel.AddToClassList("tn-panel-" + variant.ToString().ToLowerInvariant());
            if (!string.IsNullOrEmpty(className)) panel.AddToClassList(className);
            return panel;
        }

        public static Button Button(string text, Action clicked, bool primary = false)
            => Button(text, clicked, primary ? VisualButtonVariant.Primary : VisualButtonVariant.Secondary);

        public static Button Button(string text, Action clicked, VisualButtonVariant variant)
        {
            var button = new Button(clicked) { text = text };
            button.AddToClassList("tn-button");
            button.AddToClassList("tn-focus");
            button.AddToClassList("tn-button-" + variant.ToString().ToLowerInvariant());
            var primary = variant == VisualButtonVariant.Primary;
            var ghost = variant == VisualButtonVariant.Ghost;
            var danger = variant == VisualButtonVariant.Danger;
            button.style.color = primary ? VisualDesignTokens.DeepInk : VisualDesignTokens.WarmIvory;
            button.style.backgroundColor = primary ? VisualDesignTokens.AntiqueGold : danger ? VisualDesignTokens.Burgundy : ghost ? Color.clear : VisualDesignTokens.InkSoft;
            button.style.borderTopColor = danger ? VisualDesignTokens.Error : VisualDesignTokens.AntiqueGold;
            button.style.borderRightColor = button.style.borderTopColor.value;
            button.style.borderBottomColor = button.style.borderTopColor.value;
            button.style.borderLeftColor = button.style.borderTopColor.value;
            button.style.borderTopWidth = ghost ? 0 : VisualDesignTokens.BorderSubtle;
            button.style.borderRightWidth = ghost ? 0 : VisualDesignTokens.BorderSubtle;
            button.style.borderBottomWidth = ghost ? 0 : VisualDesignTokens.BorderSubtle;
            button.style.borderLeftWidth = ghost ? 0 : VisualDesignTokens.BorderSubtle;
            button.style.fontSize = 15;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.RegisterCallback<PointerEnterEvent>(_ =>
            {
                if (!UIPresentationSettings.ReducedMotion && button.enabledSelf) button.style.translate = new Translate(0, -2);
            });
            button.RegisterCallback<PointerLeaveEvent>(_ => button.style.translate = new Translate(0, 0));
            button.RegisterCallback<FocusInEvent>(_ => button.style.borderBottomWidth = 3);
            button.RegisterCallback<FocusOutEvent>(_ => button.style.borderBottomWidth = ghost ? 0 : VisualDesignTokens.BorderSubtle);
            return button;
        }

        public static Label Text(string text, string className)
        {
            var label = new Label(text);
            label.AddToClassList(className);
            return label;
        }

        public static void SetState(VisualElement element, VisualState state)
        {
            if (element == null) return;
            foreach (VisualState candidate in Enum.GetValues(typeof(VisualState)))
                element.EnableInClassList("tn-state-" + candidate.ToString().ToLowerInvariant(), candidate == state);
        }

        /// <summary>Neutral card shell for role, clue, and scenario data; no game state is inferred.</summary>
        public static VisualElement Card(string title, string description, VisualElement artwork = null)
        {
            var card = Panel("tn-card-component");
            if (artwork != null) card.Add(artwork);
            if (!string.IsNullOrEmpty(title)) card.Add(Text(title, "tn-headline"));
            if (!string.IsNullOrEmpty(description)) card.Add(Text(description, "tn-body"));
            return card;
        }

        /// <summary>Reusable player presentation shell for lobby, table, voting, and results variants.</summary>
        public static VisualElement PlayerPanel(string name, string status, VisualElement portrait = null)
        {
            var panel = Panel("tn-player-panel");
            if (portrait != null) panel.Add(portrait);
            panel.Add(Text(name ?? string.Empty, "tn-headline"));
            panel.Add(Text(status ?? string.Empty, "tn-caption"));
            return panel;
        }
    }
}
