namespace ThiefAndTheNecklace.Visual.UI
{
    /// <summary>Primary pages. Temporary UI belongs in VisualOverlayId.</summary>
    public enum ScreenId
    {
        MainMenu, Profile, CreateRoom, JoinRoom, Lobby, ScenarioIntro, PrivateRole, Game,
        Reveal, RoundResults, MatchResults, Settings, HowToPlay, ExitConfirmation, Error,
        Reconnect, Disconnected
    }

    public enum VisualOverlayId { Chat, Voting, VoteLocked, Settings, Error, Connection, ExitConfirmation, VoteResolution }
}
