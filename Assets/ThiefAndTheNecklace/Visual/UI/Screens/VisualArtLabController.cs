using UnityEngine;
using UnityEngine.UIElements;

namespace ThiefAndTheNecklace.Visual.UI
{
    /// <summary>Interactive, read-only gallery for reviewing imported PV3 art in the art lab scene.</summary>
    public sealed class VisualArtLabController : MonoBehaviour
    {
        private enum Gallery { Characters, Cards, Spaces, Props }
        [SerializeField] private UIDocument document;
        [SerializeField] private RoleVisualDatabase roleDatabase;
        [SerializeField] private CardVisualDatabase cardDatabase;
        [SerializeField] private SpaceVisualDatabase spaceDatabase;
        [SerializeField] private PropVisualDatabase propDatabase;

        private static readonly string[] Roles = { "King", "Queen", "Minister", "Soldier", "Guard", "Servant", "Thief" };
        private static readonly string[] Spaces = { "BanquetHall", "RoyalChamber", "Treasury", "Garden", "EastCorridor", "WestGate" };
        private static readonly string[] Props = { "Necklace", "RoyalCrown", "Chair", "Table", "Candle", "Goblet", "Chest", "Cabinet", "Curtain", "Torch", "Keys", "GuardEquipment", "Documents", "Books" };

        private Gallery gallery;
        private int index;
        private bool faceUp = true;
        private bool safeArea;
        private bool bounds;
        private bool lighting = true;
        private bool parallax = true;
        private bool vfx = true;
        private float scale = 1f;
        private VisualElement stage;
        private VisualElement background;
        private VisualElement midground;
        private VisualElement foreground;
        private VisualElement art;
        private VisualElement safeGuide;
        private VisualElement boundsGuide;
        private VisualElement effectMark;
        private Label title;

        private void Start()
        {
            if (document == null) document = GetComponent<UIDocument>();
            BuildInterface();
        }

        private void BuildInterface()
        {
            var root = document.rootVisualElement;
            root.Clear();
            root.style.flexDirection = FlexDirection.Column;
            root.style.paddingLeft = root.style.paddingRight = 20;
            root.style.paddingTop = root.style.paddingBottom = 16;
            root.style.backgroundColor = new Color(0.10f, 0.075f, 0.06f);
            root.Add(new Label("VISUAL ART LAB") { style = { color = new Color(0.88f, 0.72f, 0.43f), fontSize = 24, unityFontStyleAndWeight = FontStyle.Bold } });
            root.Add(new Label("PV3 production art review · art assets only; gameplay state is not simulated") { style = { color = new Color(0.90f, 0.86f, 0.77f), marginBottom = 12 } });

            var controls = Row(root);
            AddControl(controls, "Characters", () => SetGallery(Gallery.Characters));
            AddControl(controls, "Cards", () => SetGallery(Gallery.Cards));
            AddControl(controls, "Spaces", () => SetGallery(Gallery.Spaces));
            AddControl(controls, "Props", () => SetGallery(Gallery.Props));
            AddControl(controls, "◀ Previous", () => Step(-1));
            AddControl(controls, "Next ▶", () => Step(1));
            AddControl(controls, "Front / Back", () => { faceUp = !faceUp; Render(); });
            AddControl(controls, "Small", () => SetScale(0.65f));
            AddControl(controls, "Medium", () => SetScale(0.82f));
            AddControl(controls, "Large", () => SetScale(1f));

            var toggles = Row(root);
            AddToggle(toggles, "Light", () => lighting = !lighting);
            AddToggle(toggles, "Parallax", () => parallax = !parallax);
            AddToggle(toggles, "VFX", () => vfx = !vfx);
            AddToggle(toggles, "Safe Area", () => safeArea = !safeArea);
            AddToggle(toggles, "Bounds", () => bounds = !bounds);

            title = new Label { style = { color = Color.white, fontSize = 20, marginTop = 10, marginBottom = 8 } };
            root.Add(title);
            stage = new VisualElement();
            stage.style.flexGrow = 1;
            stage.style.position = Position.Relative;
            stage.style.overflow = Overflow.Hidden;
            stage.style.alignItems = Align.Center;
            stage.style.justifyContent = Justify.Center;
            stage.style.backgroundColor = new Color(0.16f, 0.12f, 0.09f);
            root.Add(stage);
            background = Layer(stage, "Space background");
            midground = Layer(stage, "Space midground");
            foreground = Layer(stage, "Space foreground");
            art = new VisualElement { name = "Card or character art" };
            art.style.width = 360;
            art.style.height = 520;
            art.style.backgroundColor = new Color(0.24f, 0.19f, 0.14f);
            art.style.unityBackgroundImageTintColor = Color.white;
            art.style.backgroundPositionX = BackgroundPositionKeyword.Center;
            art.style.backgroundPositionY = BackgroundPositionKeyword.Center;
            art.style.backgroundRepeat = new BackgroundRepeat(Repeat.NoRepeat, Repeat.NoRepeat);
            art.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            stage.Add(art);
            safeGuide = Guide(stage, new Color(0.3f, 0.95f, 0.6f));
            boundsGuide = Guide(stage, new Color(1f, 0.34f, 0.2f));
            effectMark = new Label("✦");
            effectMark.style.position = Position.Absolute;
            effectMark.style.right = 28;
            effectMark.style.top = 22;
            effectMark.style.fontSize = 32;
            effectMark.style.color = new Color(0.96f, 0.79f, 0.42f);
            stage.Add(effectMark);
            Render();
        }

        private void Render()
        {
            if (stage == null) return;
            stage.style.backgroundColor = lighting ? new Color(0.16f, 0.12f, 0.09f) : new Color(0.035f, 0.03f, 0.025f);
            stage.style.borderTopWidth = stage.style.borderRightWidth = stage.style.borderBottomWidth = stage.style.borderLeftWidth = bounds ? 2 : 0;
            stage.style.borderTopColor = stage.style.borderRightColor = stage.style.borderBottomColor = stage.style.borderLeftColor = new Color(1f, 0.34f, 0.2f);
            safeGuide.style.display = safeArea ? DisplayStyle.Flex : DisplayStyle.None;
            boundsGuide.style.display = bounds ? DisplayStyle.Flex : DisplayStyle.None;
            effectMark.style.display = vfx ? DisplayStyle.Flex : DisplayStyle.None;
            background.style.display = midground.style.display = foreground.style.display = DisplayStyle.None;
            art.style.display = DisplayStyle.Flex;
            art.style.width = 360 * scale;
            art.style.height = 520 * scale;
            art.style.backgroundImage = StyleKeyword.None;

            switch (gallery)
            {
                case Gallery.Characters: RenderCharacter(); break;
                case Gallery.Cards: RenderCard(); break;
                case Gallery.Spaces: RenderSpace(); break;
                default: RenderProp(); break;
            }
        }

        private void RenderCharacter()
        {
            var id = Roles[index % Roles.Length];
            var data = id == "Thief" ? roleDatabase?.thiefObjectiveRevealData : roleDatabase?.Find(id);
            title.text = id + (id == "Thief" ? " · reveal-only" : "");
            var sprite = data == null ? null : faceUp ? data.cardArt : cardDatabase?.universalBack;
            Show(sprite, art);
        }

        private void RenderCard()
        {
            var families = new[] { "Role", "Clue", "Scenario", "Reveal" };
            title.text = "Card family · " + (faceUp ? families[index % families.Length] : "Universal Back");
            var sprite = !faceUp ? cardDatabase?.universalBack : (index % families.Length) switch
            {
                0 => cardDatabase?.roleFrame,
                1 => cardDatabase?.clueFrame,
                2 => cardDatabase?.scenarioFrame,
                _ => cardDatabase?.revealFrame
            };
            Show(sprite, art);
        }

        private void RenderSpace()
        {
            var id = Spaces[index % Spaces.Length];
            var data = spaceDatabase == null ? null : spaceDatabase.Find(id);
            title.text = id;
            art.style.display = DisplayStyle.None;
            Show(data == null ? null : data.background, background);
            Show(data == null ? null : data.midground, midground);
            Show(data == null ? null : data.foreground, foreground);
            background.style.display = DisplayStyle.Flex;
            midground.style.display = parallax ? DisplayStyle.Flex : DisplayStyle.None;
            foreground.style.display = parallax ? DisplayStyle.Flex : DisplayStyle.None;
            background.style.opacity = lighting ? 1f : 0.55f;
        }

        private void RenderProp()
        {
            var id = Props[index % Props.Length];
            var data = propDatabase == null ? null : propDatabase.Find(id);
            title.text = id;
            Show(data == null ? null : data.worldSprite, art);
        }

        private void SetGallery(Gallery value) { gallery = value; index = 0; Render(); }
        private void Step(int amount)
        {
            var count = gallery == Gallery.Characters ? Roles.Length : gallery == Gallery.Spaces ? Spaces.Length : gallery == Gallery.Props ? Props.Length : 4;
            index = (index + amount + count) % count;
            Render();
        }
        private void SetScale(float value) { scale = value; Render(); }
        private static void Show(Sprite sprite, VisualElement element)
        {
            var missing = element.Q<Label>("art-missing");
            if (sprite == null || sprite.texture == null)
            {
                element.style.backgroundImage = StyleKeyword.None;
                element.style.backgroundColor = new Color(0.32f, 0.10f, 0.16f);
                if (missing == null)
                {
                    missing = new Label("MISSING ART\n" + element.name) { name = "art-missing" };
                    missing.style.position = Position.Absolute;
                    missing.style.left = missing.style.right = missing.style.top = missing.style.bottom = 0;
                    missing.style.alignItems = Align.Center;
                    missing.style.justifyContent = Justify.Center;
                    missing.style.color = new Color(1f, 0.93f, 0.79f);
                    missing.style.unityTextAlign = TextAnchor.MiddleCenter;
                    missing.style.whiteSpace = WhiteSpace.Normal;
                    element.Add(missing);
                }
                return;
            }

            missing?.RemoveFromHierarchy();
            element.style.backgroundColor = Color.clear;
            element.style.backgroundImage = new StyleBackground(sprite);
        }
        private static VisualElement Row(VisualElement parent)
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap, marginBottom = 6 } };
            parent.Add(row);
            return row;
        }
        private static void AddControl(VisualElement parent, string label, System.Action action)
        {
            var button = new Button(action) { text = label };
            button.style.marginRight = 5;
            button.style.marginBottom = 4;
            parent.Add(button);
        }
        private static void AddToggle(VisualElement parent, string label, System.Action action) => AddControl(parent, "Toggle " + label, action);
        private static VisualElement Layer(VisualElement parent, string label)
        {
            var layer = new VisualElement { name = label };
            layer.style.position = Position.Absolute;
            layer.style.left = 0;
            layer.style.right = 0;
            layer.style.top = 0;
            layer.style.bottom = 0;
            layer.style.backgroundPositionX = BackgroundPositionKeyword.Center;
            layer.style.backgroundPositionY = BackgroundPositionKeyword.Center;
            layer.style.backgroundRepeat = new BackgroundRepeat(Repeat.NoRepeat, Repeat.NoRepeat);
            layer.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            layer.pickingMode = PickingMode.Ignore;
            parent.Add(layer);
            return layer;
        }
        private static VisualElement Guide(VisualElement parent, Color color)
        {
            var guide = new VisualElement();
            guide.style.position = Position.Absolute;
            guide.style.left = Length.Percent(20);
            guide.style.right = Length.Percent(20);
            guide.style.top = Length.Percent(12);
            guide.style.bottom = Length.Percent(12);
            guide.style.borderTopWidth = guide.style.borderRightWidth = guide.style.borderBottomWidth = guide.style.borderLeftWidth = 2;
            guide.style.borderTopColor = guide.style.borderRightColor = guide.style.borderBottomColor = guide.style.borderLeftColor = color;
            guide.pickingMode = PickingMode.Ignore;
            guide.style.display = DisplayStyle.None;
            parent.Add(guide);
            return guide;
        }
    }
}
