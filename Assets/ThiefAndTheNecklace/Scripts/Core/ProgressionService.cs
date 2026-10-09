using System;
using System.Collections.Generic;
using System.Linq;

namespace ThiefAndTheNecklace
{
    public static class LevelService
    {
        public static int GetLevel(long totalXp)
        {
            var level = 1;
            var remaining = Math.Max(0L, totalXp);
            while (remaining >= XpToNextLevel(level) && level < 1000)
            {
                remaining -= XpToNextLevel(level);
                level++;
            }
            return level;
        }

        public static int XpToNextLevel(int level) => 100 + Math.Max(0, level - 1) * 50;

        public static long XpAtLevelStart(int level)
        {
            long sum = 0;
            for (var current = 1; current < Math.Max(1, level); current++) sum += XpToNextLevel(current);
            return sum;
        }

        public static float ProgressInLevel(long totalXp)
        {
            var level = GetLevel(totalXp);
            return (float)(totalXp - XpAtLevelStart(level)) / XpToNextLevel(level);
        }
    }

    public sealed class ProgressionService
    {
        public bool ApplyReward(PlayerProfile profile, RewardTransaction reward)
        {
            if (profile == null || reward == null || string.IsNullOrWhiteSpace(reward.eventId) || reward.xp < 0) return false;
            profile.processedRewardEventIds ??= new List<string>();
            profile.unlockedCosmeticIds ??= new List<string>();
            if (profile.processedRewardEventIds.Contains(reward.eventId)) return false;
            profile.processedRewardEventIds.Add(reward.eventId);
            profile.totalXp += reward.xp;
            profile.profileVersion++;
            UnlockLevelCosmetics(profile);
            return true;
        }

        public List<AchievementDefinition> EvaluateAchievements(PlayerProfile profile)
        {
            var unlocked = new List<AchievementDefinition>();
            foreach (var definition in AchievementCatalog.All)
            {
                var progress = GetOrCreate(profile, definition.id);
                progress.progress = GetProgress(profile, definition.id);
                if (!progress.unlocked && progress.progress >= definition.target)
                {
                    progress.unlocked = true;
                    unlocked.Add(definition);
                    if (!string.IsNullOrEmpty(definition.cosmeticUnlockId)) Unlock(profile, definition.cosmeticUnlockId);
                    ApplyReward(profile, new RewardTransaction
                    {
                        transactionId = "achievement:" + definition.id,
                        eventId = "achievement:" + definition.id,
                        xp = definition.xpReward,
                        reason = definition.title
                    });
                    profile.pendingAchievementIds.Add(definition.id);
                }
            }
            return unlocked;
        }

        public bool Unlock(PlayerProfile profile, string cosmeticId)
        {
            if (profile == null || CosmeticCatalog.Find(cosmeticId) == null) return false;
            profile.unlockedCosmeticIds ??= new List<string>();
            if (profile.unlockedCosmeticIds.Contains(cosmeticId)) return false;
            profile.unlockedCosmeticIds.Add(cosmeticId);
            profile.profileVersion++;
            return true;
        }

        public bool Equip(PlayerProfile profile, CosmeticType type, string cosmeticId)
        {
            var item = CosmeticCatalog.Find(cosmeticId);
            if (profile == null || item == null || item.type != type || !IsUnlocked(profile, cosmeticId)) return false;
            switch (type)
            {
                case CosmeticType.Avatar: profile.loadout.avatarId = cosmeticId; break;
                case CosmeticType.CardBack: profile.loadout.cardBackId = cosmeticId; break;
                case CosmeticType.Frame: profile.loadout.frameId = cosmeticId; break;
                case CosmeticType.Title: profile.loadout.titleId = cosmeticId; break;
                case CosmeticType.TableTheme: profile.loadout.tableThemeId = cosmeticId; break;
                case CosmeticType.Emote: profile.loadout.emoteSetId = cosmeticId; break;
                default: return false;
            }
            profile.profileVersion++;
            return true;
        }

        public bool IsUnlocked(PlayerProfile profile, string cosmeticId)
        {
            var item = CosmeticCatalog.Find(cosmeticId);
            return item != null && (item.defaultUnlocked || profile != null && profile.unlockedCosmeticIds != null && profile.unlockedCosmeticIds.Contains(cosmeticId));
        }

        public void EnsureDefaults(PlayerProfile profile)
        {
            if (profile.stats == null) profile.stats = new PlayerStats();
            if (profile.loadout == null) profile.loadout = new PlayerLoadout();
            if (profile.achievements == null) profile.achievements = new List<AchievementProgress>();
            if (profile.unlockedCosmeticIds == null) profile.unlockedCosmeticIds = new List<string>();
            if (profile.processedRewardEventIds == null) profile.processedRewardEventIds = new List<string>();
            if (profile.matchHistory == null) profile.matchHistory = new List<MatchHistoryEntry>();
            if (profile.roleHistory == null) profile.roleHistory = new List<RolePerformance>();
            if (profile.pendingAchievementIds == null) profile.pendingAchievementIds = new List<string>();
            foreach (var item in CosmeticCatalog.All.Where(c => c.defaultUnlocked)) Unlock(profile, item.id);
            UnlockLevelCosmetics(profile);
            profile.loadout.avatarId = EnsureEquipped(profile, CosmeticType.Avatar, profile.loadout.avatarId, "avatar_basic");
            profile.loadout.cardBackId = EnsureEquipped(profile, CosmeticType.CardBack, profile.loadout.cardBackId, "cardback_royal");
            profile.loadout.frameId = EnsureEquipped(profile, CosmeticType.Frame, profile.loadout.frameId, "frame_basic");
            profile.loadout.titleId = EnsureEquipped(profile, CosmeticType.Title, profile.loadout.titleId, "title_truth_seeker");
            profile.loadout.tableThemeId = EnsureEquipped(profile, CosmeticType.TableTheme, profile.loadout.tableThemeId, "theme_royal_court");
            profile.loadout.emoteSetId = EnsureEquipped(profile, CosmeticType.Emote, profile.loadout.emoteSetId, "emote_basic");
        }

        public void ClearPendingAchievements(PlayerProfile profile) => profile?.pendingAchievementIds?.Clear();

        private static string EnsureEquipped(PlayerProfile profile, CosmeticType type, string id, string fallback)
        {
            var item = CosmeticCatalog.Find(id);
            return item != null && item.type == type && (item.defaultUnlocked || profile.unlockedCosmeticIds.Contains(id)) ? id : fallback;
        }

        private static AchievementProgress GetOrCreate(PlayerProfile profile, string id)
        {
            profile.achievements ??= new List<AchievementProgress>();
            var progress = profile.achievements.FirstOrDefault(p => p.achievementId == id);
            if (progress != null) return progress;
            progress = new AchievementProgress { achievementId = id };
            profile.achievements.Add(progress);
            return progress;
        }

        private static int GetProgress(PlayerProfile profile, string id)
        {
            switch (id)
            {
                case "first_correct_vote": return profile.stats.correctVotes;
                case "sharp_eye": return profile.stats.correctVotes;
                case "first_escape": return profile.stats.thiefEscapes;
                case "five_mysteries": return profile.stats.gamesPlayed;
                case "court_regular": return profile.stats.gamesPlayed;
                case "perfect_deduction": return profile.stats.perfectMatches;
                default: return 0;
            }
        }

        private static void UnlockLevelCosmetics(PlayerProfile profile)
        {
            var level = LevelService.GetLevel(profile.totalXp);
            foreach (var item in CosmeticCatalog.All)
                if (item.unlockLevel <= level) profile.unlockedCosmeticIds.AddUnique(item.id);
        }
    }

    internal static class ProfileListExtensions
    {
        public static void AddUnique(this List<string> list, string value)
        { if (!list.Contains(value)) list.Add(value); }
    }
}
