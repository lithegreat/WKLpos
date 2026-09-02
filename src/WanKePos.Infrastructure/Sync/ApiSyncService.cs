using System.Threading.Tasks;
using WanKePos.Domain.Interfaces;

namespace WanKePos.Infrastructure.Sync
{
    /// <summary>
    /// 联网同步服务占位实现
    /// TODO: 未来实现 REST API 与远程后台的实时同步
    /// 计划接口: /api/v1/products, /api/v1/orders, /api/v1/members
    /// </summary>
    public class ApiSyncService : ISyncService
    {
        /// <summary>
        /// 同步功能是否启用（当前版本默认关闭）
        /// </summary>
        public bool IsEnabled => false;

        public Task<bool> SyncProductsAsync()
        {
            // TODO: 从远程 API 拉取商品数据并更新本地数据库
            return Task.FromResult(false);
        }

        public Task<bool> SyncMembersAsync()
        {
            // TODO: 与远程 API 同步会员信息
            return Task.FromResult(false);
        }

        public Task<bool> SyncOrdersAsync()
        {
            // TODO: 将本地订单推送到远程 API
            return Task.FromResult(false);
        }

        public Task<bool> IsConnectedAsync()
        {
            // TODO: 检测与远程服务器的连接状态
            return Task.FromResult(false);
        }
    }
}
