using WanKePos.Domain.Entities;

namespace WanKePos.WinUI.Messages;

/// <summary>
/// 检测到新版本更新消息 (由启动后台任务或手动检测触发)
/// </summary>
public class UpdateAvailableMessage
{
    public UpdateInfo UpdateInfo { get; }

    public UpdateAvailableMessage(UpdateInfo updateInfo)
    {
        UpdateInfo = updateInfo;
    }
}
