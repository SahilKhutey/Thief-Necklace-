using System;
using System.Collections.Generic;
using System.Linq;

namespace ThiefAndTheNecklace.PB1
{
    [Serializable]
    public sealed class GameState
    {
        internal readonly List<PlayerRuntimeData> Players = new List<PlayerRuntimeData>();
        internal bool RoundScoreCalculated;
        internal bool RoundScoreCommitted;
        internal int VoteRound;
        public int MatchNumber { get; internal set; }
        public int CurrentRound { get; internal set; }
        public int TotalRounds { get; internal set; }
        public RoundPhase Phase { get; internal set; }
        public string ScenarioId { get; internal set; }

        internal void ResetVotes()
        {
            foreach (var player in Players)
            {
                player.HasSubmittedVote = false;
                player.VoteTargetId = null;
            }
        }
    }
}
