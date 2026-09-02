using ClosedXML.Excel;
using System;
using System.IO;
using System.Threading.Tasks;
using WanKePos.Domain.Entities;

namespace WanKePos.Infrastructure.Export;

/// <summary>
/// 采购订单 Excel 导出服务 (发给供货商)
/// </summary>
public class PurchaseOrderExporter
{
    public async Task<string> ExportToExcelAsync(PurchaseOrder order, StoreSettings? storeSettings, string? targetFilePath = null)
    {
        if (string.IsNullOrWhiteSpace(targetFilePath))
        {
            var downloadsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            var fileName = $"采购订单_{order.PurchaseOrderNo}_{(string.IsNullOrEmpty(order.Supplier) ? "通用供货商" : order.Supplier)}_{DateTime.Now:yyyyMMdd}.xlsx";
            targetFilePath = Path.Combine(downloadsFolder, fileName);
        }

        await Task.Run(() =>
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("采购订单");

            // 1. 大标题
            var storeName = storeSettings?.StoreName ?? "万客隆美发用品专卖";
            ws.Cell("A1").Value = $"{storeName} - 商品采购订货单";
            ws.Range("A1:G1").Merge().Style
                .Font.SetBold(true)
                .Font.SetFontSize(16)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
                .Fill.SetBackgroundColor(XLColor.FromHtml("#1F4E78"))
                .Font.SetFontColor(XLColor.White);
            ws.Row(1).Height = 35;

            // 2. 基础单据信息
            ws.Cell("A2").Value = "采购单号:";
            ws.Cell("B2").Value = order.PurchaseOrderNo;
            ws.Cell("D2").Value = "制单日期:";
            ws.Cell("E2").Value = order.CreatedAt.ToString("yyyy-MM-dd HH:mm");

            ws.Cell("A3").Value = "供货商:";
            ws.Cell("B3").Value = string.IsNullOrEmpty(order.Supplier) ? "未指定" : order.Supplier;
            ws.Cell("D3").Value = "联系电话:";
            ws.Cell("E3").Value = storeSettings?.StorePhone ?? "";

            ws.Cell("A4").Value = "收货地址:";
            ws.Cell("B4").Value = storeSettings?.StoreAddress ?? "";
            ws.Range("B4:G4").Merge();

            ws.Cell("A5").Value = "采购备注:";
            ws.Cell("B5").Value = string.IsNullOrEmpty(order.Remark) ? "无" : order.Remark;
            ws.Range("B5:G5").Merge();

            // 格式化表头信息区
            ws.Range("A2:G5").Style.Font.SetFontSize(10);
            ws.Range("A2:A5").Style.Font.SetBold(true);
            ws.Range("D2:D3").Style.Font.SetBold(true);

            // 3. 商品明细表头
            int startRow = 7;
            ws.Cell(startRow, 1).Value = "序号";
            ws.Cell(startRow, 2).Value = "商品条码";
            ws.Cell(startRow, 3).Value = "商品名称";
            ws.Cell(startRow, 4).Value = "规格";
            ws.Cell(startRow, 5).Value = "单位";
            ws.Cell(startRow, 6).Value = "采购单价 (元)";
            ws.Cell(startRow, 7).Value = "采购数量";
            ws.Cell(startRow, 8).Value = "小计金额 (元)";

            var headerRange = ws.Range(startRow, 1, startRow, 8);
            headerRange.Style
                .Font.SetBold(true)
                .Font.SetFontSize(11)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
                .Fill.SetBackgroundColor(XLColor.FromHtml("#D9E1F2"))
                .Border.SetOutsideBorder(XLBorderStyleValues.Thin)
                .Border.SetInsideBorder(XLBorderStyleValues.Thin);
            ws.Row(startRow).Height = 24;

            // 4. 明细数据填充
            int currentRow = startRow + 1;
            int seq = 1;
            foreach (var item in order.Items)
            {
                ws.Cell(currentRow, 1).Value = seq++;
                ws.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

                ws.Cell(currentRow, 2).SetValue(item.Barcode);
                ws.Cell(currentRow, 2).Style.NumberFormat.Format = "@";

                ws.Cell(currentRow, 3).Value = item.ProductName;
                ws.Cell(currentRow, 4).Value = item.Specification ?? "-";
                ws.Cell(currentRow, 5).Value = item.SaleUnit ?? "件";
                ws.Cell(currentRow, 5).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

                ws.Cell(currentRow, 6).Value = item.CostPrice;
                ws.Cell(currentRow, 6).Style.NumberFormat.Format = "¥#,##0.00";

                ws.Cell(currentRow, 7).Value = item.Quantity;
                ws.Cell(currentRow, 7).Style.NumberFormat.Format = "#,##0";

                ws.Cell(currentRow, 8).Value = item.Subtotal;
                ws.Cell(currentRow, 8).Style.NumberFormat.Format = "¥#,##0.00";

                ws.Row(currentRow).Height = 20;
                currentRow++;
            }

            // 5. 合计行
            ws.Cell(currentRow, 1).Value = "合计";
            ws.Range(currentRow, 1, currentRow, 6).Merge().Style
                .Font.SetBold(true)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            ws.Cell(currentRow, 7).Value = order.TotalQuantity;
            ws.Cell(currentRow, 7).Style.Font.SetBold(true).NumberFormat.Format = "#,##0";

            ws.Cell(currentRow, 8).Value = order.TotalAmount;
            ws.Cell(currentRow, 8).Style.Font.SetBold(true).NumberFormat.Format = "¥#,##0.00";

            var dataRange = ws.Range(startRow, 1, currentRow, 8);
            dataRange.Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin);
            dataRange.Style.Border.SetInsideBorder(XLBorderStyleValues.Thin);

            // 6. 底部签字区
            currentRow += 2;
            ws.Cell(currentRow, 2).Value = "采购人签名: ______________";
            ws.Cell(currentRow, 6).Value = "供货商确认: ______________";
            ws.Range(currentRow, 1, currentRow, 8).Style.Font.SetFontSize(10);

            // 自动调整列宽
            ws.Columns().AdjustToContents();
            ws.Column(1).Width = 8;
            ws.Column(2).Width = 18;
            ws.Column(3).Width = 32;
            ws.Column(6).Width = 16;
            ws.Column(7).Width = 14;
            ws.Column(8).Width = 16;

            var dir = Path.GetDirectoryName(targetFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            workbook.SaveAs(targetFilePath);
        });

        return targetFilePath;
    }
}
