using ClosedXML.Excel;
using System;
using System.IO;
using System.Threading.Tasks;
using WanKePos.Domain.Entities;
using WanKePos.Domain.Interfaces;

namespace WanKePos.Infrastructure.Export;

/// <summary>
/// 采购订单 Excel 导出服务 (基于 zggj_门店商品-批量收货.xlsx 模板)
/// </summary>
public class PurchaseOrderExporter
{
    public static string UserDownloadsTemplatePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "zggj_门店商品-批量收货.xlsx");

    private readonly IProductRepository? _productRepo;
    private readonly IPurchaseOrderRepository? _purchaseRepo;

    public PurchaseOrderExporter(IProductRepository? productRepo = null, IPurchaseOrderRepository? purchaseRepo = null)
    {
        _productRepo = productRepo;
        _purchaseRepo = purchaseRepo;
    }

    /// <summary>
    /// 定位收货模板路径：优先当前用户通用 Downloads 目录，其次当前程序安装目录下的 Templates 目录
    /// </summary>
    public static string? ResolveTemplatePath()
    {
        // 1. 当前用户通用下载目录
        var userDownloads = UserDownloadsTemplatePath;
        if (File.Exists(userDownloads))
            return userDownloads;

        // 2. 程序安装目录内附带的模板
        var localTemplate = Path.Combine(AppContext.BaseDirectory, "Templates", "zggj_门店商品-批量收货.xlsx");
        if (File.Exists(localTemplate))
            return localTemplate;

        return null;
    }

    public static string DefaultExportDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "采购单");

    /// <summary>
    /// 生成符合人类可读性且时间前置（便于 Windows 资源管理器按名称降序排序时最新文件排在最上方）的建议导出文件名
    /// </summary>
    public static string GenerateDefaultFileName(PurchaseOrder order)
    {
        var supplier = !string.IsNullOrWhiteSpace(order.Supplier)
            ? SanitizeFileName(order.Supplier)
            : "通用供货商";
        var orderTime = order.CreatedAt != default ? order.CreatedAt : DateTime.Now;
        var timeStr = orderTime.ToString("yyyy-MM-dd_HHmmss");
        var orderNo = !string.IsNullOrWhiteSpace(order.PurchaseOrderNo)
            ? SanitizeFileName(order.PurchaseOrderNo)
            : DateTime.Now.ToString("yyyyMMddHHmmss");

        return $"{timeStr}_采购单_{supplier}_{orderNo}.xlsx";
    }

    /// <summary>
    /// 清洗文件名中的非法字符，替换为下划线
    /// </summary>
    public static string SanitizeFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;
        var invalidChars = Path.GetInvalidFileNameChars();
        foreach (var c in invalidChars)
        {
            name = name.Replace(c, '_');
        }
        return name.Trim();
    }

    public async Task<string> ExportToExcelAsync(PurchaseOrder order, StoreSettings? storeSettings, string? targetFilePath = null)
    {
        // 若传入的采购单缺少商品明细（例如从摘要列表传入），自动从仓储重新加载完整明细
        if ((order.Items == null || order.Items.Count == 0) && _purchaseRepo != null && order.Id > 0)
        {
            var loaded = await _purchaseRepo.GetByIdAsync(order.Id);
            if (loaded != null && loaded.Items != null && loaded.Items.Count > 0)
            {
                order = loaded;
            }
        }

        if (string.IsNullOrWhiteSpace(targetFilePath))
        {
            var fileName = GenerateDefaultFileName(order);
            targetFilePath = Path.Combine(DefaultExportDirectory, fileName);
        }

        // 确保所有明细项的商品详情均已加载
        if (_productRepo != null && order.Items != null)
        {
            foreach (var item in order.Items)
            {
                if (item.Product == null)
                {
                    if (item.ProductId > 0)
                    {
                        var p = await _productRepo.GetByIdAsync(item.ProductId);
                        if (p != null) item.Product = p;
                    }
                    if (item.Product == null && !string.IsNullOrWhiteSpace(item.Barcode))
                    {
                        var p = await _productRepo.GetByBarcodeAsync(item.Barcode);
                        if (p != null) item.Product = p;
                    }
                }
            }
        }

        await Task.Run(() =>
        {
            var dir = Path.GetDirectoryName(targetFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var templatePath = ResolveTemplatePath();
            if (!string.IsNullOrEmpty(templatePath) && File.Exists(templatePath))
            {
                // 使用 FileShare.ReadWrite 读取模板并复制到目标文件，杜绝文件锁冲突与已关闭文件访问错误
                using (var src = new FileStream(templatePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var dst = new FileStream(targetFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    src.CopyTo(dst);
                }

                using var workbook = new XLWorkbook(targetFilePath);
                var ws = workbook.Worksheet(1);

                // 清除模板中的原有示例行内容 (行 2 及以下，保留表头样式与数据验证规则)
                int lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
                for (int r = 2; r <= lastRow; r++)
                {
                    ws.Row(r).Clear(XLClearOptions.Contents);
                }

                // 填充当前采购单明细数据
                FillOrderData(ws, order);

                workbook.Save();
            }
            else
            {
                // 若模板文件在任何地方均不存在，动态构建具备相同规范的工作簿作为兜底
                using var workbook = CreateDefaultZggjWorkbook();
                var ws = workbook.Worksheet(1);
                FillOrderData(ws, order);
                workbook.SaveAs(targetFilePath);
            }
        });

        return targetFilePath;
    }

    private static void FillOrderData(IXLWorksheet ws, PurchaseOrder order)
    {
        if (order.Items == null) return;
        int currentRow = 2;
        foreach (var item in order.Items)
        {
            var product = item.Product;

            // Col A: 商品类型（必填） - 标品 / 非标品
            var prodType = !string.IsNullOrWhiteSpace(product?.ProductType) ? product.ProductType : "标品";
            ws.Cell(currentRow, 1).SetValue(prodType);

            // Col B: 条形码/简码(标品必填，多个条码用","隔开) - 文本格式保证条码不被转换为科学记数法
            var barcodeCell = ws.Cell(currentRow, 2);
            barcodeCell.SetValue(item.Barcode ?? "");
            barcodeCell.Style.NumberFormat.Format = "@";

            // Col C: 商品名称（必填）
            ws.Cell(currentRow, 3).SetValue(item.ProductName ?? product?.Name ?? "");

            // Col D: 入库数量（必填，新增加的商品数量）
            ws.Cell(currentRow, 4).SetValue(item.Quantity);
            ws.Cell(currentRow, 4).Style.NumberFormat.Format = item.Quantity % 1 == 0 ? "#,##0" : "#,##0.##";

            // Col E: 门店零售价(选填)
            if (product != null && product.RetailPrice > 0)
            {
                ws.Cell(currentRow, 5).SetValue(product.RetailPrice);
                ws.Cell(currentRow, 5).Style.NumberFormat.Format = "0.00";
            }

            // Col F: 入库进货价(选填)
            ws.Cell(currentRow, 6).SetValue(item.CostPrice);
            ws.Cell(currentRow, 6).Style.NumberFormat.Format = "0.00";

            // Col G: 售卖方式（非标品商品必填） - 按件 / 称重
            var saleMethod = !string.IsNullOrWhiteSpace(product?.SaleMethod) ? product.SaleMethod : "按件";
            ws.Cell(currentRow, 7).SetValue(saleMethod);

            // Col H: 系统末级品类
            if (!string.IsNullOrWhiteSpace(product?.SystemCategory))
            {
                ws.Cell(currentRow, 8).SetValue(product.SystemCategory);
            }

            // Col I: 店内末级品类
            if (!string.IsNullOrWhiteSpace(product?.StoreCategory))
            {
                ws.Cell(currentRow, 9).SetValue(product.StoreCategory);
            }

            // Col L: 货号(选填，一码多品必填)
            if (!string.IsNullOrWhiteSpace(product?.ArticleNumber))
            {
                ws.Cell(currentRow, 12).SetValue(product.ArticleNumber);
            }

            // Col M: 门店会员价(选填)
            if (product?.MemberPrice.HasValue == true && product.MemberPrice.Value > 0)
            {
                ws.Cell(currentRow, 13).SetValue(product.MemberPrice.Value);
                ws.Cell(currentRow, 13).Style.NumberFormat.Format = "0.00";
            }

            // Col N: 图片(选填)
            if (!string.IsNullOrWhiteSpace(product?.ImageUrl))
            {
                ws.Cell(currentRow, 14).SetValue(product.ImageUrl);
            }

            // Col O: 商品品牌(选填)
            if (!string.IsNullOrWhiteSpace(product?.Brand))
            {
                ws.Cell(currentRow, 15).SetValue(product.Brand);
            }

            // Col R: 销售单位(选填)
            var unit = !string.IsNullOrWhiteSpace(item.SaleUnit) ? item.SaleUnit : (product?.SaleUnit ?? "件");
            ws.Cell(currentRow, 18).SetValue(unit);

            // Col S: 规格(选填)
            var spec = !string.IsNullOrWhiteSpace(item.Specification) ? item.Specification : (product?.Specification ?? "");
            if (!string.IsNullOrWhiteSpace(spec))
            {
                ws.Cell(currentRow, 19).SetValue(spec);
            }

            // Col X: 供应商(选填，不可填写为掌柜宝)
            var supplier = !string.IsNullOrWhiteSpace(order.Supplier)
                ? order.Supplier
                : (!string.IsNullOrWhiteSpace(product?.Supplier) ? product.Supplier : "第三方");
            ws.Cell(currentRow, 24).SetValue(supplier);

            // Col Y: 生产日期(天)(选填)
            ws.Cell(currentRow, 25).SetValue(order.CreatedAt.ToString("yyyy-MM-dd"));

            currentRow++;
        }
    }

    private static XLWorkbook CreateDefaultZggjWorkbook()
    {
        var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Sheet1");

        string[] headers =
        {
            "商品类型\n（必填）",
            "条形码/简码(标品必填，多个条码用\",\"隔开)",
            "商品名称（必填）",
            "入库数量（必填，新增加的商品数量）",
            "门店零售价(选填)",
            "入库进货价(选填)",
            "售卖方式（非标品商品必填）",
            "系统末级品类",
            "店内末级品类",
            "毛重",
            "",
            "货号(选填，一码多品必填)",
            "门店会员价(选填)",
            "图片(选填)",
            "商品品牌(选填)",
            "保质期(天)(选填)",
            "产地(选填)",
            "销售单位(选填)",
            "规格(选填)",
            "",
            "长(mm)(选填)",
            "宽(mm)(选填)",
            "高(mm)(选填)",
            "供应商(选填，不可填写为掌柜宝)",
            "生产日期(天)(选填)"
        };

        for (int i = 0; i < headers.Length; i++)
        {
            ws.Cell(1, i + 1).SetValue(headers[i]);
        }

        var headerRow = ws.Row(1);
        headerRow.Height = 28;
        headerRow.Style.Font.Bold = true;
        headerRow.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        return workbook;
    }
}
