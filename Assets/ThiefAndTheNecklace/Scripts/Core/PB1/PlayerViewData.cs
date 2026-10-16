using System;
using System.Collections.Generic;
using System.Linq;

namespace ThiefAndTheNecklace.PB1
{
    public sealed class PlayerViewData
    {
        public int PlayerId { get; internal set; }
        public string DisplayName { get; internal set; }
        public int MatchScore { get; internal set; }
        public bool Connected { get; internal set; }
    }
}
