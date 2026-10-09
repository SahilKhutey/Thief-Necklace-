using System;
using System.Collections.Generic;

namespace ThiefAndTheNecklace
{
    [Serializable]
    public sealed class PlayerProfile
    {
        public int schemaVersion = 1;
        public long profileVersion;
        public string playerId = "local-player";
        public string displayName = "Player 1";
        public long totalXp;
        public PlayerStats stats = new PlayerStats();
        public PlayerLoadout loadout = new PlayerLoadout();
        public List<string> unlockedCosmeticIds = new List<string>();
        public List<AchievementProgress> achievements = new List<AchievementProgress>();
        public List<string> processedRewardEventIds = new List<string>();
        public List<MatchHistoryEntry> matchHistory = new List<MatchHistoryEntry>();
        public List<RolePerformance> roleHistory = new List<RolePerformance>();
        public List<string> pendingAchievementIds = new List<string>();
    }

    [Serializable]
    public sealed class PlayerStats
    {
        public int gamesPlayed;
        public int matchesWon;
        public int roundsPlayed;
        public int correctVotes;
        public int incorrectVotes;
        public int thiefRounds;
        public int thiefEscapes;
        public int timesCaught;
        public int perfectMatches;
    }

    [Serializable]
    public sealed class PlayerLoadout
    {
        public string avatarId = "avatar_basic";
        public string cardBackId = "cardback_royal";
        public string frameId = "frame_basic";
        public string titleId = "title_truth_seeker";
        public string tableThemeId = "theme_royal_court";
        public string emoteSetId = "emote_basic";
    }

    [Serializable]
    public sealed class AchievementProgress
    {
        public string achievementId;
        public int progress;
        public bool unlocked;
    }

    [Serializable]
    public sealed class MatchHistoryEntry
    {
        public string matchId;
        public string playedAtUtc;
        public int rounds;
        public int matchPoints;
        public bool won;
        public int correctVotes;
        public int thiefEscapes;
    }

    [Serializable]
    public sealed class RolePerformance
    {
        public string roleId;
        public int roundsPlayed;
        public int totalMatchPoints;
        public int correctVotes;
        public int thiefEscapes;
    }

    [Serializable]
    public sealed class RewardTransaction
    {
        public string transactionId;
        public string eventId;
        public int xp;
        public string reason;
    }

    [Serializable]
    public sealed class CosmeticDefinition
    {
        public string id;
        public CosmeticType type;
        public string displayName;
        public int unlockLevel;
        public bool defaultUnlocked;

        public CosmeticDefinition(string id, CosmeticType type, string displayName, int unlockLevel, bool defaultUnlocked)
        { this.id = id; this.type = type; this.displayName = displayName; this.unlockLevel = unlockLevel; this.defaultUnlocked = defaultUnlocked; }
    }

    public enum CosmeticType { Avatar, CardBack, Frame, Title, TableTheme, Emote }

    public static class CosmeticCatalog
    {
        public static readonly CosmeticDefinition[] All =
        {
            new CosmeticDefinition("avatar_basic", CosmeticType.Avatar, "Court Guest", 1, true),
            new CosmeticDefinition("avatar_guard", CosmeticType.Avatar, "Palace Guard", 4, false),
            new CosmeticDefinition("cardback_royal", CosmeticType.CardBack, "Royal Crest", 1, true),
            new CosmeticDefinition("cardback_midnight", CosmeticType.CardBack, "Midnight Palace", 3, false),
            new CosmeticDefinition("frame_basic", CosmeticType.Frame, "Parchment Frame", 1, true),
            new CosmeticDefinition("frame_emerald", CosmeticType.Frame, "Emerald Court", 5, false),
            new CosmeticDefinition("title_truth_seeker", CosmeticType.Title, "Truth Seeker", 1, true),
            new CosmeticDefinition("title_investigator", CosmeticType.Title, "The Investigator", 2, false),
            new CosmeticDefinition("theme_royal_court", CosmeticType.TableTheme, "Royal Court", 1, true),
            new CosmeticDefinition("theme_moonlit", CosmeticType.TableTheme, "Moonlit Palace", 6, false),
            new CosmeticDefinition("emote_basic", CosmeticType.Emote, "Basic Reactions", 1, true),
            new CosmeticDefinition("emote_court", CosmeticType.Emote, "Court Reactions", 3, false)
        };

        public static CosmeticDefinition Find(string id)
        {
            foreach (var item in All) if (item.id == id) return item;
            return null;
        }
    }

    [Serializable]
    public sealed class AchievementDefinition
    {
        public string id;
        public string title;
        public string description;
        public int target;
        public int xpReward;
        public string cosmeticUnlockId;
        public AchievementDefinition(string id, string title, string description, int target, int xpReward, string cosmeticUnlockId = null)
        { this.id = id; this.title = title; this.description = description; this.target = target; this.xpReward = xpReward; this.cosmeticUnlockId = cosmeticUnlockId; }
    }

    public static class AchievementCatalog
    {
        public static readonly AchievementDefinition[] All =
        {
            new AchievementDefinition("first_correct_vote", "First Accusation", "Cast your first correct vote.", 1, 25),
            new AchievementDefinition("sharp_eye", "Sharp Eye", "Correctly identify ten Thieves.", 10, 50, "title_investigator"),
            new AchievementDefinition("first_escape", "First Escape", "Escape once as the Thief.", 1, 25),
            new AchievementDefinition("five_mysteries", "Five Mysteries", "Complete your first full match.", 1, 50),
            new AchievementDefinition("court_regular", "Regular at Court", "Complete ten matches.", 10, 100, "frame_emerald"),
            new AchievementDefinition("perfect_deduction", "Perfect Deduction", "Vote correctly in every round you played as an Innocent.", 1, 75)
        };

        public static AchievementDefinition Find(string id)
        {
            foreach (var item in All) if (item.id == id) return item;
            return null;
        }
    }
}
