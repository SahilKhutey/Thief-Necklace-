using System;
using System.Collections.Generic;
using System.Linq;

namespace ThiefAndTheNecklace.PB1
{
    [Serializable]
    public sealed class PlayerScenarioInfo
    {
        public RoleType Role;
        public string PrivateSituation;
        public List<string> Clues = new List<string>();
    }
}
