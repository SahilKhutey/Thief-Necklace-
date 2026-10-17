namespace ThiefAndTheNecklace.PB1
{
    /// <summary>Aggregate public vote progress. It contains no voter identities or targets.</summary>
    public readonly struct VoteProgressViewData
    {
        public int Submitted { get; }
        public int Eligible { get; }
        public VoteProgressViewData(int submitted, int eligible) { Submitted = submitted; Eligible = eligible; }
    }
}
