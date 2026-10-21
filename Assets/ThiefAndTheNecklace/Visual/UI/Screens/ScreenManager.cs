using System;
using System.Collections.Generic;
using System.Linq;
using ThiefAndTheNecklace.UI;
using ThiefAndTheNecklace.Visual;
using UnityEngine;
using UnityEngine.UIElements;

namespace ThiefAndTheNecklace.Visual.UI
{
    /// <summary>Owns page switching only. Gameplay decisions remain in the game runtime.</summary>
    public sealed class ScreenManager : MonoBehaviour
    {
        [Serializable] private sealed class ScreenEntry { public ScreenId Id; public VisualTreeAsset View; }
        [Serializable] private sealed class OverlayEntry { public VisualOverlayId Id; public VisualTreeAsset View; }
        [SerializeField] private UIDocument document;
        [SerializeField] private List<ScreenEntry> screens = new List<ScreenEntry>();
        [SerializeField] private List<OverlayEntry> overlays = new List<OverlayEntry>();
        [SerializeField] private float transitionSeconds = 0.18f;
        [SerializeField] private TransitionProfile transitionProfile;
        private readonly Dictionary<ScreenId, VisualElement> _views = new Dictionary<ScreenId, VisualElement>();
        private readonly Dictionary<ScreenId, ToastController> _toastControllers = new Dictionary<ScreenId, ToastController>();
        private VisualElement _screenLayer;
        private VisualElement _transitionCurtain;
        private VisualElement _inputBlocker;
        private VisualOverlayManager _overlays;
        private VotingOverlayController _voting;
        private ChatOverlayController _chat;
        private readonly List<PageNavigationView> _pageViews = new List<PageNavigationView>();
        private readonly List<RoomEntryController> _roomForms = new List<RoomEntryController>();
        private readonly Stack<ScreenId> _history = new Stack<ScreenId>();
        private LobbyScreenController _lobby;
        private PrivateRoleScreenController _privateRole;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private VisualDebugHud _debugHud;
#endif
        private bool _switching;
        private bool _hasCurrent;
        private bool _goingBack;
        private bool _cinematicInputLocked;
        private bool _transitionInputLocked;
        public ScreenId Current { get; private set; }
        public event Action<ScreenId, ScreenId> ScreenChanged;

        private void Awake()
        {
            if (document == null) document = GetComponent<UIDocument>();
            if (document == null) { Debug.LogError("ScreenManager requires a UIDocument.", this); enabled = false; return; }
            var root = document.rootVisualElement;
            root.AddToClassList("tn-screen-host");
            root.style.flexGrow = 1;
            UIPresentationSettings.Apply(root);
            root.RegisterCallback<GeometryChangedEvent>(evt => ApplyResponsiveLayout(root, evt.newRect.width));
            var sheet = Resources.Load<StyleSheet>("DesignTokens");
            if (sheet != null && !root.styleSheets.Contains(sheet)) root.styleSheets.Add(sheet);
            _screenLayer = new VisualElement { name = "screen-layer" };
            _screenLayer.AddToClassList("tn-screen-layer");
            _screenLayer.style.flexGrow = 1;
            _screenLayer.style.width = Length.Percent(100);
            root.Add(_screenLayer);
            var overlayLayer = new VisualElement { name = "overlay-layer" };
            overlayLayer.AddToClassList("tn-overlay-layer");
            overlayLayer.style.position = Position.Absolute;
            overlayLayer.style.left = 0;
            overlayLayer.style.right = 0;
            overlayLayer.style.top = 0;
            overlayLayer.style.bottom = 0;
            root.Add(overlayLayer);
            _overlays = new VisualOverlayManager(overlayLayer);
            _transitionCurtain = new VisualElement { name = "screen-transition-curtain", pickingMode = PickingMode.Ignore };
            _transitionCurtain.style.position = Position.Absolute;
            _transitionCurtain.style.left = 0; _transitionCurtain.style.right = 0;
            _transitionCurtain.style.top = 0; _transitionCurtain.style.bottom = 0;
            _transitionCurtain.style.backgroundColor = new StyleColor(Color.black);
            _transitionCurtain.style.opacity = 0;
            _transitionCurtain.style.display = DisplayStyle.None;
            root.Add(_transitionCurtain);
            _inputBlocker = new VisualElement { name = "cinematic-input-lock", pickingMode = PickingMode.Position };
            _inputBlocker.style.position = Position.Absolute;
            _inputBlocker.style.left = 0; _inputBlocker.style.right = 0;
            _inputBlocker.style.top = 0; _inputBlocker.style.bottom = 0;
            _inputBlocker.style.display = DisplayStyle.None;
            root.Add(_inputBlocker);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Debug.isDebugBuild) _debugHud = new VisualDebugHud(root, () => Current, () => _overlays.Topmost);
#endif
            foreach (var entry in overlays)
            {
                if (entry == null) continue;
                _overlays.Register(entry.Id, entry.View);
                var overlayView = _overlays.GetView(entry.Id);
                BindAccessibilityControls(overlayView, root);
                if (overlayView != null)
                    foreach (var button in overlayView.Query<Button>().ToList())
                        if (button.name == "go-main-menu") button.clicked += () => { _overlays.HideAll(); Show(ScreenId.MainMenu); };
            }
            var votingView = _overlays.GetView(VisualOverlayId.Voting);
            if (votingView != null)
            {
                _voting = new VotingOverlayController(votingView);
                _voting.VoteSubmitted += OnVoteSubmitted;
            }
            var chatView = _overlays.GetView(VisualOverlayId.Chat);
            if (chatView != null)
            {
                _chat = new ChatOverlayController(chatView);
                _chat.MessageSubmitted += value => ChatMessageSubmitted?.Invoke(value);
            }
            foreach (var entry in screens)
            {
                if (entry == null || entry.View == null) continue;
                var view = entry.View.CloneTree();
                view.name = "screen-" + entry.Id;
                view.AddToClassList("tn-production-screen");
                UIPresentationSettings.Apply(view);
                view.style.display = DisplayStyle.None;
                _screenLayer.Add(view);
                _views[entry.Id] = view;
                var toastHost = new VisualElement { name = "toast-host", pickingMode = PickingMode.Ignore };
                toastHost.style.position = Position.Absolute;
                toastHost.style.right = 24;
                toastHost.style.bottom = 24;
                toastHost.style.width = 320;
                toastHost.style.maxWidth = Length.Percent(90);
                toastHost.style.flexDirection = FlexDirection.Column;
                toastHost.style.alignItems = Align.FlexEnd;
                (view.Q<VisualElement>("screen-root") ?? view).Add(toastHost);
                _toastControllers[entry.Id] = new ToastController(toastHost);
                BindNavigation(view, entry.Id);
                if (entry.Id == ScreenId.CreateRoom || entry.Id == ScreenId.JoinRoom)
                {
                    var form = new RoomEntryController(view, entry.Id == ScreenId.JoinRoom);
                    form.RoomRequested += OnRoomRequested;
                    _roomForms.Add(form);
                }
                if (entry.Id == ScreenId.Lobby)
                {
                    _lobby = new LobbyScreenController(view);
                    _lobby.ReadyChanged += OnLobbyReadyChanged;
                    _lobby.StartRequested += OnLobbyStartRequested;
                }
                if (entry.Id == ScreenId.PrivateRole)
                {
                    _privateRole = new PrivateRoleScreenController(view);
                    _privateRole.ContinueRequested += OnPrivateRoleContinueRequested;
                }
                if (view.Q<VisualElement>("page-container") != null) _pageViews.Add(new PageNavigationView(view));
                BindAccessibilityControls(view, root);
                if (entry.Id == ScreenId.MainMenu)
                {
                    var back = view.Q<Button>("screen-back");
                    if (back != null) back.style.display = DisplayStyle.None;
                }
            }
            if (_views.ContainsKey(ScreenId.MainMenu)) Show(ScreenId.MainMenu, false);
            else if (_views.Count > 0) Show(FirstScreen(), false);
        }

        private ScreenId FirstScreen() { foreach (var id in _views.Keys) return id; return ScreenId.MainMenu; }

        public bool Show(ScreenId screen, bool animate = true)
            => Show(screen, animate ? DefaultTransition(screen) : ScreenTransitionType.None);

        public bool Show(ScreenId screen, ScreenTransitionType transition)
        {
            if (_switching || !_views.TryGetValue(screen, out var next)) return false;
            var previous = Current;
            if (!_goingBack && _hasCurrent && previous != screen) _history.Push(previous);
            var shouldAnimate = transition != ScreenTransitionType.None && !UIPresentationSettings.ReducedMotion && _hasCurrent && previous != screen;
            if (shouldAnimate && (transition == ScreenTransitionType.Cinematic || transition == ScreenTransitionType.Curtain || transition == ScreenTransitionType.CardReveal || transition == ScreenTransitionType.ScoreTransition))
            {
                StartCurtainTransition(previous, screen, next, transition);
                return true;
            }
            if (shouldAnimate && previous != screen)
            {
                StartExitTransition(previous, screen, next, transition);
                return true;
            }
            return CommitScreen(previous, screen, next, shouldAnimate, transition);
        }

        private bool CommitScreen(ScreenId previous, ScreenId screen, VisualElement next, bool animate, ScreenTransitionType transitionType = ScreenTransitionType.None)
        {
            _hasCurrent = true;
            VisualEventBus.Raise(VisualEventType.ScreenClosed);
            foreach (var pair in _views) pair.Value.style.display = DisplayStyle.None;
            next.style.display = DisplayStyle.Flex;
            next.style.opacity = animate ? 0f : 1f;
            if (animate && transitionType == ScreenTransitionType.DirectionalSlide) next.style.translate = new Translate(24f, 0f);
            if (animate) AnimateMenuEntry(next, screen);
            Current = screen;
            VisualEventBus.Raise(VisualEventType.ScreenOpened);
            if (screen == ScreenId.ScenarioIntro) VisualEventBus.Raise(VisualEventType.ScenarioShown);
            else if (screen == ScreenId.PrivateRole) _privateRole?.Reset();
            else if (screen == ScreenId.Reveal)
            {
                VisualEventBus.Raise(VisualEventType.RevealStarted);
                AnimateRevealCard(next);
            }
            else if (screen == ScreenId.RoundResults) VisualEventBus.Raise(VisualEventType.RoundResultShown);
            else if (screen == ScreenId.MatchResults) VisualEventBus.Raise(VisualEventType.MatchResultShown);
            ScreenChanged?.Invoke(previous, screen);
            if (animate)
            {
                _switching = true;
                SetTransitionInputLocked(true);
                var start = Time.realtimeSinceStartup;
                var duration = transitionProfile != null ? transitionProfile.DurationFor(transitionType) : transitionSeconds;
                IVisualElementScheduledItem transition = null;
                transition = next.schedule.Execute(() =>
                {
                    if (next.panel == null) { _switching = false; SetTransitionInputLocked(false); transition?.Pause(); return; }
                    var t = duration <= 0 ? 1f : Mathf.Clamp01((Time.realtimeSinceStartup - start) / duration);
                    if (transitionProfile != null && transitionProfile.Curve != null) t = transitionProfile.Curve.Evaluate(t);
                    next.style.opacity = t;
                    if (transitionType == ScreenTransitionType.DirectionalSlide) next.style.translate = new Translate(Mathf.Lerp(24f, 0f, t), 0f);
                    if (t >= 1f) { _switching = false; SetTransitionInputLocked(false); transition?.Pause(); VisualEventBus.Raise(VisualEventType.TransitionCompleted); }
                }).Every(16);
            }
            return true;
        }

        private void StartExitTransition(ScreenId previous, ScreenId screen, VisualElement next, ScreenTransitionType type)
        {
            _switching = true;
            SetTransitionInputLocked(true);
            VisualEventBus.Raise(VisualEventType.TransitionStarted);
            var previousView = _views.TryGetValue(previous, out var current) ? current : null;
            var duration = transitionProfile != null ? transitionProfile.DurationFor(type) : transitionSeconds;
            if (type == ScreenTransitionType.Crossfade && previousView != null)
            {
                CommitScreen(previous, screen, next, false, type);
                previousView.style.display = DisplayStyle.Flex;
                previousView.style.opacity = 1f;
                next.style.opacity = 0f;
                var crossfadeStart = Time.realtimeSinceStartup;
                IVisualElementScheduledItem crossfade = null;
                crossfade = next.schedule.Execute(() =>
                {
                    if (next.panel == null) { _switching = false; SetTransitionInputLocked(false); crossfade?.Pause(); return; }
                    var t = duration <= 0f ? 1f : Mathf.Clamp01((Time.realtimeSinceStartup - crossfadeStart) / duration);
                    if (transitionProfile != null && transitionProfile.Curve != null) t = transitionProfile.Curve.Evaluate(t);
                    previousView.style.opacity = 1f - t;
                    next.style.opacity = t;
                    if (t >= 1f)
                    {
                        previousView.style.display = DisplayStyle.None;
                        previousView.style.opacity = 1f;
                        _switching = false; SetTransitionInputLocked(false); crossfade?.Pause();
                        VisualEventBus.Raise(VisualEventType.TransitionCompleted);
                    }
                }).Every(16);
                return;
            }
            var half = Mathf.Max(0.04f, duration * 0.5f);
            var start = Time.realtimeSinceStartup;
            IVisualElementScheduledItem item = null;
            if (previousView == null)
            {
                _switching = false;
                CommitScreen(previous, screen, next, true, type);
                return;
            }
            item = previousView.schedule.Execute(() =>
            {
                if (previousView.panel == null) { _switching = false; SetTransitionInputLocked(false); item?.Pause(); return; }
                var t = Mathf.Clamp01((Time.realtimeSinceStartup - start) / half);
                var eased = transitionProfile != null && transitionProfile.Curve != null ? transitionProfile.Curve.Evaluate(t) : t;
                previousView.style.opacity = 1f - eased;
                if (type == ScreenTransitionType.DirectionalSlide) previousView.style.translate = new Translate(Mathf.Lerp(0f, -18f, eased), 0f);
                if (t >= 1f)
                {
                    item?.Pause();
                    previousView.style.opacity = 1f;
                    previousView.style.translate = new Translate(0f, 0f);
                    CommitScreen(previous, screen, next, true, type);
                }
            }).Every(16);
        }

        private void StartCurtainTransition(ScreenId previous, ScreenId screen, VisualElement next, ScreenTransitionType type)
        {
            _switching = true;
            SetTransitionInputLocked(true);
            VisualEventBus.Raise(VisualEventType.TransitionStarted);
            _transitionCurtain.style.display = DisplayStyle.Flex;
            var duration = transitionProfile != null ? transitionProfile.DurationFor(type) : transitionSeconds * 2f;
            var half = Mathf.Max(0.06f, duration * 0.5f);
            var start = Time.realtimeSinceStartup;
            var committed = false;
            IVisualElementScheduledItem item = null;
            item = _transitionCurtain.schedule.Execute(() =>
            {
                if (_transitionCurtain.panel == null) { _switching = false; SetTransitionInputLocked(false); item?.Pause(); return; }
                var elapsed = Time.realtimeSinceStartup - start;
                if (!committed)
                {
                    var t = Mathf.Clamp01(elapsed / half);
                    _transitionCurtain.style.opacity = transitionProfile != null && transitionProfile.Curve != null ? transitionProfile.Curve.Evaluate(t) : t;
                    if (t >= 1f)
                    {
                        committed = true;
                        CommitScreen(previous, screen, next, false, ScreenTransitionType.None);
                        VisualEventBus.Raise(VisualEventType.TransitionStarted);
                        start = Time.realtimeSinceStartup;
                    }
                }
                else
                {
                    var t = Mathf.Clamp01((Time.realtimeSinceStartup - start) / half);
                    var eased = transitionProfile != null && transitionProfile.Curve != null ? transitionProfile.Curve.Evaluate(t) : t;
                    _transitionCurtain.style.opacity = 1f - eased;
                    if (t >= 1f)
                    {
                        _transitionCurtain.style.display = DisplayStyle.None;
                        _switching = false; SetTransitionInputLocked(false); item?.Pause();
                        VisualEventBus.Raise(VisualEventType.TransitionCompleted);
                    }
                }
            }).Every(16);
        }

        private static ScreenTransitionType DefaultTransition(ScreenId screen)
        {
            if (screen == ScreenId.PrivateRole) return ScreenTransitionType.CardReveal;
            if (screen == ScreenId.Reveal) return ScreenTransitionType.Cinematic;
            if (screen == ScreenId.RoundResults || screen == ScreenId.MatchResults) return ScreenTransitionType.ScoreTransition;
            if (screen == ScreenId.ScenarioIntro) return ScreenTransitionType.Cinematic;
            return ScreenTransitionType.StandardFade;
        }

        private static void AnimateRevealCard(VisualElement screen)
        {
            var card = screen.Q<VisualElement>(className: "tan-role-card");
            if (card == null) return;
            VisualEventBus.Raise(VisualEventType.CardRevealStarted);
            if (UIPresentationSettings.ReducedMotion)
            {
                card.style.opacity = 1f;
                card.style.scale = new StyleScale(new Scale(Vector3.one));
                VisualEventBus.Raise(VisualEventType.CardRevealCompleted);
                return;
            }
            card.style.opacity = 0f;
            card.style.scale = new StyleScale(new Scale(new Vector3(0.94f, 0.94f, 1f)));
            var start = Time.realtimeSinceStartup;
            IVisualElementScheduledItem item = null;
            item = card.schedule.Execute(() =>
            {
                if (card.panel == null) { item?.Pause(); return; }
                var t = Mathf.Clamp01((Time.realtimeSinceStartup - start) / 0.38f);
                card.style.opacity = t;
                card.style.scale = new StyleScale(new Scale(Vector3.Lerp(new Vector3(0.94f, 0.94f, 1f), Vector3.one, t)));
                if (t >= 1f) { item?.Pause(); VisualEventBus.Raise(VisualEventType.CardRevealCompleted); }
            }).Every(16);
        }

        public void SetCinematicInputLocked(bool locked)
        {
            _cinematicInputLocked = locked;
            RefreshInputBlocker();
        }

        private void SetTransitionInputLocked(bool locked)
        {
            _transitionInputLocked = locked;
            RefreshInputBlocker();
        }

        private void RefreshInputBlocker()
        {
            if (_inputBlocker != null)
                _inputBlocker.style.display = _cinematicInputLocked || _transitionInputLocked ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void BindTimer(float seconds)
        {
            if (_views.TryGetValue(ScreenId.Game, out var game))
            {
                var label = game.Q<Label>("timer");
                if (label != null)
                {
                    var total = Mathf.Max(0, Mathf.CeilToInt(seconds));
                    label.text = $"{total / 60:00}:{total % 60:00}";
                }
            }
        }

        public void SetRevealContinueEnabled(bool enabled)
        {
            if (_views.TryGetValue(ScreenId.Reveal, out var reveal))
            {
                var button = reveal.Q<Button>("go-round-results");
                if (button != null) button.SetEnabled(enabled);
            }
        }

        public void SetGameplayBackEnabled(bool enabled)
        {
            foreach (var id in new[] { ScreenId.PrivateRole, ScreenId.Game, ScreenId.Reveal, ScreenId.RoundResults, ScreenId.MatchResults })
            {
                if (!_views.TryGetValue(id, out var view)) continue;
                var back = view.Q<Button>("screen-back");
                if (back != null) back.SetEnabled(enabled);
            }
        }

        public void ShowOverlay(VisualOverlayId id) => _overlays?.Show(id);
        public void HideOverlay(VisualOverlayId id) => _overlays?.Hide(id);
        public void ShowVoteResolution(string title, string message)
        {
            var view = _overlays?.GetView(VisualOverlayId.VoteResolution);
            if (view == null) return;
            var titleLabel = view.Q<Label>("vote-resolution-title");
            var messageLabel = view.Q<Label>("vote-resolution-message");
            if (titleLabel != null) titleLabel.text = title ?? string.Empty;
            if (messageLabel != null) messageLabel.text = message ?? string.Empty;
            ShowOverlay(VisualOverlayId.VoteResolution);
        }
        public void ShowToast(string message, int holdMilliseconds = 2200)
        {
            if (_toastControllers.TryGetValue(Current, out var controller)) controller.Show(message, holdMilliseconds);
        }
        public event Action<string> VoteSubmitted;
        public event Action PrivateRoleContinueRequested;
        public event Action ScenarioIntroContinueRequested;
        public event Action RoundResultsContinueRequested;
        public event Action<string> ChatMessageSubmitted;
        public event Action CallVoteRequested;
        public event Action<ScreenId, string> RoomRequested;
        public event Action<bool> LobbyReadyChanged;
        public event Action LobbyStartRequested;
        public void BindVoteTargets(IReadOnlyList<VoteTargetViewModel> publicPlayers, string previouslySelectedId = null)
            => _voting?.Bind(publicPlayers, previouslySelectedId);
        public void BindVoteProgress(int submitted, int eligible, bool runoff, bool isOpen = true) => _voting?.BindProgress(submitted, eligible, runoff, isOpen);
        /// <summary>Call only after the core runtime accepts the submitted vote.</summary>
        public void ConfirmVoteLocked()
        {
            HideOverlay(VisualOverlayId.Voting);
            ShowOverlay(VisualOverlayId.VoteLocked);
            HapticsService.Pulse();
            VisualEventBus.Raise(VisualEventType.ButtonPressed);
            VisualEventBus.Raise(VisualEventType.VoteLocked);
        }
        public void BindChatMessages(IReadOnlyList<ChatMessageViewModel> messages) => _chat?.BindMessages(messages);
        public void BindLobby(LobbyScreenViewModel data) => _lobby?.Bind(data);
        public bool BindScreen(ScreenId screen, ScreenViewModel viewModel)
        {
            if (!_views.TryGetValue(screen, out var view)) return false;
            if (screen == ScreenId.Lobby && viewModel is LobbyScreenViewModel lobby) _lobby?.Bind(lobby);
            if (screen == ScreenId.PrivateRole && viewModel is PrivateRoleScreenViewModel privateRole) _privateRole?.Bind(privateRole);
            ScreenDataBinder.Bind(view, viewModel);
            return true;
        }

        private void OnVoteSubmitted(string playerId)
        {
            var hasCoreListener = VoteSubmitted != null;
            VoteSubmitted?.Invoke(playerId);
            if (!hasCoreListener) ConfirmVoteLocked();
        }

        private void OnPrivateRoleContinueRequested()
        {
            if (PrivateRoleContinueRequested != null) PrivateRoleContinueRequested.Invoke();
            else Show(ScreenId.Game, ScreenTransitionType.Crossfade);
        }

        private void OnRoomRequested(string value)
        {
            var hasRoomService = RoomRequested != null;
            RoomRequested?.Invoke(Current, value);
            if (!hasRoomService) Show(ScreenId.Lobby);
        }

        private void OnLobbyReadyChanged(bool ready) => LobbyReadyChanged?.Invoke(ready);
        private void OnLobbyStartRequested()
        {
            if (LobbyStartRequested == null) { Show(ScreenId.ScenarioIntro); return; }
            LobbyStartRequested.Invoke();
        }

        private void BindNavigation(VisualElement view, ScreenId screenId)
        {
            foreach (var button in view.Query<Button>().ToList())
            {
                if (button.name.StartsWith("go-", StringComparison.OrdinalIgnoreCase))
                {
                    button.clicked += () => VisualEventBus.Raise(VisualEventType.ButtonPressed);
                    var target = button.name.Substring(3).Replace('-', ' ');
                    if (string.Equals(target, "chat", StringComparison.OrdinalIgnoreCase)) button.clicked += () => ShowOverlay(VisualOverlayId.Chat);
                    else if (string.Equals(target, "vote", StringComparison.OrdinalIgnoreCase)) button.clicked += () =>
                    {
                        if (CallVoteRequested != null) CallVoteRequested.Invoke();
                        else ShowOverlay(VisualOverlayId.Voting);
                    };
                    else if (string.Equals(target, "settings", StringComparison.OrdinalIgnoreCase)) button.clicked += () => { if (Current == ScreenId.Game) ShowOverlay(VisualOverlayId.Settings); else NavigateByName(target); };
                    else if (button.name == "go-private-role" && screenId == ScreenId.ScenarioIntro) button.clicked += () => ScenarioIntroContinueRequested?.Invoke();
                    else if (button.name == "go-scenario-intro" && screenId == ScreenId.RoundResults) button.clicked += () => RoundResultsContinueRequested?.Invoke();
                    else if (button.name == "go-round-results" && screenId == ScreenId.Reveal) { /* The core phase controls when results may open. */ }
                    else if (string.Equals(target, "round results", StringComparison.OrdinalIgnoreCase)) button.clicked += () => NavigateByName(target);
                    else button.clicked += () => NavigateByName(target);
                }
                else if (button.name == "screen-next") button.clicked += () => NavigateNext(1);
                else if (button.name == "screen-back") button.clicked += GoBack;
                else if (button.name == "show-exit-confirmation") button.clicked += () => ShowOverlay(VisualOverlayId.ExitConfirmation);
            }
        }

        private static void BindAccessibilityControls(VisualElement view, VisualElement documentRoot)
        {
            if (view == null) return;
            UIPresentationSettings.Apply(view);
            foreach (var toggle in view.Query<Toggle>().ToList())
            {
                if (toggle.label.IndexOf("contrast", StringComparison.OrdinalIgnoreCase) >= 0)
                    toggle.SetValueWithoutNotify(UIPresentationSettings.HighContrast);
                else if (toggle.label.IndexOf("motion", StringComparison.OrdinalIgnoreCase) >= 0)
                    toggle.SetValueWithoutNotify(UIPresentationSettings.ReducedMotion);
                else if (toggle.label.IndexOf("text", StringComparison.OrdinalIgnoreCase) >= 0)
                    toggle.SetValueWithoutNotify(UIPresentationSettings.LargeText);
                toggle.RegisterValueChangedCallback(evt =>
                {
                    if (toggle.label.IndexOf("contrast", StringComparison.OrdinalIgnoreCase) >= 0) UIPresentationSettings.HighContrast = evt.newValue;
                    else if (toggle.label.IndexOf("motion", StringComparison.OrdinalIgnoreCase) >= 0) UIPresentationSettings.ReducedMotion = evt.newValue;
                    else if (toggle.label.IndexOf("text", StringComparison.OrdinalIgnoreCase) >= 0) UIPresentationSettings.LargeText = evt.newValue;
                    UIPresentationSettings.Apply(documentRoot);
                    UIPresentationSettings.Apply(view);
                });
            }
            var scale = view.Q<Slider>("ui-scale");
            if (scale != null)
            {
                scale.SetValueWithoutNotify(UIPresentationSettings.TextScale);
                scale.RegisterValueChangedCallback(evt =>
                {
                    UIPresentationSettings.TextScale = evt.newValue;
                    UIPresentationSettings.Apply(documentRoot);
                    UIPresentationSettings.Apply(view);
                });
            }
        }

        private void ApplyResponsiveLayout(VisualElement root, float width)
        {
            var compact = width < 760f;
            root.EnableInClassList("pv2-compact", compact);
            foreach (var view in _views.Values) view.EnableInClassList("pv2-compact", compact);
        }

        private static void AnimateMenuEntry(VisualElement view, ScreenId screen)
        {
            if (screen != ScreenId.MainMenu) return;
            var crest = view.Q<VisualElement>(className: "pv2-brand-crest");
            var title = view.Q<Label>("screen-title");
            if (crest != null) ScheduleFade(crest, 140);
            if (title != null) ScheduleFade(title, 240);
            var buttons = view.Query<Button>().ToList();
            for (var i = 0; i < buttons.Count; i++) ScheduleFade(buttons[i], 360 + i * 55);
        }

        private static void ScheduleFade(VisualElement element, long delayMilliseconds)
        {
            element.style.opacity = 0f;
            element.schedule.Execute(() => UIPresentationMotion.FadeIn(element, 110)).StartingIn(delayMilliseconds);
        }

        private void NavigateByName(string label)
        {
            foreach (var id in _views.Keys)
                if (string.Equals(id.ToString().Replace(" ", ""), label.Replace(" ", ""), StringComparison.OrdinalIgnoreCase)) { Show(id); return; }
        }

        private void NavigateNext(int direction)
        {
            var ids = new List<ScreenId>(_views.Keys);
            var index = ids.IndexOf(Current);
            if (index >= 0 && ids.Count > 0) Show(ids[Mathf.Clamp(index + direction, 0, ids.Count - 1)]);
        }

        private void GoBack()
        {
            if (_switching) return;
            if (_history.Count > 0)
            {
                var previous = _history.Peek();
                _goingBack = true;
                var changed = Show(previous);
                _goingBack = false;
                if (changed) _history.Pop();
            }
            else if (Current != ScreenId.MainMenu && _views.ContainsKey(ScreenId.MainMenu)) Show(ScreenId.MainMenu);
        }
    }
}
