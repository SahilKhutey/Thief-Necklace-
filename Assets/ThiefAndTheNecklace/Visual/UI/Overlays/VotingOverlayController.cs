using System;
using System.Collections.Generic;
using System.Linq;
using ThiefAndTheNecklace.Visual;
using ThiefAndTheNecklace.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace ThiefAndTheNecklace.Visual.UI
{
    public sealed class VoteTargetViewModel
    {
        public string PlayerId;
        public string DisplayName;
        public bool IsLocalPlayer;
        public bool IsConnected = true;
        public bool IsEligible = true;
    }

    /// <summary>Builds a public candidate list and prevents the local player appearing as a target.</summary>
    public sealed class VotingOverlayController
    {
        private readonly VisualElement _root;
        private readonly Dictionary<string, bool> _targetAvailability = new Dictionary<string, bool>(StringComparer.Ordinal);
        private string _selectedId;
        private Button _submit;
        private Label _selectionSummary;
        private Label _confirmationText;
        private VisualElement _confirmationPanel;
        private Button _confirmButton;
        private bool _submissionPending;
        private Func<GameplayInputAction, bool> _actionAllowed = action => true;
        public event Action<string> VoteSubmitted;
        public void BindProgress(int submitted, int eligible, bool runoff, bool isOpen = true)
        {
            var progress = _root?.Q<Label>("vote-progress");
            if (progress != null) progress.text = Mathf.Clamp(submitted, 0, eligible) + " / " + eligible + " VOTES RECEIVED";
            var status = _root?.Q<Label>("vote-status");
            if (status != null)
            {
                status.text = !isOpen ? "VOTING CLOSED" : runoff ? "TIE · REVOTE" : "CAST YOUR VOTE";
                status.EnableInClassList("pv4-vote-tie", runoff);
            }
            var list = _root?.Q<VisualElement>("candidate-list");
            if (list != null)
                foreach (var candidate in list.Query<Button>().ToList())
                    if (candidate.name.StartsWith("target-", StringComparison.Ordinal))
                    {
                        var available = _targetAvailability.TryGetValue(candidate.name, out var eligible) && eligible;
                        candidate.SetEnabled(isOpen && !_submissionPending && available && _actionAllowed(GameplayInputAction.SelectPlayer));
                    }
            if (_submit != null) _submit.SetEnabled(isOpen && !_submissionPending && !string.IsNullOrEmpty(_selectedId)
                && _actionAllowed(GameplayInputAction.SubmitVote));
            if (!isOpen)
            {
                if (_confirmationPanel != null) _confirmationPanel.style.display = DisplayStyle.None;
                if (_selectionSummary != null) _selectionSummary.text = "Voting has ended.";
            }
        }

        public VotingOverlayController(VisualElement root)
        {
            _root = root;
            var list = root?.Q<VisualElement>("candidate-list");
            _submit = root?.Q<Button>("submit-vote");
            if (root != null)
            {
                _selectionSummary = new Label("No player selected.") { name = "vote-selection-summary", focusable = true };
                _selectionSummary.AddToClassList("tn-body");
                root.Add(_selectionSummary);
                _confirmationPanel = new VisualElement { name = "vote-confirmation-panel" };
                _confirmationPanel.AddToClassList("tan-panel");
                _confirmationPanel.style.display = DisplayStyle.None;
                _confirmationText = new Label { name = "vote-confirmation-text" };
                _confirmButton = new Button(ConfirmSubmit) { name = "confirm-vote", text = "CONFIRM VOTE" };
                _confirmButton.AddToClassList("tan-button");
                _confirmButton.AddToClassList("tan-button--primary");
                var cancelButton = new Button(CancelConfirmation) { name = "cancel-vote-confirmation", text = "CANCEL" };
                cancelButton.AddToClassList("tan-button");
                cancelButton.AddToClassList("tan-button--ghost");
                _confirmationPanel.Add(_confirmationText);
                _confirmationPanel.Add(_confirmButton);
                _confirmationPanel.Add(cancelButton);
                root.Add(_confirmationPanel);
            }
            if (list != null)
                foreach (var candidate in list.Query<Button>().ToList())
                {
                    if (!candidate.name.StartsWith("target-", StringComparison.Ordinal) || !candidate.enabledSelf) continue;
                    var id = candidate.name.Substring("target-".Length);
                    candidate.clicked += () => Select(id, candidate);
                }
            if (_submit != null) _submit.clicked += Submit;
        }

        public void SetActionGuard(Func<GameplayInputAction, bool> actionAllowed)
        {
            _actionAllowed = actionAllowed ?? (action => true);
            RefreshInteractionState();
        }

        private void RefreshInteractionState(bool votingOpen = true)
        {
            foreach (var pair in _targetAvailability)
            {
                var button = _root?.Q<Button>(pair.Key);
                if (button != null) button.SetEnabled(votingOpen && !_submissionPending && pair.Value && _actionAllowed(GameplayInputAction.SelectPlayer));
            }
            if (_submit != null) _submit.SetEnabled(!_submissionPending && !string.IsNullOrEmpty(_selectedId)
                && _actionAllowed(GameplayInputAction.SubmitVote));
        }

        public void Bind(IReadOnlyList<VoteTargetViewModel> players, string selectedPlayerId = null, bool voteSubmitted = false)
        {
            if (_root == null) return;
            var list = _root.Q<VisualElement>("candidate-list");
            _submit = _root.Q<Button>("submit-vote");
            if (list == null) return;
            list.Clear();
            _targetAvailability.Clear();
            _selectedId = null;
            _submissionPending = voteSubmitted;
            if (_confirmationPanel != null) _confirmationPanel.style.display = DisplayStyle.None;
            if (_selectionSummary != null) _selectionSummary.text = voteSubmitted ? "Vote submitted. Waiting for other players…" : "No player selected.";
            if (players != null)
            {
                foreach (var player in players)
                {
                    if (player == null) continue;
                    if (player.IsLocalPlayer)
                    {
                        var self = new Label("YOU · NOT ELIGIBLE");
                        self.AddToClassList("tan-badge");
                        self.AddToClassList("tan-badge--warning");
                        list.Add(self);
                        continue;
                    }
                    var candidate = new Button { text = player.DisplayName ?? "Player", name = "target-" + player.PlayerId };
                    candidate.AddToClassList("tan-button");
                    candidate.AddToClassList("tan-button--secondary");
                    var eligible = player.IsEligible && player.IsConnected;
                    _targetAvailability[candidate.name] = eligible;
                    candidate.SetEnabled(eligible && !voteSubmitted && _actionAllowed(GameplayInputAction.SelectPlayer));
                    var captured = player.PlayerId;
                    candidate.clicked += () => Select(captured, candidate);
                    list.Add(candidate);
                    if (string.Equals(selectedPlayerId, captured, StringComparison.Ordinal)) RenderSelection(captured, candidate, false);
                }
            }
            if (_submit != null)
            {
                _submit.SetEnabled(!string.IsNullOrEmpty(_selectedId) && !_submissionPending && _actionAllowed(GameplayInputAction.SubmitVote));
                _submit.clicked -= Submit;
                _submit.clicked += Submit;
            }
            if (voteSubmitted && _selectionSummary != null) _selectionSummary.text = "Vote submitted. Waiting for other players…";
        }

        private void Select(string id, Button button)
        {
            if (_submissionPending || !_actionAllowed(GameplayInputAction.SelectPlayer)) return;
            RenderSelection(id, button, true);
        }

        private void RenderSelection(string id, Button button, bool notify)
        {
            _selectedId = id;
            foreach (var candidate in _root.Q<VisualElement>("candidate-list").Query<Button>().ToList())
                candidate.EnableInClassList("tan-is-selected", candidate == button);
            if (_submit != null) _submit.SetEnabled(!_submissionPending && _actionAllowed(GameplayInputAction.SubmitVote));
            if (_selectionSummary != null) _selectionSummary.text = "Selected: " + (button.text ?? "Player");
            if (_confirmationPanel != null) _confirmationPanel.style.display = DisplayStyle.None;
            if (notify)
            {
                VisualEventBus.Raise(VisualEventType.CardSelected);
                VisualEventBus.Raise(VisualEventType.PlayerSelected);
                VisualEventBus.Raise(VisualEventType.ButtonPressed);
                HapticsService.Pulse();
            }
        }

        private void Submit()
        {
            if (string.IsNullOrEmpty(_selectedId) || _submissionPending || !_actionAllowed(GameplayInputAction.SubmitVote)) return;
            var selected = _root?.Q<Button>("target-" + _selectedId);
            if (selected == null || !selected.enabledSelf) return;
            if (_confirmationText != null) _confirmationText.text = "Submit your vote for " + (selected.text ?? "this player") + "? This cannot be changed.";
            if (_confirmationPanel != null) _confirmationPanel.style.display = DisplayStyle.Flex;
            _confirmButton?.Focus();
        }

        private void ConfirmSubmit()
        {
            if (string.IsNullOrEmpty(_selectedId) || _submissionPending || !_actionAllowed(GameplayInputAction.SubmitVote)) return;
            _submissionPending = true;
            if (_submit != null) { _submit.text = "SUBMITTING…"; _submit.SetEnabled(false); }
            if (_confirmButton != null) { _confirmButton.text = "SUBMITTING…"; _confirmButton.SetEnabled(false); }
            if (_confirmationPanel != null) _confirmationPanel.Q<Button>("cancel-vote-confirmation")?.SetEnabled(false);
            if (_selectionSummary != null) _selectionSummary.text = "Submitting vote…";
            VoteSubmitted?.Invoke(_selectedId);
            VisualEventBus.Raise(VisualEventType.VoteSubmitted);
        }

        private void CancelConfirmation()
        {
            if (_submissionPending) return;
            if (_confirmationPanel != null) _confirmationPanel.style.display = DisplayStyle.None;
            _submit?.Focus();
        }

        public void ConfirmSubmissionAccepted()
        {
            _submissionPending = true;
            RefreshInteractionState();
            if (_selectionSummary != null) _selectionSummary.text = "Vote submitted. Waiting for other players…";
            if (_submit != null) { _submit.text = "VOTE SUBMITTED ✓"; _submit.SetEnabled(false); }
            if (_confirmationPanel != null) _confirmationPanel.style.display = DisplayStyle.None;
        }

        public void ConfirmSubmissionRejected(string playerMessage)
        {
            _submissionPending = false;
            RefreshInteractionState();
            if (_selectionSummary != null) _selectionSummary.text = string.IsNullOrWhiteSpace(playerMessage) ? "Your vote was not submitted. Please try again." : playerMessage;
            if (_submit != null) { _submit.text = "SUBMIT VOTE"; _submit.SetEnabled(!string.IsNullOrEmpty(_selectedId) && !_submissionPending && _actionAllowed(GameplayInputAction.SubmitVote)); }
            if (_confirmButton != null) { _confirmButton.text = "CONFIRM VOTE"; _confirmButton.SetEnabled(true); }
            if (_confirmationPanel != null) _confirmationPanel.style.display = DisplayStyle.None;
        }

        public void VotingClosed()
        {
            BindProgress(0, 0, false, false);
            if (_confirmationPanel != null) _confirmationPanel.style.display = DisplayStyle.None;
            if (_selectionSummary != null) _selectionSummary.text = "Voting has ended.";
        }
    }
}
