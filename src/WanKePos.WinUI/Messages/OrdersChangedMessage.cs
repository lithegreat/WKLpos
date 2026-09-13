namespace WanKePos.WinUI.Messages
{
    /// <summary>
    /// 订单数据发生变更通知消息 (前台结账完成、订单退款、订单状态修改等)
    /// </summary>
    public class OrdersChangedMessage
    {
        public int? OrderId { get; }

        public OrdersChangedMessage(int? orderId = null)
        {
            OrderId = orderId;
        }
    }
}
