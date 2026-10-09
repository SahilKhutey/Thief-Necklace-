using System.Threading.Tasks;

namespace ThiefAndTheNecklace
{
    public interface IProfileRepository
    {
        Task<PlayerProfile> LoadAsync(string playerId);
        Task SaveAsync(PlayerProfile profile);
    }
}
