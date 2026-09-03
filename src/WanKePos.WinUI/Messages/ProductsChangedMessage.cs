namespace WanKePos.WinUI.Messages
{
    /// <summary>
    /// 商品库发生变更通知消息 (新增、修改、删除、导入、采购入库等)
    /// </summary>
    public class ProductsChangedMessage
    {
        public int? DeletedProductId { get; }
        public string? DeletedProductBarcode { get; }

        public ProductsChangedMessage(int? deletedProductId = null, string? deletedProductBarcode = null)
        {
            DeletedProductId = deletedProductId;
            DeletedProductBarcode = deletedProductBarcode;
        }
    }
}
