using System.Threading.Tasks;

namespace WanKePos.Domain.Interfaces;

public interface ISyncService
{
    Task<bool> SyncProductsAsync();
    Task<bool> SyncMembersAsync();
    Task<bool> SyncOrdersAsync();
    Task<bool> IsConnectedAsync();
    bool IsEnabled { get; }
}
