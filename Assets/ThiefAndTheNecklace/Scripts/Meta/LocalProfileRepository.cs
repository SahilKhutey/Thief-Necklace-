using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace ThiefAndTheNecklace
{
    /// <summary>Development-only local profile store with a recovery copy; not an authority boundary.</summary>
    public sealed class LocalProfileRepository : IProfileRepository
    {
        private readonly string _path;
        private readonly string _backupPath;
        private readonly ProgressionService _progression = new ProgressionService();
        private bool _restoredFromBackup;

        public LocalProfileRepository(string path = null)
        {
            _path = string.IsNullOrWhiteSpace(path)
                ? Path.Combine(Application.persistentDataPath, "thief_necklace_profile.json") : path;
            _backupPath = _path + ".bak";
        }

        public Task<PlayerProfile> LoadAsync(string playerId)
        {
            var profile = TryRead(_path);
            if (profile == null)
            {
                profile = TryRead(_backupPath);
                _restoredFromBackup = profile != null || File.Exists(_path);
            }
            profile ??= CreateDefault(playerId);
            if (string.IsNullOrWhiteSpace(profile.playerId)) profile.playerId = playerId ?? "local-player";
            if (string.IsNullOrWhiteSpace(profile.displayName)) profile.displayName = "Player 1";
            _progression.EnsureDefaults(profile);
            return Task.FromResult(profile);
        }

        public Task SaveAsync(PlayerProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            _progression.EnsureDefaults(profile);
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            var tempPath = _path + ".tmp";
            File.WriteAllText(tempPath, JsonUtility.ToJson(profile, true));
            if (File.Exists(_path))
            {
                if (!_restoredFromBackup) File.Copy(_path, _backupPath, true);
                File.Copy(tempPath, _path, true);
                File.Delete(tempPath);
                _restoredFromBackup = false;
            }
            else File.Move(tempPath, _path);
            return Task.CompletedTask;
        }

        public void Reset(string playerId)
        {
            if (File.Exists(_path)) File.Delete(_path);
            if (File.Exists(_backupPath)) File.Delete(_backupPath);
            var fresh = CreateDefault(playerId);
            _progression.EnsureDefaults(fresh);
            SaveAsync(fresh).GetAwaiter().GetResult();
        }

        private static PlayerProfile TryRead(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                var profile = JsonUtility.FromJson<PlayerProfile>(File.ReadAllText(path));
                if (profile == null || profile.schemaVersion <= 0 || profile.totalXp < 0) return null;
                return profile;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static PlayerProfile CreateDefault(string playerId) => new PlayerProfile
        {
            schemaVersion = 1,
            playerId = string.IsNullOrWhiteSpace(playerId) ? "local-player" : playerId,
            displayName = "Player 1"
        };
    }

    public sealed class ProfileService
    {
        private readonly IProfileRepository _repository;
        private readonly ProgressionService _progression;
        public PlayerProfile Current { get; private set; }

        public ProfileService(IProfileRepository repository, ProgressionService progression = null)
        { _repository = repository ?? throw new ArgumentNullException(nameof(repository)); _progression = progression ?? new ProgressionService(); }

        public void Load(string playerId)
        {
            Current = _repository.LoadAsync(playerId).GetAwaiter().GetResult();
            _progression.EnsureDefaults(Current);
            Save();
        }

        public bool GrantReward(RewardTransaction reward)
        {
            if (!_progression.ApplyReward(Current, reward)) return false;
            _progression.EvaluateAchievements(Current);
            Save();
            return true;
        }

        public bool Equip(CosmeticType type, string itemId)
        {
            if (!_progression.Equip(Current, type, itemId)) return false;
            Save();
            return true;
        }

        public bool IsUnlocked(string itemId) => _progression.IsUnlocked(Current, itemId);

        public void EvaluateAchievements()
        {
            _progression.EvaluateAchievements(Current);
            Save();
        }

        public void Save() => _repository.SaveAsync(Current).GetAwaiter().GetResult();

        public void ClearAchievementNotifications()
        {
            _progression.ClearPendingAchievements(Current);
            Save();
        }
    }
}
