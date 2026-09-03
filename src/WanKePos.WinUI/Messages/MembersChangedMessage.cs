namespace WanKePos.WinUI.Messages
{
    /// <summary>
    /// 会员数据发生变更通知消息 (新增、修改、删除、注销等)
    /// </summary>
    public class MembersChangedMessage
    {
        public int? DeletedMemberId { get; }

        public MembersChangedMessage(int? deletedMemberId = null)
        {
            DeletedMemberId = deletedMemberId;
        }
    }
}
