using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ThiefAndTheNecklace.PB1
{
    public enum DiscussionStatus { NotStarted, Active, Ending, Complete }
    public enum ClaimType { Location, Timeline, Action, Access, Observation, Identity, Object, General }
    public enum ClaimInterpretationStatus { Unresolved, Candidate, ConfirmedStructured }
    public enum DiscussionEventType { DiscussionStarted, MessagePublished, QuestionPublished, ResponsePublished, ClaimPublished, ClaimRetracted, ChallengePublished, ContradictionDetected, EarlyVoteRequested, DiscussionEnded }
    public enum DiscussionContradictionType { Location, Timeline, Access, Action, Object, Identity, Witness, Sequence }
    public enum ContradictionStrength { Weak, Moderate, Strong, Direct }

    [Serializable]
    public sealed class DiscussionMessage
    {
        public string MessageId = string.Empty;
        public int PlayerId;
        public string Text = string.Empty;
        public long Timestamp;
        public long Sequence;
    }

    [Serializable]
    public sealed class PlayerQuestion
    {
        public string QuestionId = string.Empty;
        public int FromPlayerId;
        public int ToPlayerId;
        public string Text = string.Empty;
        public long Timestamp;
        public long Sequence;
    }

    [Serializable]
    public sealed class DiscussionResponse
    {
        public string ResponseId = string.Empty;
        public string QuestionId = string.Empty;
        public int PlayerId;
        public string Text = string.Empty;
        public long Timestamp;
        public long Sequence;
    }

    [Serializable]
    public sealed class ClaimChallenge
    {
        public string ChallengeId = string.Empty;
        public string ClaimId = string.Empty;
        public int PlayerId;
        public string Text = string.Empty;
        public long Timestamp;
        public long Sequence;
    }

    [Serializable]
    public sealed class DiscussionContradiction
    {
        public string ContradictionId = string.Empty;
        public string ClaimId = string.Empty;
        public int PlayerId;
        public string FactId = string.Empty;
        public string EvidenceId = string.Empty;
        public string EvidencePath = string.Empty;
        public DiscussionContradictionType Type;
        public ContradictionStrength Strength;
        public long Timestamp;
        public long Sequence;
    }

    [Serializable]
    public sealed class DiscussionEvent
    {
        public long Sequence;
        public DiscussionEventType Type;
        public int PlayerId;
        public string RelatedObjectId = string.Empty;
        public long Timestamp;
    }

    [Serializable]
    public sealed class PlayerHypothesisState
    {
        public int PlayerId;
        public List<string> CandidatePlayerIds = new List<string>();
        public List<string> SupportedHypotheses = new List<string>();
        public List<string> RejectedHypotheses = new List<string>();
    }

    [Serializable]
    public sealed class DiscussionRateConfig
    {
        public int MaxMessagesPerWindow = 5;
        public int MaxCommandsPerWindow = 12;
        public double WindowSeconds = 1.0;
        public double MinimumMessageIntervalSeconds = 0.25;
        public int MaximumTextLength = 180;
        public int MaximumHistoryItems = 500;
        public float DurationSeconds = 180f;
        public float VotingDurationSeconds = 60f;
        public bool AllowEarlyVote = true;
        public int EarlyVoteConfirmationsRequired = 4;
    }

    [Serializable]
    public sealed class DiscussionState
    {
        public bool IsActive;
        public long StartTimestamp;
        public long EndTimestamp;
        public DiscussionStatus Status = DiscussionStatus.NotStarted;
        public long Sequence;
        public bool IsFrozen;
        public List<DiscussionMessage> Messages = new List<DiscussionMessage>();
        public List<PlayerClaim> Claims = new List<PlayerClaim>();
        public List<PlayerQuestion> Questions = new List<PlayerQuestion>();
        public List<DiscussionResponse> Responses = new List<DiscussionResponse>();
        public List<ClaimChallenge> Challenges = new List<ClaimChallenge>();
        public List<DiscussionContradiction> Contradictions = new List<DiscussionContradiction>();
        public List<DiscussionEvent> Events = new List<DiscussionEvent>();
        public List<PlayerHypothesisState> Hypotheses = new List<PlayerHypothesisState>();
        public List<int> EarlyVoteRequests = new List<int>();
    }

    public sealed class DiscussionSnapshot
    {
        public long Sequence { get; }
        public long StartTime { get; }
        public long EndTime { get; }
        public DiscussionStatus Status { get; }
        public long NextSequence { get; }
        public bool HasMore { get; }
        public IReadOnlyList<DiscussionMessage> Messages { get; }
        public IReadOnlyList<PlayerClaim> Claims { get; }
        public IReadOnlyList<PlayerQuestion> Questions { get; }
        public IReadOnlyList<DiscussionResponse> Responses { get; }
        public IReadOnlyList<ClaimChallenge> Challenges { get; }

        internal DiscussionSnapshot(DiscussionState state, long afterSequence = 0, int pageSize = 100)
        {
            if (afterSequence < 0) throw new ArgumentOutOfRangeException(nameof(afterSequence));
            if (pageSize < 1 || pageSize > 200) throw new ArgumentOutOfRangeException(nameof(pageSize), "Discussion page size must be between 1 and 200.");
            Sequence=state.Sequence; StartTime=state.StartTimestamp; EndTime=state.EndTimestamp; Status=state.Status;
            var page=state.Messages.Select(item => item.Sequence)
                .Concat(state.Claims.Select(item => item.Sequence)).Concat(state.Questions.Select(item => item.Sequence))
                .Concat(state.Responses.Select(item => item.Sequence)).Concat(state.Challenges.Select(item => item.Sequence))
                .Where(sequence => sequence > afterSequence).Distinct().OrderBy(sequence => sequence).Take(pageSize + 1).ToList();
            HasMore=page.Count > pageSize;
            if (HasMore) page.RemoveAt(page.Count - 1);
            NextSequence=page.Count == 0 ? afterSequence : page[page.Count - 1];
            var included=new HashSet<long>(page);
            Messages=ReadOnly(state.Messages.Where(item => included.Contains(item.Sequence)), Copy); Claims=ReadOnly(state.Claims.Where(item => included.Contains(item.Sequence)), Copy);
            Questions=ReadOnly(state.Questions.Where(item => included.Contains(item.Sequence)), Copy); Responses=ReadOnly(state.Responses.Where(item => included.Contains(item.Sequence)), Copy);
            Challenges=ReadOnly(state.Challenges.Where(item => included.Contains(item.Sequence)), Copy);
        }
        private static IReadOnlyList<T> ReadOnly<T>(IEnumerable<T> items, Func<T,T> copy) => new ReadOnlyCollection<T>((items ?? Enumerable.Empty<T>()).Select(copy).ToList());
        private static DiscussionMessage Copy(DiscussionMessage x) => new DiscussionMessage { MessageId=x.MessageId, PlayerId=x.PlayerId, Text=x.Text, Timestamp=x.Timestamp, Sequence=x.Sequence };
        private static PlayerClaim Copy(PlayerClaim x) => new PlayerClaim { ClaimId=x.ClaimId, PlayerId=x.PlayerId, FactId=x.FactId, Statement=x.Statement, GameTime=x.GameTime, Retracted=x.Retracted, Timestamp=x.Timestamp, Sequence=x.Sequence, Type=x.Type, Interpretation=x.Interpretation };
        private static PlayerQuestion Copy(PlayerQuestion x) => new PlayerQuestion { QuestionId=x.QuestionId, FromPlayerId=x.FromPlayerId, ToPlayerId=x.ToPlayerId, Text=x.Text, Timestamp=x.Timestamp, Sequence=x.Sequence };
        private static DiscussionResponse Copy(DiscussionResponse x) => new DiscussionResponse { ResponseId=x.ResponseId, QuestionId=x.QuestionId, PlayerId=x.PlayerId, Text=x.Text, Timestamp=x.Timestamp, Sequence=x.Sequence };
        private static ClaimChallenge Copy(ClaimChallenge x) => new ClaimChallenge { ChallengeId=x.ChallengeId, ClaimId=x.ClaimId, PlayerId=x.PlayerId, Text=x.Text, Timestamp=x.Timestamp, Sequence=x.Sequence };
    }

    /// <summary>Turnless authoritative discussion history. Timestamps and ordering are assigned here, never by clients.</summary>
    public sealed class DiscussionService
    {
        private readonly DiscussionRateConfig config;
        private readonly Func<long> clock;
        private readonly Dictionary<int, Queue<long>> messageTimes = new Dictionary<int, Queue<long>>();
        private readonly Dictionary<int, Queue<long>> commandTimes = new Dictionary<int, Queue<long>>();
        private readonly HashSet<int> connectedAtStart = new HashSet<int>();
        private readonly DiscussionState state = new DiscussionState();
        public float DurationSeconds => config.DurationSeconds;
        public bool AllowEarlyVote => config.AllowEarlyVote;
        public int EarlyVoteConfirmationsRequired => config.EarlyVoteConfirmationsRequired;
        public bool HasEvidenceBackedContradictions => state.Contradictions.Count > 0;
        public event Action<DiscussionEvent> EventPublished;

        public DiscussionService(DiscussionRateConfig rateConfig = null, Func<long> timestampProvider = null)
        {
            config=rateConfig ?? new DiscussionRateConfig();
            if (config.DurationSeconds <= 0 || float.IsNaN(config.DurationSeconds) || float.IsInfinity(config.DurationSeconds)
                || config.VotingDurationSeconds <= 0 || float.IsNaN(config.VotingDurationSeconds) || float.IsInfinity(config.VotingDurationSeconds)
                || config.MaximumHistoryItems < 1 || config.MaximumTextLength < 1 || config.WindowSeconds <= 0 || double.IsNaN(config.WindowSeconds) || double.IsInfinity(config.WindowSeconds)
                || config.MinimumMessageIntervalSeconds < 0 || double.IsNaN(config.MinimumMessageIntervalSeconds) || double.IsInfinity(config.MinimumMessageIntervalSeconds)
                || config.MaxMessagesPerWindow < 1 || config.MaxCommandsPerWindow < 1 || config.EarlyVoteConfirmationsRequired < 0)
                throw new ArgumentOutOfRangeException(nameof(rateConfig), "Discussion limits and duration must be positive.");
            clock=timestampProvider ?? (() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }

        public void Start(IEnumerable<int> connectedPlayers)
        {
            if (state.Status == DiscussionStatus.Active) throw new InvalidOperationException("Discussion is already active.");
            var participants=(connectedPlayers ?? Enumerable.Empty<int>()).Distinct().ToArray();
            if (participants.Length == 0 || participants.Any(id => id <= 0)) throw new ArgumentException("Discussion requires valid match participants.", nameof(connectedPlayers));
            Reset();
            foreach (var id in participants) connectedAtStart.Add(id);
            state.StartTimestamp=clock(); state.EndTimestamp=state.StartTimestamp + (long)(config.DurationSeconds * 1000f);
            state.IsActive=true; state.Status=DiscussionStatus.Active;
            NextSequence(); Publish(DiscussionEventType.DiscussionStarted, 0, string.Empty, state.StartTimestamp);
        }

        public DiscussionMessage SendMessage(int playerId, string text, bool connected, RoundPhase phase)
        {
            EnsureAllowed(playerId, connected, phase, text, true);
            var item=new DiscussionMessage { MessageId=Guid.NewGuid().ToString("N"), PlayerId=playerId, Text=text.Trim(), Timestamp=clock(), Sequence=NextSequence() };
            state.Messages.Add(item); Publish(DiscussionEventType.MessagePublished, playerId, item.MessageId, item.Timestamp); return Copy(item);
        }

        public PlayerQuestion Ask(int fromPlayerId, int toPlayerId, string text, bool connected, bool targetExists, RoundPhase phase)
        {
            if (!targetExists || fromPlayerId == toPlayerId) throw new ArgumentException("Question must target another player in this match.");
            EnsureAllowed(fromPlayerId, connected, phase, text, false);
            var item=new PlayerQuestion { QuestionId=Guid.NewGuid().ToString("N"), FromPlayerId=fromPlayerId, ToPlayerId=toPlayerId, Text=text.Trim(), Timestamp=clock(), Sequence=NextSequence() };
            state.Questions.Add(item); Publish(DiscussionEventType.QuestionPublished, fromPlayerId, item.QuestionId, item.Timestamp); return Copy(item);
        }

        public DiscussionResponse Respond(string questionId, int playerId, string text, bool connected, RoundPhase phase)
        {
            var question=state.Questions.FirstOrDefault(q => q.QuestionId == questionId);
            if (question == null || question.ToPlayerId != playerId) throw new InvalidOperationException("Only the target player may respond to a current-round question.");
            EnsureAllowed(playerId, connected, phase, text, false);
            var item=new DiscussionResponse { ResponseId=Guid.NewGuid().ToString("N"), QuestionId=questionId, PlayerId=playerId, Text=text.Trim(), Timestamp=clock(), Sequence=NextSequence() };
            state.Responses.Add(item); Publish(DiscussionEventType.ResponsePublished, playerId, questionId, item.Timestamp); return Copy(item);
        }

        public PlayerClaim PublishClaim(int playerId, string factId, string statement, float gameTime, ClaimType type, bool connected, RoundPhase phase)
        {
            if (playerId <= 0 || float.IsNaN(gameTime) || float.IsInfinity(gameTime) || gameTime < 0) throw new ArgumentOutOfRangeException(nameof(playerId));
            if (!Enum.IsDefined(typeof(ClaimType), type)) throw new ArgumentOutOfRangeException(nameof(type));
            EnsureAllowed(playerId, connected, phase, statement, false);
            var claim=new PlayerClaim { ClaimId=Guid.NewGuid().ToString("N"), PlayerId=playerId, FactId=factId ?? string.Empty, Statement=statement.Trim(), GameTime=gameTime, Type=type,
                Timestamp=clock(), Sequence=NextSequence(), Interpretation=string.IsNullOrWhiteSpace(factId) ? ClaimInterpretationStatus.Unresolved : ClaimInterpretationStatus.ConfirmedStructured };
            state.Claims.Add(claim); Publish(DiscussionEventType.ClaimPublished, claim.PlayerId, claim.ClaimId, claim.Timestamp); return Copy(claim);
        }

        public void RetractClaim(string claimId)
        {
            var claim=state.Claims.FirstOrDefault(c => c.ClaimId == claimId);
            if (claim != null && !claim.Retracted) { claim.Retracted=true; claim.Timestamp=clock(); claim.Sequence=NextSequence(); Publish(DiscussionEventType.ClaimRetracted, claim.PlayerId, claimId, claim.Timestamp); }
        }

        public ClaimChallenge Challenge(string claimId, int playerId, string text, bool connected, RoundPhase phase)
        {
            if (!state.Claims.Any(c => c.ClaimId == claimId && !c.Retracted)) throw new ArgumentException("Challenge references no active public claim.", nameof(claimId));
            EnsureAllowed(playerId, connected, phase, text, false);
            var item=new ClaimChallenge { ChallengeId=Guid.NewGuid().ToString("N"), ClaimId=claimId, PlayerId=playerId, Text=text.Trim(), Timestamp=clock(), Sequence=NextSequence() };
            state.Challenges.Add(item); Publish(DiscussionEventType.ChallengePublished, playerId, claimId, item.Timestamp); return Copy(item);
        }

        public DiscussionContradiction EvaluateStructuredClaim(PlayerClaim claim, ScenarioTruthModel truth, IEnumerable<string> knownEvidenceIds)
        {
            if (claim == null || string.IsNullOrWhiteSpace(claim.FactId) || truth == null) return null;
            var fact=(truth.Facts ?? new List<FactDefinition>()).FirstOrDefault(f => f != null && f.FactId == claim.FactId);
            if (fact == null || fact.Truth != EvidenceTruth.False) return null;
            var known=new HashSet<string>(knownEvidenceIds ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
            var evidence=(truth.Evidence ?? new List<EvidenceDefinition>()).Where(e => e != null && known.Contains(e.EvidenceId) && ((e.ContradictsFactIds ?? new List<string>()).Contains(fact.FactId) || (e.SupportsFactIds ?? new List<string>()).Contains(fact.FactId)))
                .OrderBy(e => e.EvidenceId, StringComparer.Ordinal).FirstOrDefault();
            if (evidence == null) return null;
            var evidencePath=string.IsNullOrWhiteSpace(evidence.CausalPathId) ? evidence.SourceId : evidence.CausalPathId;
            if (string.IsNullOrWhiteSpace(evidencePath)) return null;
            var prior=state.Contradictions.FirstOrDefault(item => item.ClaimId == claim.ClaimId && item.EvidenceId == evidence.EvidenceId);
            if (prior != null) return null;
            var result=new DiscussionContradiction { ContradictionId=Guid.NewGuid().ToString("N"), ClaimId=claim.ClaimId, PlayerId=claim.PlayerId, FactId=fact.FactId, EvidenceId=evidence.EvidenceId,
                EvidencePath=evidencePath,
                Type=MapType(claim.Type, fact.Predicate), Strength=MapStrength(evidence.Strength),
                Timestamp=clock(), Sequence=NextSequence() };
            state.Contradictions.Add(result); Publish(DiscussionEventType.ContradictionDetected, claim.PlayerId, claim.ClaimId, result.Timestamp); return Copy(result);
        }

        public void UpdateHypothesis(int playerId, IEnumerable<string> candidates, IEnumerable<string> supported, IEnumerable<string> rejected)
        {
            if (!state.IsActive || state.IsFrozen) throw new InvalidOperationException("Hypotheses can only change during active discussion.");
            var candidateIds=(candidates ?? Enumerable.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).ToList();
            var supportedIds=(supported ?? Enumerable.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).ToList();
            var rejectedIds=(rejected ?? Enumerable.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).ToList();
            if (supportedIds.Intersect(rejectedIds, StringComparer.Ordinal).Any()
                || candidateIds.Concat(supportedIds).Concat(rejectedIds).Any(value => value.Length > config.MaximumTextLength)
                || supportedIds.Count > 128 || rejectedIds.Count > 128)
                throw new ArgumentException("Hypothesis sets contain conflicting or oversized entries.");
            var item=state.Hypotheses.FirstOrDefault(value => value.PlayerId == playerId);
            if (item == null) state.Hypotheses.Add(item=new PlayerHypothesisState { PlayerId=playerId });
            item.CandidatePlayerIds=candidateIds; item.SupportedHypotheses=supportedIds; item.RejectedHypotheses=rejectedIds;
        }

        public bool RequestEarlyVote(int playerId, bool connected, RoundPhase phase)
        {
            if (!config.AllowEarlyVote || config.EarlyVoteConfirmationsRequired < 1) throw new InvalidOperationException("Early voting is disabled by discussion rules.");
            EnsureAllowed(playerId, connected, phase, "request", false);
            if (!connectedAtStart.Contains(playerId)) throw new InvalidOperationException("Only a connected match participant may request early voting.");
            if (state.EarlyVoteRequests.Contains(playerId)) return state.EarlyVoteRequests.Count >= config.EarlyVoteConfirmationsRequired;
            state.EarlyVoteRequests.Add(playerId);
            NextSequence();
            Publish(DiscussionEventType.EarlyVoteRequested, playerId, string.Empty, clock());
            return state.EarlyVoteRequests.Count >= config.EarlyVoteConfirmationsRequired;
        }

        public void End()
        {
            if (state.Status != DiscussionStatus.Active) return;
            state.Status=DiscussionStatus.Ending; state.IsActive=false; state.IsFrozen=true; state.EndTimestamp=Math.Min(state.EndTimestamp, clock());
            state.Status=DiscussionStatus.Complete;
            NextSequence();
            Publish(DiscussionEventType.DiscussionEnded, 0, string.Empty, state.EndTimestamp);
        }

        public DiscussionSnapshot CaptureSnapshot(long afterSequence = 0, int pageSize = 100) => new DiscussionSnapshot(state, afterSequence, pageSize);
        public bool IsExpired() => state.Status == DiscussionStatus.Active && clock() >= state.EndTimestamp;

        private void EnsureAllowed(int playerId, bool connected, RoundPhase phase, string text, bool isMessage)
        {
            if (!state.IsActive || state.IsFrozen || phase != RoundPhase.Discussion || clock() >= state.EndTimestamp) throw new InvalidOperationException("Discussion has ended or is not active.");
            if (!connected || !connectedAtStart.Contains(playerId)) throw new InvalidOperationException("Disconnected or non-member players cannot participate.");
            var normalized=(text ?? string.Empty).Trim();
            if (normalized.Length == 0 || normalized.Length > config.MaximumTextLength || normalized.Any(char.IsControl)) throw new ArgumentException("Discussion text is empty, malformed, or exceeds the configured length limit.", nameof(text));
            if (state.Messages.Count + state.Questions.Count + state.Responses.Count + state.Claims.Count + state.Challenges.Count >= config.MaximumHistoryItems) throw new InvalidOperationException("Discussion history has reached its configured capacity.");
            var now=clock(); var times=isMessage ? messageTimes : commandTimes;
            if (!times.TryGetValue(playerId, out var queue)) times[playerId]=queue=new Queue<long>();
            var window=(long)(config.WindowSeconds * 1000.0); while (queue.Count > 0 && now - queue.Peek() >= window) queue.Dequeue();
            var limit=isMessage ? config.MaxMessagesPerWindow : config.MaxCommandsPerWindow;
            if (queue.Count >= limit || (isMessage && queue.Count > 0 && now - queue.Last() < config.MinimumMessageIntervalSeconds * 1000.0)) throw new InvalidOperationException("Discussion action rate limit exceeded.");
            queue.Enqueue(now);
        }

        private long NextSequence() => ++state.Sequence;
        private void Publish(DiscussionEventType type, int playerId, string relatedId, long timestamp)
        {
            var item=new DiscussionEvent { Sequence=state.Sequence, Type=type, PlayerId=playerId, RelatedObjectId=relatedId ?? string.Empty, Timestamp=timestamp };
            state.Events.Add(item);
            if (type != DiscussionEventType.ContradictionDetected)
                EventPublished?.Invoke(new DiscussionEvent { Sequence=item.Sequence, Type=item.Type, PlayerId=item.PlayerId, RelatedObjectId=item.RelatedObjectId, Timestamp=item.Timestamp });
        }
        private void Reset()
        {
            state.IsActive=false; state.StartTimestamp=state.EndTimestamp=state.Sequence=0; state.Status=DiscussionStatus.NotStarted; state.IsFrozen=false;
            state.Messages.Clear(); state.Claims.Clear(); state.Questions.Clear(); state.Responses.Clear(); state.Challenges.Clear(); state.Contradictions.Clear(); state.Events.Clear(); state.Hypotheses.Clear(); state.EarlyVoteRequests.Clear();
            connectedAtStart.Clear(); messageTimes.Clear(); commandTimes.Clear();
        }
        private static DiscussionContradictionType MapType(ClaimType type, string predicate)
        {
            switch (type)
            {
                case ClaimType.Location: return DiscussionContradictionType.Location;
                case ClaimType.Timeline: return DiscussionContradictionType.Timeline;
                case ClaimType.Access: return DiscussionContradictionType.Access;
                case ClaimType.Action: return DiscussionContradictionType.Action;
                case ClaimType.Object: return DiscussionContradictionType.Object;
                case ClaimType.Identity: return DiscussionContradictionType.Identity;
                case ClaimType.Observation: return DiscussionContradictionType.Witness;
            }
            var value=(predicate ?? string.Empty).Trim().ToLowerInvariant().Replace(' ', '_');
            if (value == "location" || value == "at_location" || value == "located_at") return DiscussionContradictionType.Location;
            if (value == "access" || value == "has_access") return DiscussionContradictionType.Access;
            if (value == "object" || value == "holds" || value == "has_object") return DiscussionContradictionType.Object;
            if (value == "action" || value == "entered" || value == "stole" || value == "performed") return DiscussionContradictionType.Action;
            if (value == "identity" || value == "is_person") return DiscussionContradictionType.Identity;
            if (value == "witness" || value == "saw") return DiscussionContradictionType.Witness;
            if (value == "sequence" || value == "before" || value == "after") return DiscussionContradictionType.Sequence;
            return DiscussionContradictionType.Timeline;
        }
        private static ContradictionStrength MapStrength(EvidenceStrength strength)
        {
            if (strength == EvidenceStrength.Conclusive) return ContradictionStrength.Direct;
            if (strength == EvidenceStrength.Strong) return ContradictionStrength.Strong;
            if (strength == EvidenceStrength.Weak) return ContradictionStrength.Weak;
            return ContradictionStrength.Moderate;
        }
        private static DiscussionMessage Copy(DiscussionMessage x) => new DiscussionMessage { MessageId=x.MessageId, PlayerId=x.PlayerId, Text=x.Text, Timestamp=x.Timestamp, Sequence=x.Sequence };
        private static PlayerClaim Copy(PlayerClaim x) => new PlayerClaim { ClaimId=x.ClaimId, PlayerId=x.PlayerId, FactId=x.FactId, Statement=x.Statement, GameTime=x.GameTime, Retracted=x.Retracted, Timestamp=x.Timestamp, Sequence=x.Sequence, Type=x.Type, Interpretation=x.Interpretation };
        private static PlayerQuestion Copy(PlayerQuestion x) => new PlayerQuestion { QuestionId=x.QuestionId, FromPlayerId=x.FromPlayerId, ToPlayerId=x.ToPlayerId, Text=x.Text, Timestamp=x.Timestamp, Sequence=x.Sequence };
        private static DiscussionResponse Copy(DiscussionResponse x) => new DiscussionResponse { ResponseId=x.ResponseId, QuestionId=x.QuestionId, PlayerId=x.PlayerId, Text=x.Text, Timestamp=x.Timestamp, Sequence=x.Sequence };
        private static ClaimChallenge Copy(ClaimChallenge x) => new ClaimChallenge { ChallengeId=x.ChallengeId, ClaimId=x.ClaimId, PlayerId=x.PlayerId, Text=x.Text, Timestamp=x.Timestamp, Sequence=x.Sequence };
        private static DiscussionContradiction Copy(DiscussionContradiction x) => new DiscussionContradiction { ContradictionId=x.ContradictionId, ClaimId=x.ClaimId, PlayerId=x.PlayerId, FactId=x.FactId, EvidenceId=x.EvidenceId, EvidencePath=x.EvidencePath, Type=x.Type, Strength=x.Strength, Timestamp=x.Timestamp, Sequence=x.Sequence };
    }
}
