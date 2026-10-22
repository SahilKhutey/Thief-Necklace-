using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ThiefAndTheNecklace.PB1
{
    public enum RevealStatus { NotStarted, Preparing, Active, RoleRevealed, OutcomeResolved, Complete }
    public enum RevealReason { VoteTargetSelected, ThiefEscaped }
    public enum RevealOutcome { ThiefCaught, WrongPlayerSelected, ThiefEscaped }
    public enum RoleRevealPolicy { ThiefOnly, SelectedAndThief, AllPlayers }
    public enum RevealEventType { RevealStarted, RoleRevealed, ThiefRevealed, NecklaceRevealed, RevealOutcomeResolved, RevealCompleted }

    [Serializable]
    public sealed class RevealConfig
    {
        public RoleRevealPolicy RoleRevealPolicy = RoleRevealPolicy.AllPlayers;
        public bool RevealNecklace = true;
        public bool RevealActualThiefOnWrongVote = true;
        public bool RevealActualThiefOnEscape = true;
        public bool RequirePresentationAck;

        public void Validate()
        {
            if (!Enum.IsDefined(typeof(RoleRevealPolicy), RoleRevealPolicy))
                throw new ArgumentOutOfRangeException(nameof(RoleRevealPolicy));
        }
    }

    public sealed class RoleRevealRecord
    {
        public int PlayerId { get; }
        public RoleType Role { get; }
        public bool IsThief { get; }
        public bool IsPublic { get; }
        internal RoleRevealRecord(int playerId, RoleType role, bool isThief)
        { PlayerId=playerId; Role=role; IsThief=isThief; IsPublic=true; }
    }

    public sealed class NecklaceState
    {
        public int? OwnerPlayerId { get; }
        public bool IsRevealed { get; }
        public bool IsRecovered { get; }
        internal NecklaceState(int? ownerPlayerId, bool revealed, bool recovered = false)
        { OwnerPlayerId=ownerPlayerId; IsRevealed=revealed; IsRecovered=recovered; }
    }

    public sealed class RevealResult
    {
        public string MatchId { get; }
        public string RoundId { get; }
        public RevealReason Reason { get; }
        public int? SelectedTargetId { get; }
        /// <summary>Authoritative truth for trusted gameplay/scoring code; publish only after reveal is active.</summary>
        public int ActualThiefPlayerId { get; }
        public int? PublicThiefPlayerId { get; }
        public RevealOutcome Outcome { get; }
        public IReadOnlyList<RoleRevealRecord> RevealedRoles { get; }
        public NecklaceState Necklace { get; }
        public long RevealStartedAt { get; }
        public long RevealCompletedAt { get; }
        public RevealStatus Status { get; }

        internal RevealResult(string matchId, string roundId, RevealReason reason, int? selectedTargetId,
            int actualThiefPlayerId, int? publicThiefPlayerId, RevealOutcome outcome,
            IEnumerable<RoleRevealRecord> roles, NecklaceState necklace, long startedAt, long completedAt, RevealStatus status)
        {
            MatchId=matchId; RoundId=roundId; Reason=reason; SelectedTargetId=selectedTargetId;
            ActualThiefPlayerId=actualThiefPlayerId; PublicThiefPlayerId=publicThiefPlayerId; Outcome=outcome;
            RevealedRoles=Array.AsReadOnly((roles ?? Enumerable.Empty<RoleRevealRecord>()).ToArray());
            Necklace=necklace; RevealStartedAt=startedAt; RevealCompletedAt=completedAt; Status=status;
        }
    }

    public sealed class RevealSnapshot
    {
        public string MatchId { get; }
        public string RoundId { get; }
        public RevealStatus Status { get; }
        public RevealOutcome Outcome { get; }
        public int? SelectedTargetId { get; }
        public int? ThiefPlayerId { get; }
        public IReadOnlyList<RoleRevealRecord> RevealedRoles { get; }
        public NecklaceState Necklace { get; }
        internal RevealSnapshot(RevealResult result)
        {
            MatchId=result.MatchId; RoundId=result.RoundId; Status=result.Status; Outcome=result.Outcome;
            SelectedTargetId=result.SelectedTargetId; ThiefPlayerId=result.PublicThiefPlayerId;
            RevealedRoles=result.RevealedRoles; Necklace=result.Necklace;
        }
    }

    public sealed class RevealEvent
    {
        public string MatchId { get; }
        public string RoundId { get; }
        public long Sequence { get; }
        public RevealEventType Type { get; }
        public int? PlayerId { get; }
        public RoleType? Role { get; }
        public RevealOutcome? Outcome { get; }
        internal RevealEvent(string matchId, string roundId, long sequence, RevealEventType type,
            int? playerId = null, RoleType? role = null, RevealOutcome? outcome = null)
        { MatchId=matchId; RoundId=roundId; Sequence=sequence; Type=type; PlayerId=playerId; Role=role; Outcome=outcome; }
    }

    /// <summary>Resolves and publishes the hidden-role boundary from authoritative roles and a committed GW6 result.</summary>
    public sealed class RevealService
    {
        private readonly RevealConfig config;
        private readonly Func<long> clock;
        private long eventSequence;
        private RevealResult current;
        private bool publishingEvents;
        public event Action<RevealEvent> EventPublished;
        public RevealResult Current => current;

        public RevealService(RevealConfig revealConfig = null, Func<long> timestampProvider = null)
        {
            var source=revealConfig ?? new RevealConfig();
            config=new RevealConfig
            {
                RoleRevealPolicy=source.RoleRevealPolicy,
                RevealNecklace=source.RevealNecklace,
                RevealActualThiefOnWrongVote=source.RevealActualThiefOnWrongVote,
                RevealActualThiefOnEscape=source.RevealActualThiefOnEscape,
                RequirePresentationAck=source.RequirePresentationAck
            };
            config.Validate();
            clock=timestampProvider ?? (() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }

        public RevealResult Start(string matchId, string roundId, RoundPhase phase, VoteResolutionResult resolution,
            IReadOnlyCollection<(int PlayerId, RoleType Role, bool IsThief)> players)
        {
            Prepare(matchId, roundId, phase, resolution, players);
            return Activate();
        }

        public RevealResult Prepare(string matchId, string roundId, RoundPhase phase, VoteResolutionResult resolution,
            IReadOnlyCollection<(int PlayerId, RoleType Role, bool IsThief)> players)
        {
            if (current != null) return current;
            if (phase != RoundPhase.VoteResolution && phase != RoundPhase.Reveal)
                throw new InvalidOperationException("Reveal can be prepared only after vote resolution.");
            if (resolution == null || (resolution.Outcome != ResolutionOutcome.TargetSelected && resolution.Outcome != ResolutionOutcome.ThiefEscaped))
                throw new InvalidOperationException("Reveal requires a committed target selection or Thief escape result.");
            if (string.IsNullOrWhiteSpace(matchId) || string.IsNullOrWhiteSpace(roundId)) throw new ArgumentException("Reveal match and round identities are required.");
            if (players == null || players.Count < 4 || players.Select(player => player.PlayerId).Distinct().Count() != players.Count
                || players.Any(player => player.PlayerId <= 0 || !Enum.IsDefined(typeof(RoleType), player.Role) || player.Role == RoleType.Thief)
                || players.Select(player => player.Role).Distinct().Count() != players.Count)
                throw new InvalidOperationException("Reveal requires the unique active round roster.");

            var thieves=players.Where(player => player.IsThief).ToArray();
            if (thieves.Length != 1)
                throw new InvalidOperationException("Reveal requires exactly one Thief marker and valid court-role assignments.");
            var thief=thieves[0];
            var escaped=resolution.Outcome == ResolutionOutcome.ThiefEscaped;
            var selected=escaped ? (int?)null : resolution.SelectedTargetId;
            if (!escaped && (!selected.HasValue || !players.Any(player => player.PlayerId == selected.Value)))
                throw new InvalidOperationException("A selected-target reveal must identify a player in the active roster.");
            if (escaped && resolution.SelectedTargetId.HasValue)
                throw new InvalidOperationException("An escape reveal cannot contain a selected target.");

            var outcome=escaped ? RevealOutcome.ThiefEscaped
                : selected.Value == thief.PlayerId ? RevealOutcome.ThiefCaught : RevealOutcome.WrongPlayerSelected;
            var showThief=!escaped && outcome == RevealOutcome.ThiefCaught
                || escaped && config.RevealActualThiefOnEscape
                || !escaped && outcome == RevealOutcome.WrongPlayerSelected && config.RevealActualThiefOnWrongVote
                || config.RevealNecklace;
            var visibleIds=new HashSet<int>();
            switch (config.RoleRevealPolicy)
            {
                case RoleRevealPolicy.AllPlayers:
                    foreach (var player in players) visibleIds.Add(player.PlayerId);
                    break;
                case RoleRevealPolicy.SelectedAndThief:
                    if (selected.HasValue) visibleIds.Add(selected.Value);
                    if (showThief) visibleIds.Add(thief.PlayerId);
                    break;
                case RoleRevealPolicy.ThiefOnly:
                    if (showThief) visibleIds.Add(thief.PlayerId);
                    break;
                default: throw new InvalidOperationException("Unsupported role reveal policy.");
            }

            var started=clock();
            var records=players.Where(player => visibleIds.Contains(player.PlayerId)).OrderBy(player => player.PlayerId)
                .Select(player => new RoleRevealRecord(player.PlayerId, player.Role, player.IsThief)).ToArray();
            var publicThief=showThief || config.RoleRevealPolicy == RoleRevealPolicy.AllPlayers
                ? (int?)thief.PlayerId : null;
            var necklace=new NecklaceState(config.RevealNecklace && publicThief.HasValue ? thief.PlayerId : (int?)null, config.RevealNecklace);
            current=new RevealResult(matchId, roundId, escaped ? RevealReason.ThiefEscaped : RevealReason.VoteTargetSelected,
                selected, thief.PlayerId, publicThief, outcome, records, necklace, started, 0, RevealStatus.Preparing);
            return current;
        }

        public RevealResult Activate()
        {
            if (current == null) throw new InvalidOperationException("Reveal has not been prepared.");
            if (current.Status != RevealStatus.Preparing) return current;
            SetStatus(RevealStatus.Active);
            publishingEvents=true;
            try
            {
                Publish(RevealEventType.RevealStarted);
                foreach (var role in current.RevealedRoles)
                {
                    SetStatus(RevealStatus.RoleRevealed);
                    Publish(RevealEventType.RoleRevealed, role.PlayerId, role.Role);
                }
                if (current.PublicThiefPlayerId.HasValue) Publish(RevealEventType.ThiefRevealed, current.PublicThiefPlayerId.Value);
                if (current.Necklace.IsRevealed) Publish(RevealEventType.NecklaceRevealed, current.Necklace.OwnerPlayerId);
                SetStatus(RevealStatus.OutcomeResolved);
                Publish(RevealEventType.RevealOutcomeResolved, current.SelectedTargetId, outcome: current.Outcome);
            }
            finally { publishingEvents=false; }
            if (!config.RequirePresentationAck) Complete();
            return current;
        }

        public RevealResult Complete()
        {
            if (current == null) throw new InvalidOperationException("Reveal has not started.");
            if (current.Status == RevealStatus.Preparing) Activate();
            if (current.Status == RevealStatus.Complete) return current;
            if (publishingEvents || current.Status != RevealStatus.OutcomeResolved) return current;
            current=new RevealResult(current.MatchId, current.RoundId, current.Reason, current.SelectedTargetId,
                current.ActualThiefPlayerId, current.PublicThiefPlayerId, current.Outcome, current.RevealedRoles,
                current.Necklace, current.RevealStartedAt, clock(), RevealStatus.Complete);
            Publish(RevealEventType.RevealCompleted);
            return current;
        }

        public RevealSnapshot CaptureSnapshot()
        {
            if (current == null) throw new InvalidOperationException("Reveal has not started.");
            return new RevealSnapshot(current);
        }

        public void Reset()
        {
            if (current != null && current.Status != RevealStatus.Complete)
                throw new InvalidOperationException("An active reveal must complete before a new round can start.");
            current=null;
            eventSequence=0;
        }

        private void SetStatus(RevealStatus status)
        {
            current=new RevealResult(current.MatchId, current.RoundId, current.Reason, current.SelectedTargetId,
                current.ActualThiefPlayerId, current.PublicThiefPlayerId, current.Outcome, current.RevealedRoles,
                current.Necklace, current.RevealStartedAt, current.RevealCompletedAt, status);
        }

        private void Publish(RevealEventType type, int? playerId = null, RoleType? role = null, RevealOutcome? outcome = null)
        {
            EventPublished?.Invoke(new RevealEvent(current.MatchId, current.RoundId, ++eventSequence, type, playerId, role, outcome));
        }
    }
}
