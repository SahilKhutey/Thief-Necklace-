using System;
using System.Collections.Generic;
using System.Linq;

namespace ThiefAndTheNecklace.PB1
{
    public interface IVotingView
    {
        event Action<int> VoteSelected;
        event Action VoteSubmitted;
        void SetVotingEnabled(bool enabled);
        void SetPlayerSelectable(int playerId, bool selectable);
        void ShowVoteLock();
        void ShowInvalidVote();
    }
}
