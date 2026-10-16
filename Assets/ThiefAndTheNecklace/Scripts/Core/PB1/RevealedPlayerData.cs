namespace ThiefAndTheNecklace.PB1
{
    public sealed class RevealedPlayerData
    {
        public int PlayerId { get; internal set; }
        public RoleType Role { get; internal set; }
        public bool IsThief { get; internal set; }
    }
}
