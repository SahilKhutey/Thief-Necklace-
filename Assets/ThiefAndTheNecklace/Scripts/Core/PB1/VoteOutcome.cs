using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ThiefAndTheNecklace.PB1
{
    public sealed class VoteOutcome
    {
        public VoteResolution Result { get; private set; }
        public int? AccusedPlayerId { get; private set; }
        public IReadOnlyDictionary<int, int> Counts { get; private set; }
        internal VoteOutcome(VoteResolution result, int? accusedPlayerId, IDictionary<int, int> counts)
        { Result = result; AccusedPlayerId = accusedPlayerId; Counts = new ReadOnlyDictionary<int, int>(new Dictionary<int, int>(counts)); }
    }
}
