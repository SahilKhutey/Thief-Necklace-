using System;
using System.Collections.Generic;
using System.Linq;
using ThiefAndTheNecklace.Content;
using ThiefAndTheNecklace.UI;
using ThiefAndTheNecklace.Visual;
using UnityEngine;
using UnityEngine.UIElements;

namespace ThiefAndTheNecklace
{
    public sealed class OfflinePrototypeApp : MonoBehaviour
    {
        private const int TotalRounds = 5;
        private const float DiscussionDuration = 180f;
        private const int DefaultPlayerCount = 4;
        private const int DefaultMatchSeed = 10001;
        private static Color Ink = VisualDesignTokens.DeepInk;
        private static Color Panel = VisualDesignTokens.RoyalSurface;
        private static Color Parchment = VisualDesignTokens.Parchment;
        private static Color Gold = VisualDesignTokens.AntiqueGold;
        private static Color Cream = VisualDesignTokens.WarmIvory;
        private static readonly Color HighContrastInk = Color.black;
        private static readonly Color HighContrastCream = Color.white;

        private UIDocument _document;
        private PanelSettings _panelSettings;
        private UIThemeData _themeData;
        private AudioService _audioService;
        private ProfileService _profileService;
        private readonly GameEventBus _eventBus = new GameEventBus();
        private VisualElement _root;
        private VisualElement _content;
        private Label _timerLabel;
        private TextField _chatInput;
        private DropdownField _speakerField;
        private readonly List<PlayerData> _players = new List<PlayerData>();
        private readonly List<DiscussionMessage> _messages = new List<DiscussionMessage>();
        private readonly HashSet<string> _readyForVote = new HashSet<string>();
        private IRoundSource _roundSource;
        private RuntimeScenario _currentScenario;
        private readonly VoteManager _voteManager = new VoteManager();
        private readonly ScoreResolver _scoreResolver = new ScoreResolver();
        private readonly RoundManager _roundManager = new RoundManager();

        private View _view;
        private int _roundNumber;
        private int _seed;
        private int _playerCount = DefaultPlayerCount;
        private int _matchSeed = DefaultMatchSeed;
        private float _discussionRemaining;
        private int _privatePlayerIndex;
        private bool _secretVisible;
        private bool _returnToDiscussionAfterPrivateReview;
        private bool _ballotVisible;
        private bool _isRunoff;
        private int _voterIndex;
        private string _selectedTarget;
        private string _accusedId;
        private List<string> _runoffCandidates;
        private RevealResult _reveal;
        private RoundScoreResult _roundScore;
        private string _matchId;
        private int _matchStartScore;
        private long _xpAtMatchStart;
        private int _localCorrectVotesThisMatch;
        private int _localIncorrectVotesThisMatch;
        private int _localThiefEscapesThisMatch;
        private long _roundXpEarned;

        private enum View { MainMenu, Intro, PrivateReveal, Discussion, Voting, Reveal, Results, MatchResults, Settings, HowToPlay, Profile }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureApplication()
        {
            if (FindObjectOfType<OfflinePrototypeApp>() != null) return;
            var app = new GameObject("ThiefAndTheNecklace_OfflinePrototype");
            DontDestroyOnLoad(app);
            app.AddComponent<OfflinePrototypeApp>();
        }

        private void OnEnable()
        {
            _themeData = Resources.Load<UIThemeData>("UIThemeData");
            if (_themeData != null)
            {
                Ink = _themeData.ink;
                Panel = _themeData.panel;
                Parchment = _themeData.parchment;
                Gold = _themeData.gold;
                Cream = _themeData.cream;
            }
            _document = gameObject.AddComponent<UIDocument>();
            _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _panelSettings.renderMode = PanelRenderMode.ScreenSpaceOverlay;
            _document.panelSettings = _panelSettings;
            _audioService = gameObject.AddComponent<AudioService>();
            _audioService.Initialize(_eventBus);
            _profileService = new ProfileService(new LocalProfileRepository());
            _profileService.Load("local-player");
            _root = _document.rootVisualElement;
            var designTokens = Resources.Load<StyleSheet>("DesignTokens");
            if (designTokens != null) _root.styleSheets.Add(designTokens);
            _root.style.flexGrow = 1;
            _root.style.backgroundColor = Ink;
            _root.style.color = Cream;
            _root.style.paddingLeft = 28;
            _root.style.paddingRight = 28;
            _root.style.paddingTop = 24;
            _root.style.paddingBottom = 24;
            _root.style.flexDirection = FlexDirection.Column;
            _root.RegisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);
            UIPresentationSettings.Apply(_root);
            _view = View.MainMenu;
            Render();
        }

        private void OnDestroy()
        {
            if (_panelSettings != null) Destroy(_panelSettings);
        }

        private void Update()
        {
            if (_roundManager.CurrentPhase != RoundPhase.Discussion) return;
            _discussionRemaining = Mathf.Max(0f, _discussionRemaining - Time.deltaTime);
            if (_timerLabel != null) _timerLabel.text = FormatTime(_discussionRemaining);
            if (_discussionRemaining <= 0f) BeginVoting();
        }

        private void StartMatch()
        {
            var registry = ScenarioAssetLoader.LoadResourcesOrDefault();
            var fallbackId = registry.All.FirstOrDefault(s => s.scenarioId == "ROYAL_NECKLACE_01")?.scenarioId ?? registry.All.First().scenarioId;
            _roundSource = new ProceduralRoundSource(registry, fallbackId);
            _players.Clear();
            for (var i = 1; i <= _playerCount; i++) _players.Add(new PlayerData("P" + i, i == 1 ? _profileService.Current.displayName : "Player " + i));
            _matchId = Guid.NewGuid().ToString("N");
            _matchStartScore = 0;
            _xpAtMatchStart = _profileService.Current.totalXp;
            _localCorrectVotesThisMatch = 0;
            _localIncorrectVotesThisMatch = 0;
            _localThiefEscapesThisMatch = 0;
            _roundNumber = 0;
            StartRound();
        }

        private void StartRound()
        {
            _roundNumber++;
            _seed = unchecked(_matchSeed + _roundNumber * 7919);
            _roundManager.Reset();
            _currentScenario = _roundSource.Create(_players, _seed, ScenarioDifficulty.Normal);
            foreach (var player in _players)
            {
                var secret = _currentScenario.playerSecrets.First(s => s.playerId == player.PlayerId);
                player.PrivateSituation = secret.privateSituation;
                player.PrivateClues.Clear();
                player.PrivateClues.AddRange(_currentScenario.clues.Where(c => c.ownerPlayerId == player.PlayerId).Select(c => c.text));
                player.PrivateClue = string.Join("\n\n", player.PrivateClues);
            }
            _messages.Clear();
            _readyForVote.Clear();
            _voteManager.Clear();
            _runoffCandidates = null;
            _isRunoff = false;
            _accusedId = null;
            _reveal = null;
            _roundScore = null;
            _privatePlayerIndex = 0;
            _secretVisible = false;
            EnterPhase(RoundPhase.Intro);
            _view = View.Intro;
            Render();
        }

        private void BeginPrivateReveal()
        {
            _returnToDiscussionAfterPrivateReview = false;
            EnterPhase(RoundPhase.PrivateReveal);
            _privatePlayerIndex = 0;
            _secretVisible = false;
            _view = View.PrivateReveal;
            Render();
        }

        private void BeginDiscussion()
        {
            EnterPhase(RoundPhase.Discussion);
            _discussionRemaining = DiscussionDuration;
            _readyForVote.Clear();
            _view = View.Discussion;
            Render();
        }

        private void MarkReadyForVote()
        {
            if (_speakerField == null) return;
            var player = _players.FirstOrDefault(p => p.DisplayName == _speakerField.value);
            if (player == null) return;
            _readyForVote.Add(player.PlayerId);
            if (_readyForVote.Count == _players.Count) BeginVoting();
            else Render();
        }

        private void SendChat()
        {
            if (_chatInput == null || _speakerField == null) return;
            var text = (_chatInput.value ?? string.Empty).Trim();
            if (text.Length == 0 || text.Length > 180) return;
            var player = _players.FirstOrDefault(p => p.DisplayName == _speakerField.value);
            if (player == null) return;
            _messages.Add(new DiscussionMessage
            {
                SenderId = player.PlayerId,
                Text = text,
                Sequence = _messages.Count + 1
            });
            Render();
        }

        private void BeginVoting()
        {
            if (_roundManager.CurrentPhase == RoundPhase.Discussion || _roundManager.CurrentPhase == RoundPhase.VoteResolution)
                EnterPhase(RoundPhase.Voting);
            _view = View.Voting;
            _ballotVisible = false;
            _voterIndex = 0;
            _selectedTarget = null;
            _voteManager.Clear();
            Render();
        }

        private void RevealBallot()
        {
            _ballotVisible = true;
            _selectedTarget = null;
            Render();
        }

        private void SelectTarget(string playerId)
        {
            _selectedTarget = playerId;
            _eventBus.Publish(new VoteFeedbackEvent(false));
            Render();
        }

        private void SubmitLocalVote()
        {
            if (string.IsNullOrEmpty(_selectedTarget) || _voterIndex >= _players.Count) return;
            var voter = _players[_voterIndex];
            IReadOnlyCollection<string> validVoters = _players.Select(p => p.PlayerId).ToList();
            IReadOnlyCollection<string> validTargets = _isRunoff ? _runoffCandidates : validVoters;
            if (!_voteManager.SubmitVote(voter.PlayerId, _selectedTarget, validVoters, validTargets)) return;
            _eventBus.Publish(new VoteFeedbackEvent(true));
            HapticsService.Pulse();
            _voterIndex++;
            _ballotVisible = false;
            _selectedTarget = null;
            if (_voterIndex >= _players.Count) ResolveCurrentBallot();
            else Render();
        }

        private void ResolveCurrentBallot()
        {
            EnterPhase(RoundPhase.VoteResolution);
            var candidateSet = _isRunoff ? _runoffCandidates : _players.Select(p => p.PlayerId).ToList();
            var eligibleVoters = _players.Select(p => p.PlayerId).ToList();
            var resolution = _voteManager.Resolve(eligibleVoters, candidateSet);
            if (!resolution.IsResolved && !_isRunoff)
            {
                _runoffCandidates = new List<string>(resolution.TopCandidates);
                _isRunoff = true;
                BeginVoting();
                return;
            }

            _accusedId = resolution.IsResolved ? resolution.WinnerId : null;
            var thief = _players.First(p => p.IsThief);
            _reveal = new RevealResult
            {
                AccusedPlayerId = _accusedId,
                ThiefPlayerId = thief.PlayerId,
                ThiefCaught = _accusedId != null && _accusedId == thief.PlayerId,
                WasRunoff = _isRunoff,
                FinalVote = resolution
            };
            EnterPhase(RoundPhase.Reveal);
            _eventBus.Publish(new RevealFeedbackEvent(_reveal.ThiefCaught));
            HapticsService.Pulse();
            _view = View.Reveal;
            Render();
        }

        private void ApplyScore()
        {
            if (_reveal == null || _roundScore != null) return;
            EnterPhase(RoundPhase.Scoring);
            _roundScore = _scoreResolver.Resolve(_players, _reveal.ThiefPlayerId, _reveal.AccusedPlayerId, _voteManager.Votes);
            foreach (var player in _players)
            {
                var delta = _roundScore.Deltas[player.PlayerId];
                player.MatchScore += delta;
                if (player.PlayerId != _reveal.ThiefPlayerId && delta > 0) player.CorrectDetections++;
            }
            RecordLocalRoundProgress();
            _eventBus.Publish(new ScoreFeedbackEvent(_roundScore.Deltas.Values.Where(v => v > 0).Sum()));
            if (!_reveal.ThiefCaught)
                _players.First(p => p.PlayerId == _reveal.ThiefPlayerId).SuccessfulThiefEscapes++;
            _view = View.Results;
            Render();
        }

        private void ContinueAfterResults()
        {
            EnterPhase(RoundPhase.RoundEnd);
            if (_roundNumber < TotalRounds)
            {
                StartRound();
                return;
            }
            RecordLocalMatchProgress();
            EnterPhase(RoundPhase.MatchEnd);
            _view = View.MatchResults;
            Render();
        }

        private void RecordLocalRoundProgress()
        {
            if (_players.Count == 0 || _profileService?.Current == null) return;
            var local = _players[0];
            var profile = _profileService.Current;
            var rewardEventId = _matchId + ":round:" + _roundNumber;
            if (profile.processedRewardEventIds.Contains(rewardEventId)) return;
            var delta = _roundScore.Deltas[local.PlayerId];
            var correct = !local.IsThief && delta > 0;
            profile.stats.roundsPlayed++;
            var role = profile.roleHistory.FirstOrDefault(r => r.roleId == local.RoleId);
            if (role == null)
            {
                role = new RolePerformance { roleId = local.RoleId };
                profile.roleHistory.Add(role);
            }
            role.roundsPlayed++;
            role.totalMatchPoints += delta;
            if (correct)
            {
                profile.stats.correctVotes++;
                role.correctVotes++;
                _localCorrectVotesThisMatch++;
            }
            else if (!local.IsThief)
            {
                profile.stats.incorrectVotes++;
                _localIncorrectVotesThisMatch++;
            }

            var xp = 10;
            if (local.IsThief)
            {
                profile.stats.thiefRounds++;
                if (_reveal.ThiefCaught) profile.stats.timesCaught++;
                else
                {
                    profile.stats.thiefEscapes++;
                    role.thiefEscapes++;
                    _localThiefEscapesThisMatch++;
                    xp += 30;
                }
                if (_reveal.ThiefCaught) xp += 10;
            }
            else if (correct) xp += 15;

            var previousXp = profile.totalXp;
            _profileService.GrantReward(new RewardTransaction
            {
                transactionId = rewardEventId,
                eventId = rewardEventId,
                xp = xp,
                reason = "Completed round"
            });
            _roundXpEarned = profile.totalXp - previousXp;
        }

        private void RecordLocalMatchProgress()
        {
            if (_players.Count == 0 || _profileService?.Current == null) return;
            var profile = _profileService.Current;
            var rewardEventId = _matchId + ":match-complete";
            if (profile.processedRewardEventIds.Contains(rewardEventId)) return;
            var ranked = _players.OrderByDescending(p => p.MatchScore)
                .ThenByDescending(p => p.CorrectDetections)
                .ThenByDescending(p => p.SuccessfulThiefEscapes).ToList();
            var local = _players[0];
            var top = ranked[0];
            var won = local.MatchScore == top.MatchScore && local.CorrectDetections == top.CorrectDetections &&
                      local.SuccessfulThiefEscapes == top.SuccessfulThiefEscapes;
            profile.stats.gamesPlayed++;
            if (won) profile.stats.matchesWon++;
            if (_localCorrectVotesThisMatch > 0 && _localIncorrectVotesThisMatch == 0) profile.stats.perfectMatches++;
            profile.matchHistory.Insert(0, new MatchHistoryEntry
            {
                matchId = _matchId,
                playedAtUtc = DateTime.UtcNow.ToString("O"),
                rounds = _roundNumber,
                matchPoints = _players[0].MatchScore - _matchStartScore,
                won = won,
                correctVotes = _localCorrectVotesThisMatch,
                thiefEscapes = _localThiefEscapesThisMatch
            });
            if (profile.matchHistory.Count > 30) profile.matchHistory.RemoveRange(30, profile.matchHistory.Count - 30);
            _profileService.GrantReward(new RewardTransaction
            {
                transactionId = rewardEventId,
                eventId = rewardEventId,
                xp = 50 + (won ? 30 : 0),
                reason = "Completed match"
            });
        }

        private void Render()
        {
            if (_root == null) return;
            _timerLabel = null;
            _chatInput = null;
            _speakerField = null;
            _root.Clear();
            _root.style.backgroundColor = UIPresentationSettings.HighContrast ? Color.black : Ink;
            UIPresentationSettings.Apply(_root);

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.alignItems = Align.Center;
            header.style.borderBottomWidth = 2;
            header.style.borderBottomColor = Gold;
            header.style.paddingBottom = 12;
            header.style.marginBottom = 16;
            if (_view == View.MainMenu) header.style.display = DisplayStyle.None;
            _root.Add(header);
            var title = NewLabel("THIEF & THE NECKLACE", 18, Gold, true);
            title.style.unityTextAlign = TextAnchor.MiddleLeft;
            header.Add(title);
            if (_roundNumber > 0)
            {
                var round = NewLabel("ROUND " + _roundNumber + " / " + TotalRounds, 13, Cream, true);
                round.style.unityTextAlign = TextAnchor.MiddleRight;
                header.Add(round);
            }

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            scroll.style.maxWidth = 920;
            scroll.style.width = Length.Percent(100);
            scroll.style.alignSelf = Align.Center;
            _root.Add(scroll);
            _content = scroll;

            switch (_view)
            {
                case View.MainMenu: RenderMainMenu(); break;
                case View.Intro: RenderIntro(); break;
                case View.PrivateReveal: RenderPrivateReveal(); break;
                case View.Discussion: RenderDiscussion(); break;
                case View.Voting: RenderVoting(); break;
                case View.Reveal: RenderReveal(); break;
                case View.Results: RenderResults(); break;
                case View.MatchResults: RenderMatchResults(); break;
                case View.Settings: RenderSettings(); break;
                case View.HowToPlay: RenderHowToPlay(); break;
                case View.Profile: RenderProfile(); break;
            }
        }

        private void OnRootGeometryChanged(GeometryChangedEvent evt)
        {
            if (_root == null) return;
            var compact = evt.newRect.width < 620;
            var inset = compact ? 12f : 28f;
            _root.style.paddingLeft = inset;
            _root.style.paddingRight = inset;
            _root.style.paddingTop = compact ? 12 : 24;
            _root.style.paddingBottom = compact ? 12 : 24;
            var menuOptions = _root.Q<VisualElement>(className: "tn-menu-options");
            if (menuOptions != null)
                menuOptions.style.flexDirection = compact ? FlexDirection.Column : FlexDirection.Row;
            var menuTitle = _root.Q<Label>(className: "tn-menu-title");
            if (menuTitle != null) menuTitle.style.fontSize = compact ? 32 : 40;
            var menuCrest = _root.Q<Label>(className: "tn-menu-crest");
            if (menuCrest != null)
            {
                menuCrest.style.width = compact ? 60 : 72;
                menuCrest.style.height = compact ? 60 : 72;
            }
        }

        private void RenderMainMenu()
        {
            _content.AddToClassList("tn-page");
            var shell = VisualComponents.Panel("tn-menu-shell");
            _content.Add(shell);
            var crest = VisualComponents.Text("♛", "tn-menu-crest");
            shell.Add(crest);
            shell.Add(VisualComponents.Text("A ROYAL MYSTERY", "tn-headline"));
            var title = VisualComponents.Text("THIEF & THE NECKLACE", "tn-display");
            title.AddToClassList("tn-menu-title");
            shell.Add(title);
            var subtitle = VisualComponents.Text("Four to six players. One hidden Thief. Every clue brings the palace closer to the truth.", "tn-body");
            subtitle.AddToClassList("tn-menu-subtitle");
            shell.Add(subtitle);
            shell.Add(new PalaceVectorArtwork());
            var actions = new VisualElement();
            actions.AddToClassList("tn-menu-actions");
            shell.Add(actions);
            var playerOptions = new List<string> { "4 players", "5 players", "6 players" };
            Button startButton = null;
            var playerCountField = new DropdownField("Players", playerOptions, _playerCount - 4);
            playerCountField.style.marginBottom = 10;
            playerCountField.style.color = Cream;
            playerCountField.RegisterValueChangedCallback(evt =>
            {
                _playerCount = Mathf.Clamp(int.Parse(evt.newValue.Substring(0, 1)), 4, 6);
                if (startButton != null) startButton.text = "BEGIN LOCAL MATCH · " + _playerCount + " PLAYERS";
            });
            actions.Add(playerCountField);
            var seedField = new IntegerField("Match seed") { value = _matchSeed };
            seedField.style.marginBottom = 14;
            seedField.style.color = Cream;
            seedField.RegisterValueChangedCallback(evt => _matchSeed = evt.newValue);
            actions.Add(seedField);
            startButton = VisualComponents.Button("BEGIN LOCAL MATCH · " + _playerCount + " PLAYERS", () =>
            {
                _audioService?.PlayCue("ui_confirm");
                StartMatch();
            }, true);
            startButton.AddToClassList("tn-menu-action");
            startButton.style.minHeight = 56;
            startButton.style.fontSize = Mathf.RoundToInt(16 * UIPresentationSettings.TextScale);
            if (UIPresentationSettings.HighContrast) startButton.style.backgroundColor = HighContrastCream;
            actions.Add(startButton);

            var secondary = new VisualElement();
            secondary.AddToClassList("tn-menu-options");
            shell.Add(secondary);
            var profileButton = VisualComponents.Button("PROFILE · LEVEL " + LevelService.GetLevel(_profileService.Current.totalXp), () => { _view = View.Profile; Render(); });
            profileButton.AddToClassList("tn-menu-action");
            secondary.Add(profileButton);
            var howToButton = VisualComponents.Button("HOW TO PLAY", () => { _view = View.HowToPlay; Render(); });
            howToButton.AddToClassList("tn-menu-action");
            secondary.Add(howToButton);
            var settingsButton = VisualComponents.Button("SETTINGS", () => { _view = View.Settings; Render(); });
            settingsButton.AddToClassList("tn-menu-action");
            secondary.Add(settingsButton);

            var rule = new VisualElement();
            rule.AddToClassList("tn-menu-rule");
            shell.Add(rule);
            shell.Add(VisualComponents.Text("PASS-AND-PLAY · FIVE ROUNDS · THREE-MINUTE DISCUSSION", "tn-caption"));
            shell.Add(VisualComponents.Text("Online rooms are not connected in this build.", "tn-caption"));
            shell.Add(VisualComponents.Text("Use the private handoff between secrets so other players cannot see your role or clues.", "tn-caption"));
        }

        private void RenderIntro()
        {
            AddTitle(_content, _currentScenario.title);
            var keyLocation = _currentScenario.events.FirstOrDefault(e => e.critical)?.locationId ?? "hall";
            _content.Add(new PalaceVectorArtwork(keyLocation));
            AddScenarioIllustration(_content);
            AddCard(_content, "THE INCIDENT", _currentScenario.publicDescription);
            AddText(_content, "The Royal Palace\nBanquet Hall · Royal Chamber · Treasury · East Corridor · West Gate · Garden", 17, Parchment, false);
            AddText(_content, "A private role and clue will be handed to each player in turn. Keep them secret.", 15, Cream, false);
            AddButton(_content, "BEGIN PRIVATE ROLE HANDOFF", BeginPrivateReveal, true);
        }

        private void RenderPrivateReveal()
        {
            var player = _players[_privatePlayerIndex];
            AddTitle(_content, "PRIVATE HANDOFF");
            AddCard(_content, _returnToDiscussionAfterPrivateReview ? "PRIVATE REVIEW" : "PASS THE DEVICE", _returnToDiscussionAfterPrivateReview ? "Only " + player.DisplayName + " should read this card." : "Hand the screen to " + player.DisplayName + ". Other players should look away, then reveal their card.");
            if (!_secretVisible)
            {
                AddButton(_content, "REVEAL " + player.DisplayName + "'S CARD", () => { _secretVisible = true; Render(); }, true);
                return;
            }

            var role = RoleCatalog.Find(player.RoleId);
            _content.Add(new CourtRoleVectorPortrait(player.RoleId));
            AddCard(_content, "YOUR ROLE", role == null ? "Unknown" : role.DisplayName + " · " + role.PointValue + " points\n" + role.Description);
            AddCard(_content, player.IsThief ? "YOUR OBJECTIVE · THIEF" : "YOUR OBJECTIVE · INNOCENT", player.IsThief ? "Avoid detection. Blend into the story and survive the vote." : "Compare clues and identify the Thief.");
            AddCard(_content, "YOUR SITUATION", player.PrivateSituation);
            AddCard(_content, "WHAT YOU KNOW", player.PrivateClue);
            if (_returnToDiscussionAfterPrivateReview)
            {
                AddButton(_content, "HIDE & RETURN TO DISCUSSION", () =>
                {
                    _secretVisible = false;
                    _returnToDiscussionAfterPrivateReview = false;
                    _view = View.Discussion;
                    Render();
                }, true);
            }
            else if (_privatePlayerIndex + 1 < _players.Count)
            {
                AddButton(_content, "HIDE & PASS TO " + _players[_privatePlayerIndex + 1].DisplayName.ToUpperInvariant(), () =>
                {
                    _privatePlayerIndex++;
                    _secretVisible = false;
                    Render();
                }, true);
            }
            else AddButton(_content, "HIDE ALL CARDS & BEGIN DISCUSSION", BeginDiscussion, true);
        }

        private void RenderDiscussion()
        {
            AddTitle(_content, _currentScenario.title);
            var timer = NewLabel(FormatTime(_discussionRemaining), 32, Gold, true);
            timer.style.unityTextAlign = TextAnchor.MiddleCenter;
            timer.style.marginBottom = 8;
            _content.Add(timer);
            _timerLabel = timer;
            AddPlayerStrip(_content, false);

            AddSection(_content, "DISCUSSION");
            var transcript = new VisualElement();
            transcript.style.backgroundColor = Panel;
            transcript.style.paddingLeft = 12;
            transcript.style.paddingRight = 12;
            transcript.style.paddingTop = 10;
            transcript.style.paddingBottom = 10;
            transcript.style.marginBottom = 12;
            foreach (var message in _messages)
            {
                var sender = _players.FirstOrDefault(p => p.PlayerId == message.SenderId);
                var label = NewLabel((sender == null ? "PLAYER" : sender.DisplayName.ToUpperInvariant()) + "  ·  " + message.Text, 14, Cream, false);
                label.style.whiteSpace = WhiteSpace.Normal;
                transcript.Add(label);
            }
            if (_messages.Count == 0) transcript.Add(NewLabel("No messages yet. Start with: Where were you?", 14, Parchment, false));
            _content.Add(transcript);

            _speakerField = new DropdownField("Speaking as", _players.Select(p => p.DisplayName).ToList(), 0);
            _speakerField.style.marginBottom = 8;
            _content.Add(_speakerField);
            _chatInput = new TextField("Message");
            _chatInput.multiline = false;
            _chatInput.maxLength = 180;
            _chatInput.style.marginBottom = 8;
            _content.Add(_chatInput);
            AddButton(_content, "SEND MESSAGE", SendChat, false);

            AddCard(_content, "PRIVATE REVIEW", "Your role, situation, and clue remain available to review locally after discussion starts.");
            AddButton(_content, "MY ROLE & CLUE", () => ShowPrivateReminder(), false);
            AddSection(_content, "READY FOR THE VOTE · " + _readyForVote.Count + "/" + _players.Count);
            AddText(_content, "Choose a speaker above, then mark them ready. When everyone agrees, voting begins early; otherwise the timer starts it.", 13, Parchment, false);
            AddButton(_content, "MARK SELECTED PLAYER READY", MarkReadyForVote, false);
        }

        private void ShowPrivateReminder()
        {
            var selected = _speakerField == null ? _players[0] : _players.FirstOrDefault(p => p.DisplayName == _speakerField.value) ?? _players[0];
            _privatePlayerIndex = _players.IndexOf(selected);
            _secretVisible = false;
            _returnToDiscussionAfterPrivateReview = true;
            _view = View.PrivateReveal;
            Render();
        }

        private void RenderVoting()
        {
            AddTitle(_content, _isRunoff ? "FINAL REVOTE" : "WHO IS THE THIEF?");
            if (_isRunoff)
            {
                var names = _runoffCandidates.Select(id => _players.First(p => p.PlayerId == id).DisplayName);
                AddCard(_content, "TIE", "Revote among tied leaders: " + string.Join(" · ", names));
            }
            if (_voterIndex >= _players.Count)
            {
                AddCard(_content, "BALLOTS COMPLETE", "Resolving the vote…");
                return;
            }

            var voter = _players[_voterIndex];
            if (!_ballotVisible)
            {
                AddCard(_content, "SECRET BALLOT " + (_voterIndex + 1) + " / " + _players.Count, "Pass the device to " + voter.DisplayName + ". Previous votes are hidden.");
                AddButton(_content, "SHOW " + voter.DisplayName.ToUpperInvariant() + "'S BALLOT", RevealBallot, true);
                return;
            }

            AddCard(_content, "YOUR VOTE, " + voter.DisplayName.ToUpperInvariant(), "Choose one other player. Your choice stays hidden until the outcome.");
            var eligible = _isRunoff ? _runoffCandidates : _players.Select(p => p.PlayerId).ToList();
            foreach (var targetId in eligible)
            {
                var target = _players.First(p => p.PlayerId == targetId);
                if (target.PlayerId == voter.PlayerId)
                {
                    AddDisabledButton(_content, target.DisplayName.ToUpperInvariant() + " · YOU");
                    continue;
                }
                var selected = _selectedTarget == target.PlayerId;
                AddButton(_content, (selected ? "✓  " : "   ") + target.DisplayName.ToUpperInvariant(), () => SelectTarget(target.PlayerId), selected);
            }
            AddButton(_content, "LOCK SECRET VOTE", SubmitLocalVote, !string.IsNullOrEmpty(_selectedTarget));
        }

        private void RenderReveal()
        {
            _content.Add(new PalaceVectorArtwork(PalaceVectorArtwork.Motif.Necklace));
            AddSpacer(_content, 18);
            AddCentered(_content, "THE TRUTH", 18, Gold, true);
            var accused = _reveal.AccusedPlayerId == null ? "NO CLEAR ACCUSATION" : _players.First(p => p.PlayerId == _reveal.AccusedPlayerId).DisplayName.ToUpperInvariant();
            AddCentered(_content, accused, 34, Cream, true);
            if (_reveal.AccusedPlayerId == null)
                AddCard(_content, "THE VOTE WAS TIED", "No player was selected. The Thief escaped.");
            else
            {
                var accusedPlayer = _players.First(p => p.PlayerId == _reveal.AccusedPlayerId);
                var role = RoleCatalog.Find(accusedPlayer.RoleId);
                _content.Add(new CourtRoleVectorPortrait(accusedPlayer.RoleId));
                AddCard(_content, "ACCUSED ROLE", (role == null ? "Unknown" : role.DisplayName) + (_reveal.ThiefCaught ? " · THIEF" : " · INNOCENT"));
            }
            var thief = _players.First(p => p.PlayerId == _reveal.ThiefPlayerId);
            _content.Add(new CourtRoleVectorPortrait(thief.RoleId));
            var target = _currentScenario.world.Objects.First(o => o.id == _currentScenario.targetObjectId);
            AddCard(_content, _reveal.ThiefCaught ? "THE " + target.displayName.ToUpperInvariant() + " IS RECOVERED" : "THE THIEF ESCAPED", "The Thief was " + thief.DisplayName + " (" + RoleCatalog.Find(thief.RoleId).DisplayName + "). The stolen object was " + target.displayName + ".");
            AddSection(_content, "WHAT ACTUALLY HAPPENED");
            AddText(_content, "The complete scenario timeline is revealed after the vote. These are the generated events, in order.", 13, Parchment, false);
            foreach (var ev in _currentScenario.events.OrderBy(e => e.sequence))
            {
                var location = _currentScenario.world.Locations.FirstOrDefault(l => l.id == ev.locationId);
                var line = "21:" + ev.minute.ToString("00") + " · " + (location == null ? "Palace" : location.displayName) + "\n" + DescribeTruthEvent(ev);
                AddCard(_content, ev.critical ? "CRITICAL EVENT" : "TIMELINE", line);
            }
            AddSection(_content, "EVERYONE'S ROLE");
            foreach (var player in _players)
            {
                var role = RoleCatalog.Find(player.RoleId);
                AddText(_content, player.DisplayName + "  —  " + role.DisplayName + (player.IsThief ? " · THIEF" : ""), 15, Cream, false);
            }
            AddButton(_content, "SEE ROUND SCORE", ApplyScore, true);
        }

        private void RenderResults()
        {
            AddTitle(_content, "ROUND " + _roundNumber + " RESULT");
            AddCard(_content, "PROGRESSION", "+" + _roundXpEarned + " XP · Level " + LevelService.GetLevel(_profileService.Current.totalXp));
            var thief = _players.First(p => p.IsThief);
            AddCard(_content, _reveal.ThiefCaught ? "THIEF CAUGHT" : "THIEF ESCAPED", thief.DisplayName + " was the Thief.");
            foreach (var player in _players)
            {
                var role = RoleCatalog.Find(player.RoleId);
                var delta = _roundScore.Deltas[player.PlayerId];
                var reason = _roundScore.Reasons.ContainsKey(player.PlayerId) ? " · " + _roundScore.Reasons[player.PlayerId] : "";
                AddText(_content, player.DisplayName + " · " + role.DisplayName + "     " + FormatDelta(delta) + reason + "     Total " + player.MatchScore, 14, Cream, false);
            }
            ShowPendingAchievementCards();
            AddSpacer(_content, 10);
            AddButton(_content, _roundNumber < TotalRounds ? "NEXT ROUND" : "FINAL STANDINGS", ContinueAfterResults, true);
        }

        private void RenderMatchResults()
        {
            AddTitle(_content, "MATCH COMPLETE");
            AddCard(_content, "PROGRESSION", "+" + (_profileService.Current.totalXp - _xpAtMatchStart) + " XP · Level " + LevelService.GetLevel(_profileService.Current.totalXp));
            var ranked = _players.OrderByDescending(p => p.MatchScore)
                .ThenByDescending(p => p.CorrectDetections)
                .ThenByDescending(p => p.SuccessfulThiefEscapes)
                .ToList();
            var place = 0;
            var previousScore = int.MinValue;
            var previousDetections = int.MinValue;
            var previousEscapes = int.MinValue;
            foreach (var player in ranked)
            {
                if (player.MatchScore != previousScore || player.CorrectDetections != previousDetections || player.SuccessfulThiefEscapes != previousEscapes)
                    place++;
                var roleText = "";
                AddCard(_content, "" + place + ". " + player.DisplayName, player.MatchScore + " points · " + player.CorrectDetections + " correct detections · " + player.SuccessfulThiefEscapes + " Thief escapes" + roleText);
                previousScore = player.MatchScore;
                previousDetections = player.CorrectDetections;
                previousEscapes = player.SuccessfulThiefEscapes;
            }
            ShowPendingAchievementCards();
            AddButton(_content, "REMATCH", StartMatch, true);
            AddButton(_content, "PLAYER PROFILE", () => { _view = View.Profile; Render(); }, false);
            AddButton(_content, "MAIN MENU", () => { _players.Clear(); _view = View.MainMenu; Render(); }, false);
        }

        private void RenderProfile()
        {
            var profile = _profileService.Current;
            var level = LevelService.GetLevel(profile.totalXp);
            var levelStart = LevelService.XpAtLevelStart(level);
            var levelTarget = levelStart + LevelService.XpToNextLevel(level);
            AddTitle(_content, "PLAYER PROFILE");
            AddCard(_content, "LEVEL " + level + " · " + CosmeticName(profile.loadout.titleId),
                profile.displayName + "\n" + profile.totalXp + " total XP · " + (profile.totalXp - levelStart) + " / " + (levelTarget - levelStart) + " XP to next level");
            var xpTrack = new VisualElement();
            xpTrack.style.height = 12;
            xpTrack.style.backgroundColor = Panel;
            xpTrack.style.marginBottom = 14;
            var xpFill = new VisualElement();
            xpFill.style.height = Length.Percent(100);
            xpFill.style.width = Length.Percent(Mathf.Clamp01(LevelService.ProgressInLevel(profile.totalXp)) * 100f);
            xpFill.style.backgroundColor = Gold;
            xpTrack.Add(xpFill);
            _content.Add(xpTrack);
            var name = new TextField("Display name") { value = profile.displayName, maxLength = 20 };
            name.RegisterValueChangedCallback(evt =>
            {
                var clean = (evt.newValue ?? string.Empty).Trim();
                if (clean.Length == 0) return;
                profile.displayName = clean;
                if (_players.Count > 0) _players[0].DisplayName = clean;
            });
            _content.Add(name);
            AddButton(_content, "SAVE PROFILE NAME", () => { profile.profileVersion++; _profileService.Save(); }, false);
            var correctTotal = profile.stats.correctVotes + profile.stats.incorrectVotes;
            var accuracy = correctTotal == 0 ? 0f : 100f * profile.stats.correctVotes / correctTotal;
            var winRate = profile.stats.gamesPlayed == 0 ? 0f : 100f * profile.stats.matchesWon / profile.stats.gamesPlayed;
            var escapeRate = profile.stats.thiefRounds == 0 ? 0f : 100f * profile.stats.thiefEscapes / profile.stats.thiefRounds;
            AddCard(_content, "MATCH STATISTICS", "Matches " + profile.stats.gamesPlayed + " · Wins " + profile.stats.matchesWon +
                " · Win rate " + winRate.ToString("0") + "%\nRounds " + profile.stats.roundsPlayed + " · Correct votes " + profile.stats.correctVotes + " · Accuracy " + accuracy.ToString("0") + "%" +
                "\nThief rounds " + profile.stats.thiefRounds + " · Escapes " + profile.stats.thiefEscapes + " · Escape rate " + escapeRate.ToString("0") + "% · Caught " + profile.stats.timesCaught);

            AddSection(_content, "ACHIEVEMENTS");
            foreach (var achievement in AchievementCatalog.All)
            {
                var progress = profile.achievements.FirstOrDefault(a => a.achievementId == achievement.id);
                var value = progress == null ? 0 : Mathf.Min(progress.progress, achievement.target);
                AddCard(_content, (progress != null && progress.unlocked ? "✓ " : "○ ") + achievement.title,
                    achievement.description + "  ·  " + value + " / " + achievement.target + "  ·  Reward " + achievement.xpReward + " XP");
            }

            AddSection(_content, "COSMETIC COLLECTION · NO GAMEPLAY EFFECTS");
            foreach (CosmeticType type in Enum.GetValues(typeof(CosmeticType)))
            {
                AddSection(_content, type.ToString().ToUpperInvariant());
                foreach (var item in CosmeticCatalog.All.Where(c => c.type == type))
                {
                    var owned = _profileService.IsUnlocked(item.id);
                    var equipped = GetEquippedId(profile.loadout, type) == item.id;
                    var state = equipped ? "EQUIPPED" : owned ? "OWNED" : "LOCKED · LEVEL " + item.unlockLevel;
                    AddCard(_content, item.displayName + " · " + state, "Cosmetic identity only. It never changes roles, clues, voting or score.");
                    if (owned && !equipped)
                        AddButton(_content, "EQUIP " + item.displayName.ToUpperInvariant(), () => { _profileService.Equip(type, item.id); Render(); }, false);
                }
            }

            AddSection(_content, "RECENT MATCHES");
            if (profile.matchHistory.Count == 0) AddText(_content, "Complete a match to start your history.", 14, Parchment, false);
            foreach (var match in profile.matchHistory.OrderByDescending(m => m.playedAtUtc).Take(10))
                AddCard(_content, match.won ? "VICTORY" : "MATCH COMPLETE", match.matchPoints + " match points · " + match.rounds + " rounds · " + match.correctVotes + " correct votes · " + match.playedAtUtc);

            AddSection(_content, "ROLE PERFORMANCE");
            foreach (var role in profile.roleHistory.OrderBy(r => r.roleId))
            {
                var roleName = RoleCatalog.Find(role.roleId)?.DisplayName ?? role.roleId;
                var average = role.roundsPlayed == 0 ? 0f : (float)role.totalMatchPoints / role.roundsPlayed;
                AddText(_content, roleName + " · " + role.roundsPlayed + " rounds · average " + average.ToString("0.0") + " points · " + role.correctVotes + " correct votes · " + role.thiefEscapes + " Thief escapes", 14, Cream, false);
            }
            AddButton(_content, "BACK TO TITLE", () => { _view = View.MainMenu; Render(); }, true);
        }

        private void ShowPendingAchievementCards()
        {
            var pending = _profileService.Current.pendingAchievementIds.ToArray();
            if (pending.Length == 0) return;
            AddSection(_content, "ACHIEVEMENT UNLOCKED");
            foreach (var id in pending)
            {
                var definition = AchievementCatalog.Find(id);
                if (definition != null) AddCard(_content, definition.title, definition.description + " · +" + definition.xpReward + " XP");
            }
            _profileService.ClearAchievementNotifications();
        }

        private static string GetEquippedId(PlayerLoadout loadout, CosmeticType type)
        {
            switch (type)
            {
                case CosmeticType.Avatar: return loadout.avatarId;
                case CosmeticType.CardBack: return loadout.cardBackId;
                case CosmeticType.Frame: return loadout.frameId;
                case CosmeticType.Title: return loadout.titleId;
                case CosmeticType.TableTheme: return loadout.tableThemeId;
                case CosmeticType.Emote: return loadout.emoteSetId;
                default: return string.Empty;
            }
        }

        private static string CosmeticName(string id) => CosmeticCatalog.Find(id)?.displayName ?? id;

        private void RenderSettings()
        {
            AddTitle(_content, "PRESENTATION SETTINGS");
            AddText(_content, "These options change presentation only. They never alter clues, voting, or scoring.", 15, Parchment, false);
            AddButton(_content, "REDUCED MOTION · " + (UIPresentationSettings.ReducedMotion ? "ON" : "OFF"), () =>
            {
                UIPresentationSettings.ReducedMotion = !UIPresentationSettings.ReducedMotion;
                Render();
            }, UIPresentationSettings.ReducedMotion);
            AddButton(_content, "HIGH CONTRAST · " + (UIPresentationSettings.HighContrast ? "ON" : "OFF"), () =>
            {
                UIPresentationSettings.HighContrast = !UIPresentationSettings.HighContrast;
                Render();
            }, UIPresentationSettings.HighContrast);
            AddButton(_content, "LARGE TEXT · " + (UIPresentationSettings.LargeText ? "ON" : "OFF"), () =>
            {
                UIPresentationSettings.LargeText = !UIPresentationSettings.LargeText;
                Render();
            }, UIPresentationSettings.LargeText);
            var scales = new[] { 1f, 1.15f, 1.3f, 1.5f };
            var nextScale = scales.FirstOrDefault(s => s > UIPresentationSettings.TextScale + 0.01f);
            if (nextScale <= 0f) nextScale = scales[0];
            AddButton(_content, "TEXT SIZE · " + Mathf.RoundToInt(UIPresentationSettings.TextScale * 100f) + "% (CHANGE)", () =>
            {
                UIPresentationSettings.TextScale = nextScale;
                Render();
            }, false);
            AddAudioSlider("MASTER VOLUME", _audioService.MasterVolume, value => _audioService.MasterVolume = value);
            AddAudioSlider("MUSIC VOLUME", _audioService.MusicVolume, value => _audioService.MusicVolume = value);
            AddAudioSlider("AMBIENCE VOLUME", _audioService.AmbienceVolume, value => _audioService.AmbienceVolume = value);
            AddAudioSlider("SFX VOLUME", _audioService.SfxVolume, value => _audioService.SfxVolume = value);
            AddAudioSlider("UI VOLUME", _audioService.UiVolume, value => _audioService.UiVolume = value);
            AddButton(_content, "HAPTICS · " + (HapticsService.Enabled ? "ON" : "OFF"), () =>
            {
                HapticsService.Enabled = !HapticsService.Enabled;
                Render();
            }, HapticsService.Enabled);
            AddButton(_content, "BACK TO TITLE", () => { _view = View.MainMenu; Render(); }, false);
        }

        private void AddAudioSlider(string title, float value, Action<float> changed)
        {
            var slider = new Slider(title, 0f, 1f) { value = value };
            slider.style.marginTop = 8;
            slider.style.marginBottom = 8;
            slider.RegisterValueChangedCallback(evt => changed(evt.newValue));
            slider.RegisterCallback<PointerUpEvent>(_ => _audioService.SaveSettings());
            slider.RegisterCallback<FocusOutEvent>(_ => _audioService.SaveSettings());
            _content.Add(slider);
        }

        private string DescribeTruthEvent(ScenarioEvent scenarioEvent)
        {
            var actor = _players.FirstOrDefault(p => p.PlayerId == scenarioEvent.actorPlayerId)?.DisplayName;
            if (string.IsNullOrEmpty(actor)) actor = RoleCatalog.Find(scenarioEvent.actorRoleId)?.DisplayName;
            var location = _currentScenario.world.Locations.FirstOrDefault(l => l.id == scenarioEvent.locationId)?.displayName ?? "the palace";
            var target = _currentScenario.world.Objects.FirstOrDefault(o => o.id == scenarioEvent.targetObjectId)?.displayName;
            switch (scenarioEvent.eventType)
            {
                case EventType.Gathering: return "The gathering began in " + location + ".";
                case EventType.Movement: return (actor ?? "A courtier") + " moved toward " + location + ".";
                case EventType.Access: return string.IsNullOrEmpty(actor) ? location + " was accessible during the distraction." : actor + " was at " + location + ".";
                case EventType.Distraction: return "The distraction drew attention away from " + location + ".";
                case EventType.Theft: return (actor ?? "The Thief") + " took " + (target ?? "the missing object") + " from " + location + ".";
                case EventType.Escape: return (actor ?? "The Thief") + " escaped by " + location + ".";
                case EventType.Discovery: return (target ?? "The missing object") + " was discovered missing.";
                default: return scenarioEvent.description;
            }
        }

        private void RenderHowToPlay()
        {
            AddTitle(_content, "HOW TO PLAY");
            AddCard(_content, "1 · READ YOUR SECRET", "Learn your role, private situation, and clue. Keep them private until the reveal.");
            AddCard(_content, "2 · RECONSTRUCT THE NIGHT", "Share what you know, ask where and when, and compare stories for corroboration or contradictions.");
            AddCard(_content, "3 · VOTE", "Each player secretly votes for another player. A tied ballot goes to a revote among the leaders.");
            AddCard(_content, "4 · REVEAL & SCORE", "Innocents score their role value when their vote identifies the Thief. The Thief scores by escaping.");
            AddButton(_content, "BACK TO TITLE", () => { _view = View.MainMenu; Render(); }, true);
        }

        private void AddPlayerStrip(VisualElement parent, bool revealRoles)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.marginBottom = 12;
            foreach (var player in _players)
            {
                var role = RoleCatalog.Find(player.RoleId);
                var label = new Label(revealRoles ? player.DisplayName + "\n" + role.DisplayName : player.DisplayName);
                label.style.backgroundColor = Panel;
                label.style.color = Cream;
                label.style.paddingLeft = 12;
                label.style.paddingRight = 12;
                label.style.paddingTop = 9;
                label.style.paddingBottom = 9;
                label.style.marginRight = 7;
                label.style.marginBottom = 6;
                label.style.borderBottomWidth = 2;
                label.style.borderBottomColor = Gold;
                row.Add(label);
            }
            parent.Add(row);
        }

        private void AddScenarioIllustration(VisualElement parent)
        {
            var art = Resources.LoadAll<ScenarioArtData>("ScenarioArt")
                .FirstOrDefault(item => item != null && item.scenarioId == _currentScenario.scenarioId && item.keyIllustration != null);
            if (art == null) return;
            var image = new Image { image = art.keyIllustration.texture, scaleMode = ScaleMode.ScaleToFit };
            image.style.width = Length.Percent(100);
            image.style.maxHeight = 280;
            image.style.marginBottom = 14;
            image.style.backgroundColor = Panel;
            image.name = "Scenario illustration: " + _currentScenario.title;
            UIPresentationMotion.FadeIn(image);
            parent.Add(image);
        }

        private void AddTitle(VisualElement parent, string text)
        {
            var title = NewLabel(text, 27, Gold, true);
            title.style.marginTop = 8;
            title.style.marginBottom = 12;
            parent.Add(title);
        }

        private void AddSection(VisualElement parent, string text)
        {
            var label = NewLabel(text, 13, Gold, true);
            label.style.marginTop = 10;
            label.style.marginBottom = 7;
            parent.Add(label);
        }

        private void AddText(VisualElement parent, string text, int size, Color color, bool bold)
        {
            var label = NewLabel(text, size, color, bold);
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.marginBottom = 9;
            if (UIPresentationSettings.LargeText) label.style.fontSize = size + 2;
            parent.Add(label);
        }

        private void AddCentered(VisualElement parent, string text, int size, Color color, bool bold)
        {
            var label = NewLabel(text, size, color, bold);
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.marginBottom = 12;
            parent.Add(label);
        }

        private Label NewLabel(string text, int size, Color color, bool bold)
        {
            var label = new Label(text);
            label.style.fontSize = Mathf.RoundToInt(size * UIPresentationSettings.TextScale);
            label.style.color = color;
            label.style.unityFontStyleAndWeight = bold ? FontStyle.Bold : FontStyle.Normal;
            return label;
        }

        private void AddCard(VisualElement parent, string heading, string body)
        {
            var card = new VisualElement();
            card.style.backgroundColor = UIPresentationSettings.HighContrast ? new Color(0.08f, 0.08f, 0.08f) : Panel;
            card.style.borderLeftWidth = 3;
            card.style.borderLeftColor = Gold;
            card.style.paddingLeft = 16;
            card.style.paddingRight = 16;
            card.style.paddingTop = 13;
            card.style.paddingBottom = 13;
            card.style.marginBottom = 12;
            card.style.borderTopWidth = 1;
            card.style.borderRightWidth = 1;
            card.style.borderBottomWidth = 1;
            card.style.borderTopColor = UIPresentationSettings.HighContrast ? Color.white : new Color(Gold.r, Gold.g, Gold.b, 0.35f);
            card.style.borderRightColor = card.style.borderTopColor.value;
            card.style.borderBottomColor = card.style.borderTopColor.value;
            card.Add(NewLabel(heading, 13, Gold, true));
            var text = NewLabel(body, 17, Cream, false);
            text.style.whiteSpace = WhiteSpace.Normal;
            text.style.marginTop = 7;
            card.Add(text);
            UIPresentationMotion.FadeIn(card);
            parent.Add(card);
        }

        private void AddButton(VisualElement parent, string text, Action action, bool primary)
        {
            var button = new Button(() =>
            {
                _audioService?.PlayCue("ui_confirm");
                action?.Invoke();
            }) { text = text };
            button.style.minHeight = 48;
            button.style.marginTop = 5;
            button.style.marginBottom = 7;
            button.style.paddingLeft = 16;
            button.style.paddingRight = 16;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.fontSize = Mathf.RoundToInt(15 * UIPresentationSettings.TextScale);
            button.style.color = UIPresentationSettings.HighContrast ? (primary ? HighContrastInk : HighContrastCream) : primary ? Ink : Cream;
            button.style.backgroundColor = UIPresentationSettings.HighContrast ? (primary ? HighContrastCream : HighContrastInk) : primary ? Gold : Panel;
            button.style.borderTopWidth = 1;
            button.style.borderRightWidth = 1;
            button.style.borderBottomWidth = 1;
            button.style.borderLeftWidth = 1;
            button.style.borderTopColor = UIPresentationSettings.HighContrast ? Color.white : Gold;
            button.style.borderRightColor = button.style.borderTopColor.value;
            button.style.borderBottomColor = button.style.borderTopColor.value;
            button.style.borderLeftColor = button.style.borderTopColor.value;
            button.style.borderTopLeftRadius = 4;
            button.style.borderTopRightRadius = 4;
            button.style.borderBottomLeftRadius = 4;
            button.style.borderBottomRightRadius = 4;
            button.RegisterCallback<PointerEnterEvent>(_ =>
            {
                if (!UIPresentationSettings.ReducedMotion && button.enabledSelf) button.style.opacity = 0.88f;
            });
            button.RegisterCallback<PointerLeaveEvent>(_ => button.style.opacity = 1f);
            button.RegisterCallback<PointerDownEvent>(_ =>
            {
                if (button.enabledSelf) button.style.opacity = 0.72f;
            });
            button.RegisterCallback<PointerUpEvent>(_ => button.style.opacity = 1f);
            button.RegisterCallback<FocusInEvent>(_ => button.style.borderBottomWidth = 3);
            button.RegisterCallback<FocusOutEvent>(_ => button.style.borderBottomWidth = 1);
            UIPresentationMotion.FadeIn(button);
            parent.Add(button);
        }

        private void AddDisabledButton(VisualElement parent, string text)
        {
            var button = new Button { text = text, focusable = false };
            button.SetEnabled(false);
            button.style.minHeight = 44;
            button.style.marginBottom = 6;
            button.style.color = Parchment;
            button.style.backgroundColor = Panel;
            parent.Add(button);
        }

        private void AddSpacer(VisualElement parent, float height)
        {
            var spacer = new VisualElement();
            spacer.style.height = height;
            parent.Add(spacer);
        }

        private string FormatTime(float seconds)
        {
            var total = Mathf.CeilToInt(seconds);
            return (total / 60).ToString("00") + ":" + (total % 60).ToString("00");
        }

        private string FormatDelta(int delta) => delta > 0 ? "+" + delta : delta.ToString();

        private void EnterPhase(RoundPhase phase)
        {
            if (_roundManager.TryEnterPhase(phase))
                _eventBus.Publish(new RoundPhaseChangedEvent(phase, _roundNumber));
        }
    }
}
