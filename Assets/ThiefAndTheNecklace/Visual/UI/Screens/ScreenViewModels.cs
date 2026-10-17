using System.Collections.Generic;

namespace ThiefAndTheNecklace.Visual.UI
{
    /// <summary>Public presentation data. Do not add secret role/truth fields here.</summary>
    public class ScreenViewModel
    {
        public string Title = string.Empty;
        public string Subtitle = string.Empty;
        public string Description = string.Empty;
        public string PrimaryAction = string.Empty;
        public string Status = string.Empty;
    }

    public sealed class ProfileScreenViewModel : ScreenViewModel
    {
        public string DisplayName = string.Empty;
        public int Level;
        public long TotalExperience;
        public int MatchesPlayed;
        public int MysteriesSolved;
    }

    public sealed class LobbyScreenViewModel : ScreenViewModel
    {
        public string RoomCode = string.Empty;
        public int PlayerCount;
        public int Capacity;
        public bool LocalPlayerReady;
        public bool CanStart;
        public IReadOnlyList<PlayerCardViewModel> Players;
    }

    public sealed class GameScreenViewModel : ScreenViewModel
    {
        public int CurrentRound;
        public int TotalRounds;
        public float RemainingTime;
        public string ScenarioTitle = string.Empty;
        public bool CanVote;
        public bool ChatOpen;
        public string LocalPlayerId = string.Empty;
        public IReadOnlyList<PlayerCardViewModel> PublicPlayers;
    }

    /// <summary>Private local-player information; never bind this model into lobby or public player views.</summary>
    public sealed class PrivateRoleScreenViewModel : ScreenViewModel
    {
        public string RoleName = string.Empty;
        public UnityEngine.Sprite RoleArtwork;
        public int RoleValue;
        public string Situation = string.Empty;
        public IReadOnlyList<string> Clues;
        public string Objective = string.Empty;
    }

    public sealed class ResultsScreenViewModel : ScreenViewModel
    {
        public int ScoreDelta;
        public int TotalScore;
        public string Summary = string.Empty;
        public IReadOnlyList<PlayerCardViewModel> Rankings;
    }

    /// <summary>Only construct after the authoritative runtime has authorized a reveal.</summary>
    public sealed class AuthorizedRevealViewModel : ScreenViewModel
    {
        public string RevealedRole = string.Empty;
        public string Outcome = string.Empty;
        public int ScoreDelta;
        public bool IsThiefObjectiveReveal;
        public UnityEngine.Sprite RevealedArtwork;
    }
}
