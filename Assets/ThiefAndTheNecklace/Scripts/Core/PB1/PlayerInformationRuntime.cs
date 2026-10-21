using System;
using System.Collections.Generic;
using System.Linq;

namespace ThiefAndTheNecklace.PB1
{
    public enum KnowledgeSource { PrivateRoleData, PrivateClue, PublicScenarioFact, PublicClaim, Reveal, SystemEvent }
    public enum KnowledgeItemKind { Fact, Evidence, Event, Location, Player, Claim }

    public sealed class KnowledgeAdded
    {
        public int PlayerId { get; private set; }
        public string ItemId { get; private set; }
        public KnowledgeItemKind Kind { get; private set; }
        public KnowledgeSource Source { get; private set; }
        internal KnowledgeAdded(int playerId, string itemId, KnowledgeItemKind kind, KnowledgeSource source)
        { PlayerId = playerId; ItemId = itemId; Kind = kind; Source = source; }
    }

    public sealed class PlayerBeliefView
    {
        public string FactId { get; private set; }
        public string SourceClaimId { get; private set; }
        public float Confidence { get; private set; }
        internal PlayerBeliefView(string factId, string sourceClaimId, float confidence)
        { FactId = factId; SourceClaimId = sourceClaimId; Confidence = confidence; }
    }

    /// <summary>Authority-side knowledge updates. Beliefs are recorded separately from facts.</summary>
    public sealed class PlayerKnowledgeService
    {
        public event Action<KnowledgeAdded> KnowledgeAddedEvent;

        public bool GrantKnownFact(GameRuntimeState state, int playerId, string factId, KnowledgeSource source)
        {
            if (source == KnowledgeSource.PublicClaim) throw new InvalidOperationException("A public claim is a belief source, not proof of an authoritative fact.");
            PlayerState player;
            var fact = Resolve(state, playerId, factId, source, out player);
            if (fact.Truth != EvidenceTruth.True) throw new InvalidOperationException("Only an authoritative true fact can be added to known facts.");
            if (player.Knowledge.FactIds.Contains(factId)) return false;
            player.Knowledge.UnknownFactIds.Remove(factId);
            player.Knowledge.FalseFactIds.Remove(factId);
            player.Knowledge.FactIds.Add(factId);
            Publish(playerId, factId, KnowledgeItemKind.Fact, source);
            return true;
        }

        public bool GrantKnownFalseFact(GameRuntimeState state, int playerId, string factId, KnowledgeSource source)
        {
            if (source == KnowledgeSource.PublicClaim) throw new InvalidOperationException("A public claim is a belief source, not proof of an authoritative fact.");
            PlayerState player;
            var fact = Resolve(state, playerId, factId, source, out player);
            if (fact.Truth != EvidenceTruth.False) throw new InvalidOperationException("Only an authoritative false fact can be added to known-false facts.");
            if (player.Knowledge.FalseFactIds.Contains(factId)) return false;
            player.Knowledge.FactIds.Remove(factId);
            player.Knowledge.UnknownFactIds.Remove(factId);
            player.Knowledge.FalseFactIds.Add(factId);
            Publish(playerId, factId, KnowledgeItemKind.Fact, source);
            return true;
        }

        public bool GrantEvidence(GameRuntimeState state, int playerId, string evidenceId, KnowledgeSource source)
        {
            var player = ResolvePlayer(state, playerId);
            EnsureSource(source);
            var truth = state.Scenario.Truth;
            var evidenceExists = truth.Evidence.Any(item => item != null && item.EvidenceId == evidenceId);
            if (!evidenceExists)
                throw new ArgumentException("Evidence ID is not present in the current scenario.", nameof(evidenceId));
            if (source == KnowledgeSource.PrivateClue && !truth.PlayerInformation.Any(bundle => bundle != null
                && bundle.PlayerId == player.PlayerId.ToString() && (bundle.EvidenceIds ?? new List<string>()).Contains(evidenceId)))
                throw new InvalidOperationException("A private clue can only be granted to its assigned owner.");
            if (player.Knowledge.EvidenceIds.Contains(evidenceId)) return false;
            player.Knowledge.EvidenceIds.Add(evidenceId);
            Publish(playerId, evidenceId, KnowledgeItemKind.Evidence, source);
            return true;
        }

        public bool GrantObservedEvent(GameRuntimeState state, int playerId, string eventId, KnowledgeSource source)
        {
            var player = ResolvePlayer(state, playerId);
            EnsureSource(source);
            if (!state.Scenario.Truth.Timeline.Any(item => item != null && item.EventId == eventId))
                throw new ArgumentException("Event ID is not present in the current scenario.", nameof(eventId));
            if (player.Knowledge.ObservedEventIds.Contains(eventId)) return false;
            player.Knowledge.ObservedEventIds.Add(eventId);
            Publish(playerId, eventId, KnowledgeItemKind.Event, source);
            return true;
        }

        public bool GrantKnownLocation(GameRuntimeState state, int playerId, string locationId, KnowledgeSource source)
        {
            var player = ResolvePlayer(state, playerId);
            EnsureSource(source);
            if (!state.Scenario.Truth.Timeline.Any(item => item != null && item.LocationId == locationId))
                throw new ArgumentException("Location ID is not present in the current scenario.", nameof(locationId));
            if (player.Knowledge.KnownLocationIds.Contains(locationId)) return false;
            player.Knowledge.KnownLocationIds.Add(locationId);
            Publish(playerId, locationId, KnowledgeItemKind.Location, source);
            return true;
        }

        public bool GrantKnownPlayer(GameRuntimeState state, int playerId, string knownPlayerId, KnowledgeSource source)
        {
            var player = ResolvePlayer(state, playerId);
            EnsureSource(source);
            if (!state.Match.Players.Any(item => item.PlayerId.ToString() == knownPlayerId))
                throw new ArgumentException("Player ID is not present in the current match.", nameof(knownPlayerId));
            if (player.Knowledge.KnownPlayerIds.Contains(knownPlayerId)) return false;
            player.Knowledge.KnownPlayerIds.Add(knownPlayerId);
            Publish(playerId, knownPlayerId, KnowledgeItemKind.Player, source);
            return true;
        }

        public bool RecordBelief(GameRuntimeState state, int playerId, string factId, string sourceClaimId, float confidence)
        {
            var player = ResolvePlayer(state, playerId);
            if (!state.Scenario.Truth.Facts.Any(item => item != null && item.FactId == factId))
                throw new ArgumentException("Belief must reference a fact in the current scenario.", nameof(factId));
            if (string.IsNullOrWhiteSpace(sourceClaimId)) throw new ArgumentException("A claim identity is required for a belief.", nameof(sourceClaimId));
            if (float.IsNaN(confidence) || float.IsInfinity(confidence) || confidence < 0f || confidence > 1f)
                throw new ArgumentOutOfRangeException(nameof(confidence));
            if (player.Knowledge.Beliefs.Any(b => b.FactId == factId && b.SourceClaimId == sourceClaimId)) return false;
            player.Knowledge.Beliefs.Add(new PlayerBelief { FactId = factId, SourceClaimId = sourceClaimId, Confidence = confidence });
            Publish(playerId, sourceClaimId, KnowledgeItemKind.Claim, KnowledgeSource.PublicClaim);
            return true;
        }

        private static FactDefinition Resolve(GameRuntimeState state, int playerId, string factId, KnowledgeSource source, out PlayerState player)
        {
            player = ResolvePlayer(state, playerId);
            EnsureSource(source);
            var fact = state.Scenario.Truth.Facts.FirstOrDefault(item => item != null && item.FactId == factId);
            if (fact == null) throw new ArgumentException("Fact ID is not present in the current scenario.", nameof(factId));
            return fact;
        }

        private static PlayerState ResolvePlayer(GameRuntimeState state, int playerId)
        {
            if (state == null || state.Match == null || state.Scenario == null) throw new ArgumentNullException(nameof(state));
            return state.Match.Players.FirstOrDefault(player => player.PlayerId == playerId)
                ?? throw new ArgumentOutOfRangeException(nameof(playerId));
        }

        private static void EnsureSource(KnowledgeSource source)
        { if (!Enum.IsDefined(typeof(KnowledgeSource), source)) throw new ArgumentOutOfRangeException(nameof(source)); }

        private void Publish(int playerId, string itemId, KnowledgeItemKind kind, KnowledgeSource source)
        { KnowledgeAddedEvent?.Invoke(new KnowledgeAdded(playerId, itemId, kind, source)); }
    }

    /// <summary>Creates and commits one player's scenario-specific private package after role assignment.</summary>
    public sealed class PrivateDataService
    {
        public PlayerInformationBundle Generate(PlayerState player, ScenarioTruthModel truth, DifficultyConfig difficulty)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (truth == null) throw new ArgumentNullException(nameof(truth));
            if (difficulty == null) throw new ArgumentNullException(nameof(difficulty));
            var packageRole = player.IsThief ? RoleType.Thief : player.Role;
            var template = (truth.PlayerInformation ?? new List<PlayerInformationBundle>())
                .FirstOrDefault(item => item != null && item.Role == packageRole);
            if (template == null) throw new InvalidOperationException("Scenario is missing player information for " + packageRole + ".");
            if (template.EvidenceIds == null || template.EvidenceIds.Count < difficulty.EvidencePerPlayer)
                throw new InvalidOperationException("Scenario does not provide enough base evidence for " + packageRole + ".");

            var evidenceIds = template.EvidenceIds.Take(difficulty.EvidencePerPlayer).ToList();
            var ambiguous = (truth.Evidence ?? new List<EvidenceDefinition>()).Where(item => item != null && item.IsAmbiguous)
                .OrderBy(item => item.EvidenceId, StringComparer.Ordinal).ToList();
            var falseLeads = (truth.Evidence ?? new List<EvidenceDefinition>()).Where(item => item != null && item.IsFalseLead)
                .OrderBy(item => item.EvidenceId, StringComparer.Ordinal).ToList();
            AddDifficultyEvidence(evidenceIds, ambiguous, difficulty.AmbiguousEvidenceCount, player.PlayerId);
            AddDifficultyEvidence(evidenceIds, falseLeads, difficulty.FalseLeadCount, player.PlayerId);

            var package = new PlayerInformationBundle
            {
                PlayerId = player.PlayerId.ToString(),
                Role = packageRole,
                PrivateSituation = template.PrivateSituation,
                EvidenceIds = evidenceIds,
                KnownFactIds = new List<string>(template.KnownFactIds ?? new List<string>()),
                UnknownFactIds = new List<string>(template.UnknownFactIds ?? new List<string>()),
                FalseFactIds = new List<string>(template.FalseFactIds ?? new List<string>()),
                ObservedEventIds = new List<string>(template.ObservedEventIds ?? new List<string>()),
                KnownLocationIds = new List<string>(template.KnownLocationIds ?? new List<string>()),
                KnownPlayerIds = new List<string>(template.KnownPlayerIds ?? new List<string>()),
                Objective = player.IsThief ? "Avoid detection and survive the vote." : "Determine who stole the Necklace."
            };
            player.ThiefObjective = player.IsThief ? ThiefObjective.MaintainAlibi : (ThiefObjective?)null;
            player.Knowledge = new PrivateKnowledgeState
            {
                Situation = package.PrivateSituation,
                FactIds = new List<string>(package.KnownFactIds),
                UnknownFactIds = new List<string>(package.UnknownFactIds),
                FalseFactIds = new List<string>(package.FalseFactIds),
                EvidenceIds = new List<string>(package.EvidenceIds),
                ObservedEventIds = new List<string>(package.ObservedEventIds),
                KnownLocationIds = new List<string>(package.KnownLocationIds),
                KnownPlayerIds = new List<string>(package.KnownPlayerIds),
                Objective = package.Objective
            };
            return package;
        }

        private static void AddDifficultyEvidence(List<string> destination, List<EvidenceDefinition> pool, int count, int playerId)
        {
            if (count == 0) return;
            if (pool.Count == 0) throw new InvalidOperationException("Scenario lacks evidence required by its difficulty configuration.");
            for (var i = 0; i < count; i++)
            {
                var evidence = pool[(playerId - 1 + i) % pool.Count];
                if (!destination.Contains(evidence.EvidenceId)) destination.Add(evidence.EvidenceId);
            }
        }
    }

    /// <summary>Validates a fully assigned round before any player enters private reveal.</summary>
    public sealed class InformationIntegrityValidator
    {
        public void ValidateRound(IReadOnlyList<PlayerState> players, ScenarioTruthModel truth)
        {
            if (players == null) throw new ArgumentNullException(nameof(players));
            if (truth == null) throw new ArgumentNullException(nameof(truth));
            if (players.Count < 4 || players.Count > 6) throw new InvalidOperationException("Player count must be between 4 and 6.");
            if (players.Count(p => p != null && p.IsThief) != 1) throw new InvalidOperationException("A round must assign exactly one hidden Thief.");
            if (players.Any(p => p == null || p.PlayerId <= 0 || string.IsNullOrWhiteSpace(p.DisplayName) || p.Knowledge == null))
                throw new InvalidOperationException("Every player needs a valid identity and private knowledge state.");
            if (players.Select(p => p.PlayerId).Distinct().Count() != players.Count)
                throw new InvalidOperationException("Player identities must be unique within the match.");
            if (players.Select(p => p.Role).Distinct().Count() != players.Count || players.Any(p => p.Role == RoleType.Thief))
                throw new InvalidOperationException("Each player must retain one unique court role; Thief is a separate hidden objective.");

            var facts = new HashSet<string>((truth.Facts ?? new List<FactDefinition>()).Where(f => f != null).Select(f => f.FactId), StringComparer.Ordinal);
            var evidence = new HashSet<string>((truth.Evidence ?? new List<EvidenceDefinition>()).Where(e => e != null).Select(e => e.EvidenceId), StringComparer.Ordinal);
            var events = new HashSet<string>((truth.Timeline ?? new List<TimelineEvent>()).Where(e => e != null).Select(e => e.EventId), StringComparer.Ordinal);
            var locations = new HashSet<string>((truth.Timeline ?? new List<TimelineEvent>()).Where(e => e != null).Select(e => e.LocationId), StringComparer.Ordinal);
            var playerIds = new HashSet<string>(players.Where(p => p != null).Select(p => p.PlayerId.ToString()), StringComparer.Ordinal);
            var bundles = truth.PlayerInformation ?? new List<PlayerInformationBundle>();
            if (bundles.Count != players.Count || bundles.Any(b => b == null))
                throw new InvalidOperationException("Every active player must have exactly one private information package.");
            if (bundles.Select(b => b.PlayerId).Distinct(StringComparer.Ordinal).Count() != bundles.Count)
                throw new InvalidOperationException("Private information package ownership must be unique.");

            foreach (var player in players)
            {
                var package = bundles.SingleOrDefault(b => string.Equals(b.PlayerId, player.PlayerId.ToString(), StringComparison.Ordinal));
                var expectedPackageRole = player.IsThief ? RoleType.Thief : player.Role;
                if (package == null || package.Role != expectedPackageRole)
                    throw new InvalidOperationException("Private information package does not match its assigned player and role.");
                var knowledge = player.Knowledge;
                if (string.IsNullOrWhiteSpace(knowledge.Situation) || string.IsNullOrWhiteSpace(knowledge.Objective)
                    || knowledge.EvidenceIds == null || knowledge.EvidenceIds.Count == 0)
                    throw new InvalidOperationException("Every player needs a situation, objective, and personal evidence.");
                if (!string.Equals(package.PrivateSituation, knowledge.Situation, StringComparison.Ordinal)
                    || !string.Equals(package.Objective, knowledge.Objective, StringComparison.Ordinal)
                    || !(package.EvidenceIds ?? new List<string>()).SequenceEqual(knowledge.EvidenceIds, StringComparer.Ordinal)
                    || !(package.KnownFactIds ?? new List<string>()).SequenceEqual(knowledge.FactIds ?? new List<string>(), StringComparer.Ordinal)
                    || !(package.UnknownFactIds ?? new List<string>()).SequenceEqual(knowledge.UnknownFactIds ?? new List<string>(), StringComparer.Ordinal)
                    || !(package.FalseFactIds ?? new List<string>()).SequenceEqual(knowledge.FalseFactIds ?? new List<string>(), StringComparer.Ordinal)
                    || !(package.ObservedEventIds ?? new List<string>()).SequenceEqual(knowledge.ObservedEventIds ?? new List<string>(), StringComparer.Ordinal)
                    || !(package.KnownLocationIds ?? new List<string>()).SequenceEqual(knowledge.KnownLocationIds ?? new List<string>(), StringComparer.Ordinal)
                    || !(package.KnownPlayerIds ?? new List<string>()).SequenceEqual(knowledge.KnownPlayerIds ?? new List<string>(), StringComparer.Ordinal))
                    throw new InvalidOperationException("Committed private package content differs from its owner's runtime knowledge.");
                if (knowledge.EvidenceIds.Any(id => string.IsNullOrWhiteSpace(id) || !evidence.Contains(id)))
                    throw new InvalidOperationException("Private information references unknown evidence.");
                var known = knowledge.FactIds ?? new List<string>();
                var unknown = knowledge.UnknownFactIds ?? new List<string>();
                var falseFacts = knowledge.FalseFactIds ?? new List<string>();
                if (known.Concat(unknown).Concat(falseFacts).Any(id => string.IsNullOrWhiteSpace(id) || !facts.Contains(id)))
                    throw new InvalidOperationException("Private knowledge references an unknown world fact.");
                if (known.Intersect(unknown, StringComparer.Ordinal).Any()
                    || known.Intersect(falseFacts, StringComparer.Ordinal).Any()
                    || unknown.Intersect(falseFacts, StringComparer.Ordinal).Any())
                    throw new InvalidOperationException("A fact cannot be simultaneously known true, unknown, or known false by one player.");
                if ((knowledge.ObservedEventIds ?? new List<string>()).Any(id => string.IsNullOrWhiteSpace(id) || !events.Contains(id))
                    || (knowledge.KnownLocationIds ?? new List<string>()).Any(id => string.IsNullOrWhiteSpace(id) || !locations.Contains(id))
                    || (knowledge.KnownPlayerIds ?? new List<string>()).Any(id => string.IsNullOrWhiteSpace(id) || !playerIds.Contains(id)))
                    throw new InvalidOperationException("Private knowledge references an unknown event, location, or player.");
            }
        }
    }

    /// <summary>Builds recipient-scoped projections; public views never contain unrevealed roles.</summary>
    public sealed class InformationProjectionService
    {
        public IReadOnlyList<PlayerViewData> BuildPublicPlayers(MatchState match)
        {
            if (match == null || match.Players == null) throw new ArgumentNullException(nameof(match));
            return Array.AsReadOnly(match.Players.Select(p => new PlayerViewData
            {
                PlayerId = p.PlayerId,
                DisplayName = p.DisplayName,
                MatchScore = p.MatchScore,
                Connected = p.Connected,
                Ready = p.Ready,
                SeatIndex = p.PlayerId - 1
            }).ToArray());
        }

        public PrivatePlayerViewData BuildPrivatePlayer(GameRuntimeState state, int playerId, int authenticatedRequesterId)
        {
            if (state == null || state.Match == null) throw new ArgumentNullException(nameof(state));
            EnsurePrivateAccess(state, playerId, authenticatedRequesterId);
            var player = state.Match.Players.First(p => p.PlayerId == playerId);
            var evidence = new HashSet<string>(player.Knowledge.EvidenceIds ?? new List<string>(), StringComparer.Ordinal);
            var clueText = state.Scenario.Truth.Evidence.Where(item => item != null && evidence.Contains(item.EvidenceId))
                .Select(item => item.Description).Where(description => !string.IsNullOrWhiteSpace(description)).ToArray();
            return new PrivatePlayerViewData
            {
                PlayerId = player.PlayerId,
                OwnerPlayerId = player.PlayerId,
                Public = BuildPublicPlayers(state.Match).First(view => view.PlayerId == playerId),
                Role = player.Role,
                IsThief = player.IsThief,
                Situation = player.Knowledge.Situation,
                Clues = Array.AsReadOnly(clueText),
                EvidenceIds = Array.AsReadOnly((player.Knowledge.EvidenceIds ?? new List<string>()).ToArray()),
                KnownFactIds = Array.AsReadOnly((player.Knowledge.FactIds ?? new List<string>()).ToArray()),
                UnknownFactIds = Array.AsReadOnly((player.Knowledge.UnknownFactIds ?? new List<string>()).ToArray()),
                FalseFactIds = Array.AsReadOnly((player.Knowledge.FalseFactIds ?? new List<string>()).ToArray()),
                ObservedEventIds = Array.AsReadOnly((player.Knowledge.ObservedEventIds ?? new List<string>()).ToArray()),
                KnownLocationIds = Array.AsReadOnly((player.Knowledge.KnownLocationIds ?? new List<string>()).ToArray()),
                KnownPlayerIds = Array.AsReadOnly((player.Knowledge.KnownPlayerIds ?? new List<string>()).ToArray()),
                Beliefs = Array.AsReadOnly((player.Knowledge.Beliefs ?? new List<PlayerBelief>())
                    .Select(belief => new PlayerBeliefView(belief.FactId, belief.SourceClaimId, belief.Confidence)).ToArray()),
                Objective = player.Knowledge.Objective,
                ThiefObjective = player.ThiefObjective,
                RoleScoreValue = RuntimeScoreService.GetRoleValue(player.Role)
            };
        }

        public IReadOnlyList<RevealedPlayerData> BuildRevealedPlayers(GameRuntimeState state, bool revealCommitted)
        {
            if (state == null || state.Match == null) throw new ArgumentNullException(nameof(state));
            var phase = state.Match.CurrentRoundState.Phase;
            if (!revealCommitted || (phase != RoundPhase.Reveal && phase != RoundPhase.Scoring
                && phase != RoundPhase.RoundEnd && phase != RoundPhase.NextRound && phase != RoundPhase.MatchEnd))
                throw new InvalidOperationException("Player roles are public only after the authoritative reveal is committed.");
            return Array.AsReadOnly(state.Match.Players.Select(p => new RevealedPlayerData
            {
                PlayerId = p.PlayerId,
                Role = p.Role,
                IsThief = p.IsThief
            }).ToArray());
        }

        public IReadOnlyList<EvidenceDefinition> BuildPrivateEvidence(GameRuntimeState state, int playerId, int authenticatedRequesterId)
        {
            if (state == null || state.Match == null) throw new ArgumentNullException(nameof(state));
            EnsurePrivateAccess(state, playerId, authenticatedRequesterId);
            var owned = new HashSet<string>(state.Match.Players.First(p => p.PlayerId == playerId).Knowledge.EvidenceIds ?? new List<string>(), StringComparer.Ordinal);
            return Array.AsReadOnly(state.Scenario.Truth.Evidence.Where(e => e != null && owned.Contains(e.EvidenceId))
                .Select(e =>
                {
                    var target=state.Match.Players.FirstOrDefault(player => player.PlayerId.ToString() == e.ClueTargetPlayerId);
                    return new EvidenceDefinition
                    {
                        EvidenceId = e.EvidenceId,
                        Description = target == null ? e.Description : (e.Description ?? string.Empty).Replace("{player}", target.DisplayName),
                        EvidenceType = e.EvidenceType,
                        ClueType = e.ClueType,
                        Strength = e.Strength
                    };
                }).ToArray());
        }

        private static void EnsurePrivateAccess(GameRuntimeState state, int playerId, int authenticatedRequesterId)
        {
            var phase = state.Match.CurrentRoundState.Phase;
            if (phase != RoundPhase.PrivateReveal && phase != RoundPhase.Discussion)
                throw new InvalidOperationException("Private information is unavailable in this phase.");
            if (playerId != authenticatedRequesterId)
                throw new InvalidOperationException("Private information is available only to its owning player.");
            if (!state.Match.Players.Any(p => p.PlayerId == playerId)) throw new ArgumentOutOfRangeException(nameof(playerId));
        }
    }
}
