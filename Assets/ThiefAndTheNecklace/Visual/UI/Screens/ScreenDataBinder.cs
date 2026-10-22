using UnityEngine.UIElements;
using UnityEngine;
using ThiefAndTheNecklace.Visual;
using ThiefAndTheNecklace.Visual.Gameplay;

namespace ThiefAndTheNecklace.Visual.UI
{
    /// <summary>Maps approved presentation data to named labels; contains no gameplay decisions.</summary>
    public static class ScreenDataBinder
    {
        public static void Bind(VisualElement screen, ScreenViewModel data)
        {
            if (screen == null || data == null) return;
            SetIfProvided(screen, "screen-title", data.Title);
            SetIfProvided(screen, "screen-subtitle", data.Subtitle);
            SetIfProvided(screen, "screen-status", data.Status);
            if (!string.IsNullOrEmpty(data.PrimaryAction))
            {
                var action = screen.Q<Button>("primary-action");
                if (action != null) action.text = data.PrimaryAction;
            }
            if (data is GameScreenViewModel game)
            {
                Set(screen, "timer", FormatTime(game.RemainingTime));
                Set(screen, "screen-title", $"ROUND {game.CurrentRound} / {game.TotalRounds}");
                SetIfProvided(screen, "scenario-title", game.ScenarioTitle);
                var vote = screen.Q<Button>("go-vote");
                if (vote != null) vote.SetEnabled(game.CanVote);
                BindPlayerSeats(screen, game);
            }
            if (data is AuthorizedRevealViewModel reveal)
            {
                Set(screen, "revealed-role", reveal.RevealedRole);
                Set(screen, "outcome", reveal.Outcome);
                SetIfProvided(screen, "reveal-target", reveal.SelectedTarget);
                SetIfProvided(screen, "reveal-thief", reveal.ThiefPlayer);
                SetIfProvided(screen, "necklace-owner", reveal.NecklaceRevealed ? reveal.NecklaceOwner : string.Empty);
                Set(screen, "score-delta", reveal.ScoreDelta >= 0 ? "+" + reveal.ScoreDelta : reveal.ScoreDelta.ToString());
                var artwork = screen.Q<VisualElement>("revealed-artwork");
                if (artwork != null && reveal.RevealedArtwork != null) artwork.style.backgroundImage = new StyleBackground(reveal.RevealedArtwork);
                VisualEventBus.Raise(VisualEventType.RoleRevealed);
            }
            if (data is ResultsScreenViewModel results)
            {
                Set(screen, "score", results.ScoreDelta >= 0 ? "+ " + results.ScoreDelta : results.ScoreDelta.ToString());
                Set(screen, "result-summary", results.Summary);
                Set(screen, "total-score", results.TotalScore.ToString());
                SetIfProvided(screen, "screen-subtitle", results.Summary);
                BindRankings(screen, results.Rankings);
            }
            if (data is ProfileScreenViewModel profile)
            {
                SetIfProvided(screen, "profile-name", profile.DisplayName);
                Set(screen, "profile-level", profile.Level.ToString());
                Set(screen, "profile-xp", profile.TotalExperience.ToString());
                Set(screen, "profile-matches", profile.MatchesPlayed.ToString());
                Set(screen, "profile-solved", profile.MysteriesSolved.ToString());
            }
        }

        private static void BindPlayerSeats(VisualElement screen, GameScreenViewModel data)
        {
            var seatsRoot = screen.Q<VisualElement>("game-player-seats");
            if (seatsRoot == null || data.PublicPlayers == null) return;
            seatsRoot.Clear();
            if (data.PublicPlayers.Count == 0) return;
            var positions = new PlayerSeatLayout().Generate(data.PublicPlayers.Count, new Vector2(0.5f, 0.5f), 0.36f);
            for (var i = 0; i < data.PublicPlayers.Count; i++)
            {
                var player = data.PublicPlayers[i];
                if (player == null) continue;
                var pos = positions[i];
                var seat = new VisualElement { name = "seat-" + player.PlayerId };
                seat.AddToClassList("pv2-seat");
                var local = !string.IsNullOrEmpty(data.LocalPlayerId) && player.PlayerId == data.LocalPlayerId;
                if (local) seat.AddToClassList("pv2-seat--local");
                seat.style.left = Length.Percent(pos.x * 100f);
                seat.style.top = Length.Percent((1f - pos.y) * 100f);
                seat.style.translate = new Translate(-60, -34);
                var name = new Label(player.DisplayName ?? string.Empty); name.AddToClassList("pv2-seat__name");
                var statusText = !player.Connected ? "OFFLINE" : local ? "YOU" : (player.Status ?? string.Empty);
                var status = new Label(statusText); status.AddToClassList("pv2-seat__status");
                seat.Add(name); seat.Add(status);
                seatsRoot.Add(seat);
            }
        }

        private static void BindRankings(VisualElement screen, System.Collections.Generic.IReadOnlyList<PlayerCardViewModel> rankings)
        {
            var list = screen.Q<VisualElement>("rank-list");
            if (list == null || rankings == null) return;
            list.Clear();
            for (var i = 0; i < rankings.Count; i++)
            {
                var player = rankings[i];
                if (player == null) continue;
                var row = new VisualElement(); row.AddToClassList("tan-player-card");
                if (i == 0) row.AddToClassList("tan-player-card--winner");
                var rank = new Label((i + 1).ToString()); rank.AddToClassList("tan-player-card__score");
                var name = new Label(player.DisplayName ?? string.Empty); name.AddToClassList("tan-player-card__name");
                var score = new Label(player.Score.ToString()); score.AddToClassList("tan-player-card__score");
                row.Add(rank); row.Add(name);
                if (!string.IsNullOrEmpty(player.Status))
                {
                    var delta = new Label(player.Status); delta.AddToClassList("tan-player-card__status"); row.Add(delta);
                    var amount = 0;
                    int.TryParse(player.Status.Replace("+", string.Empty), out amount);
                    ScoreCountAnimation.Count(score, player.Score - amount, player.Score, 0.55f, i * 130);
                }
                row.Add(score); list.Add(row);
            }
        }

        private static void Set(VisualElement root, string name, string text)
        {
            var label = root.Q<Label>(name);
            if (label != null) label.text = text ?? string.Empty;
        }

        private static void SetIfProvided(VisualElement root, string name, string text)
        {
            if (!string.IsNullOrEmpty(text)) Set(root, name, text);
        }

        private static string FormatTime(float seconds)
        {
            var total = UnityEngine.Mathf.Max(0, UnityEngine.Mathf.CeilToInt(seconds));
            return $"{total / 60:00}:{total % 60:00}";
        }
    }
}
