using ThiefAndTheNecklace.Visual;
using ThiefAndTheNecklace.Visual.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ThiefAndTheNecklace.Visual.Editor
{
    /// <summary>Editor-only preview surface for checking PV1 components before screen integration.</summary>
    public sealed class VisualComponentLabWindow : EditorWindow
    {
        private const string VisualRoot = "Assets/ThiefAndTheNecklace/Visual/";

        [MenuItem("Thief & the Necklace/Visual/Component Lab")]
        public static void Open()
        {
            var window = GetWindow<VisualComponentLabWindow>();
            window.titleContent = new GUIContent("Visual Component Lab");
            window.minSize = new Vector2(820, 600);
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;
            root.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(VisualRoot + "UI/Foundations/Resources/DesignTokens.uss"));
            root.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(VisualRoot + "UI/USS/TANComponents.uss"));
            root.style.backgroundColor = VisualDesignTokens.DeepInk;
            root.style.paddingLeft = 20;
            root.style.paddingRight = 20;
            root.style.paddingTop = 16;
            root.style.paddingBottom = 16;

            var title = new Label("VISUAL COMPONENT LAB");
            title.style.color = VisualDesignTokens.AntiqueGold;
            title.style.fontSize = 24;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            root.Add(title);
            var subtitle = new Label("PV1 · component states · responsive preview");
            subtitle.style.color = VisualDesignTokens.Parchment;
            subtitle.style.marginBottom = 12;
            root.Add(subtitle);

            var preferences = new VisualElement();
            preferences.style.flexDirection = FlexDirection.Row;
            preferences.style.marginBottom = 8;
            var contrastToggle = new Toggle("High contrast");
            contrastToggle.RegisterValueChangedCallback(evt => root.EnableInClassList("tn-high-contrast", evt.newValue));
            preferences.Add(contrastToggle);
            var largeTextToggle = new Toggle("Large text");
            largeTextToggle.RegisterValueChangedCallback(evt => root.EnableInClassList("tn-large-text", evt.newValue));
            preferences.Add(largeTextToggle);
            var reducedMotionToggle = new Toggle("Reduced motion");
            reducedMotionToggle.RegisterValueChangedCallback(evt => root.EnableInClassList("tn-reduced-motion", evt.newValue));
            preferences.Add(reducedMotionToggle);
            root.Add(preferences);

            var scroll = new ScrollView();
            root.Add(scroll);
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            scroll.Add(row);

            AddGroup(row, "BUTTONS", column =>
            {
                AddButton(column, "Primary", VisualButtonVariant.Primary);
                AddButton(column, "Secondary", VisualButtonVariant.Secondary);
                AddButton(column, "Ghost", VisualButtonVariant.Ghost);
                AddButton(column, "Danger", VisualButtonVariant.Danger);
                AddButton(column, "Disabled", VisualButtonVariant.Secondary, disabled: true);
                AddButton(column, "Loading", VisualButtonVariant.Primary, loading: true);
                AddTemplate(column, "Icon Button", "UI/UXML/IconButton.uxml", element => new IconButtonView(element).Bind(null, "Open settings", null));
            });

            AddGroup(row, "CARDS & PANELS", column =>
            {
                AddTemplate(column, "Role Card · face up", "UI/UXML/RoleCard.uxml", element => new RoleCardView(element).Bind("KING", (Sprite)null, 4));
                AddTemplate(column, "Universal Card Back", "UI/UXML/RoleCard.uxml", element => new RoleCardView(element).ShowBack());
                AddTemplate(column, "Clue Card", "UI/UXML/ClueCard.uxml", element => new ClueCardView(element).Bind("OBSERVATION", "The eastern door stood open during the blackout.", "Witness account", null));
                AddTemplate(column, "Information Panel", "UI/UXML/TanPanel.uxml", element =>
                {
                    var panel = new TanPanel(element);
                    panel.SetHeader("SITUATION");
                    panel.Content?.Add(new Label("A concise readable information panel."));
                });
            });

            AddGroup(row, "PLAYERS & INDICATORS", column =>
            {
                AddTemplate(column, "Player Card", "UI/UXML/PlayerCard.uxml", element => new PlayerCardView(element).Bind(new PlayerCardViewModel { DisplayName = "Queen Elara", Status = "READY", Score = 12, Ready = true, Connected = true }));
                AddTemplate(column, "Disconnected Player", "UI/UXML/PlayerCard.uxml", element => new PlayerCardView(element).Bind(new PlayerCardViewModel { DisplayName = "Minister Rowan", Status = "WAITING", Connected = false }));
                AddTemplate(column, "Ready Badge", "UI/UXML/Badge.uxml", element => new BadgeView(element).Bind("READY", "success"));
                AddTemplate(column, "Discussion Timer", "UI/UXML/Timer.uxml", element => { var view = new TimerView(element); view.SetTime(135, 180); view.SetState(TimerVisualState.Normal); });
                AddTemplate(column, "Score", "UI/UXML/Score.uxml", element => new ScoreView(element).Bind(8, 24));
            });

            AddGroup(row, "CHAT & FEEDBACK", column =>
            {
                AddTemplate(column, "Chat Message", "UI/UXML/ChatMessage.uxml", element => new ChatMessageView(element).Bind(new ChatMessageViewModel { Sender = "Minister", Message = "I heard footsteps near the east corridor.", Timestamp = "02:15" }));
                AddTemplate(column, "Toast", "UI/UXML/Toast.uxml", element => new ToastView(element).Show("Vote submitted.", 60000));
                AddTemplate(column, "Modal", "UI/UXML/Modal.uxml", element => new ModalView(element).Open("LEAVE MATCH?"));
                AddTemplate(column, "Navigation", "UI/UXML/Navigation.uxml", element => new NavigationView(element).Bind("ROUND RESULTS", null));
            });
        }

        private static void AddButton(VisualElement parent, string label, VisualButtonVariant variant, bool disabled = false, bool loading = false)
        {
            var tree = Load("UI/UXML/TanButton.uxml");
            if (tree == null) return;
            var element = tree.Instantiate();
            var button = new TanButton(element);
            button.SetText(label);
            button.SetVariant(variant);
            button.SetEnabledState(!disabled);
            button.SetLoadingState(loading);
            parent.Add(element);
        }

        private static void AddGroup(VisualElement parent, string heading, System.Action<VisualElement> build)
        {
            var group = new VisualElement();
            group.style.width = 360;
            group.style.minHeight = 280;
            group.style.marginRight = 12;
            group.style.marginBottom = 12;
            group.style.paddingLeft = 12;
            group.style.paddingRight = 12;
            group.style.paddingTop = 12;
            group.style.paddingBottom = 12;
            group.AddToClassList("tn-panel");
            var title = new Label(heading);
            title.style.color = VisualDesignTokens.AntiqueGold;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 8;
            group.Add(title);
            build(group);
            parent.Add(group);
        }

        private static void AddTemplate(VisualElement parent, string caption, string assetPath, System.Action<VisualElement> bind)
        {
            var tree = Load(assetPath);
            if (tree == null) return;
            var preview = tree.Instantiate();
            bind?.Invoke(preview);
            var label = new Label(caption);
            label.style.color = VisualDesignTokens.Parchment;
            label.style.marginTop = 8;
            label.style.marginBottom = 4;
            parent.Add(label);
            parent.Add(preview);
        }

        private static VisualTreeAsset Load(string relativePath)
            => AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(VisualRoot + relativePath);
    }
}
