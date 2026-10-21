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
        private string _selectedId;
        private Button _submit;
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
        }

        public VotingOverlayController(VisualElement root)
        {
            _root = root;
            var list = root?.Q<VisualElement>("candidate-list");
            _submit = root?.Q<Button>("submit-vote");
            if (list != null)
                foreach (var candidate in list.Query<Button>().ToList())
                {
                    if (!candidate.name.StartsWith("target-", StringComparison.Ordinal) || !candidate.enabledSelf) continue;
                    var id = candidate.name.Substring("target-".Length);
                    candidate.clicked += () => Select(id, candidate);
                }
            if (_submit != null) _submit.clicked += Submit;
        }

        public void Bind(IReadOnlyList<VoteTargetViewModel> players, string selectedPlayerId = null)
        {
            if (_root == null) return;
            var list = _root.Q<VisualElement>("candidate-list");
            _submit = _root.Q<Button>("submit-vote");
            if (list == null) return;
            list.Clear();
            _selectedId = null;
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
                    candidate.SetEnabled(player.IsEligible && player.IsConnected);
                    var captured = player.PlayerId;
                    candidate.clicked += () => Select(captured, candidate);
                    list.Add(candidate);
                    if (string.Equals(selectedPlayerId, captured, StringComparison.Ordinal)) Select(captured, candidate);
                }
            }
            if (_submit != null)
            {
                _submit.SetEnabled(!string.IsNullOrEmpty(_selectedId));
                _submit.clicked -= Submit;
                _submit.clicked += Submit;
            }
        }

        private void Select(string id, Button button)
        {
            _selectedId = id;
            foreach (var candidate in _root.Q<VisualElement>("candidate-list").Query<Button>().ToList())
                candidate.EnableInClassList("tan-is-selected", candidate == button);
            if (_submit != null) _submit.SetEnabled(true);
            VisualEventBus.Raise(VisualEventType.CardSelected);
            VisualEventBus.Raise(VisualEventType.PlayerSelected);
            VisualEventBus.Raise(VisualEventType.ButtonPressed);
            HapticsService.Pulse();
        }

        private void Submit()
        {
            if (string.IsNullOrEmpty(_selectedId)) return;
            VoteSubmitted?.Invoke(_selectedId);
            VisualEventBus.Raise(VisualEventType.VoteSubmitted);
        }
    }
}
