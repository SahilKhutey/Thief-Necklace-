namespace ThiefAndTheNecklace.PB1
{
    /// <summary>Public round and match scores, returned only after authoritative scoring.</summary>
    public sealed class RoundScoreViewData
    {
        public int PlayerId { get; internal set; }
        public string DisplayName { get; internal set; }
        public int RoundScore { get; internal set; }
        public int MatchScore { get; internal set; }
    }
}
