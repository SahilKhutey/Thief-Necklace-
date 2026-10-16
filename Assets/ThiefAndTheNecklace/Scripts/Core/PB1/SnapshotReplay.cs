using System;
using System.Collections.Generic;
using System.Linq;

namespace ThiefAndTheNecklace.PB1
{
    [Serializable]
    public sealed class GameSnapshot
    {
        public string MatchId = string.Empty;
        public int Seed;
        public int Round;
        public RoundPhase Phase;
        public string ScenarioId = string.Empty;
        public int VoteRound;
        public string Resolution = string.Empty;
        public bool Completed;
        public List<PlayerScoreSnapshot> Scores = new List<PlayerScoreSnapshot>();
    }

    [Serializable]
    public sealed class PlayerScoreSnapshot
    {
        public int PlayerId;
        public int RoundScore;
        public int MatchScore;
    }

    public sealed class SnapshotService
    {
        public GameSnapshot Capture(GameRuntimeState state)
        {
            if (state == null || state.Match == null || state.Match.CurrentRoundState == null) throw new ArgumentNullException(nameof(state));
            return new GameSnapshot
            {
                MatchId=state.Match.MatchId, Seed=state.Match.Seed, Round=state.Match.CurrentRound,
                Phase=state.Match.CurrentRoundState.Phase, ScenarioId=state.Match.CurrentRoundState.ScenarioId,
                VoteRound=state.Match.CurrentRoundState.VoteRound, Resolution=state.Match.CurrentRoundState.Resolution.ToString(),
                Completed=state.Match.CurrentRoundState.Completed,
                Scores=state.Match.Players.Select(p => new PlayerScoreSnapshot { PlayerId=p.PlayerId, RoundScore=p.RoundScore, MatchScore=p.MatchScore }).ToList()
            };
        }
    }

    [Serializable]
    public sealed class ReplayRecord
    {
        public string MatchId = string.Empty;
        public int Seed;
        public List<ReplayEvent> Events = new List<ReplayEvent>();
        public ReplayRecord DeepCopy() => new ReplayRecord { MatchId=MatchId, Seed=Seed, Events=Events.Select(e => new ReplayEvent { Round=e.Round, EventType=e.EventType, Data=e.Data }).ToList() };
    }

    [Serializable]
    public sealed class ReplayEvent
    {
        public int Round;
        public string EventType = string.Empty;
        public string Data = string.Empty;
    }

    public sealed class ReplayRecorder
    {
        private readonly ReplayRecord record;
        public ReplayRecorder(string matchId, int seed)
        {
            if (string.IsNullOrWhiteSpace(matchId)) throw new ArgumentException("Match ID is required.", nameof(matchId));
            record=new ReplayRecord { MatchId=matchId, Seed=seed };
        }
        public void Record(int round, string eventType, string data)
        {
            if (round < 1) throw new ArgumentOutOfRangeException(nameof(round));
            if (string.IsNullOrWhiteSpace(eventType)) throw new ArgumentException("Replay event type is required.", nameof(eventType));
            record.Events.Add(new ReplayEvent { Round=round, EventType=eventType, Data=data ?? string.Empty });
        }
        public ReplayRecord GetRecord() => record.DeepCopy();
    }

    public sealed class LocalMatchTestDriver
    {
        private readonly MatchController match;
        public LocalMatchTestDriver(MatchController match) { this.match=match ?? throw new ArgumentNullException(nameof(match)); }
        public VoteResolution PlayRound(int[] voteTargets, int[] runoffTargets = null)
        {
            if (voteTargets == null || voteTargets.Length != match.State.Match.Players.Count) throw new ArgumentException("Invalid vote array.", nameof(voteTargets));
            match.BeginDiscussion(); match.BeginVoting();
            for (var i=0; i<voteTargets.Length; i++) if (!match.SubmitVote(match.State.Match.Players[i].PlayerId, voteTargets[i])) throw new InvalidOperationException("Test driver ballot was rejected.");
            var result=match.ResolveVote();
            if (result == VoteResolution.RevoteRequired)
            {
                if (runoffTargets == null || runoffTargets.Length != voteTargets.Length) throw new InvalidOperationException("A tie requires an explicit runoff ballot.");
                for (var i=0; i<runoffTargets.Length; i++) if (!match.SubmitVote(match.State.Match.Players[i].PlayerId, runoffTargets[i])) throw new InvalidOperationException("Test driver runoff ballot was rejected.");
                result=match.ResolveVote();
            }
            match.CompleteReveal(); match.ResolveScoring(); match.FinishRound();
            return result;
        }
    }
}
