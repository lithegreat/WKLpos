using WanKePos.Domain.Entities;

namespace WanKePos.WinUI.Messages;

/// <summary>
/// 系统设置自动保存或更新消息
/// </summary>
public class SettingsChangedMessage
{
    public StoreSettings Settings { get; }

    public SettingsChangedMessage(StoreSettings settings)
    {
        Settings = settings;
    }
}
