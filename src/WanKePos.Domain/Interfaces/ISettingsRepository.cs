using System.Threading.Tasks;
using WanKePos.Domain.Entities;

namespace WanKePos.Domain.Interfaces;

public interface ISettingsRepository
{
    Task<StoreSettings> GetSettingsAsync();
    Task SaveSettingsAsync(StoreSettings settings);
}
