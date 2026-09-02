using ClosedXML.Excel;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WanKePos.Domain.Entities;
using WanKePos.Domain.Enums;

namespace WanKePos.Infrastructure.Import
{
    public class ExcelImporter
    {
        private static string GetCellString(IXLRow row, int column)
        {
            var cell = row.Cell(column);
            return cell.Value.ToString()?.Trim() ?? string.Empty;
        }

        public Task<List<Product>> ImportProductsAsync(string filePath)
        {
            return Task.Run(() =>
            {
                var products = new List<Product>();
                using var workbook = new XLWorkbook(filePath);
                var worksheet = workbook.Worksheet(1);
                var rows = worksheet.RowsUsed();
                
                bool isFirstRow = true;
                foreach (var row in rows)
                {
                    if (isFirstRow)
                    {
                        isFirstRow = false;
                        continue;
                    }

                    var barcode = GetCellString(row, 2);
                    var name = GetCellString(row, 3);
                    if (string.IsNullOrWhiteSpace(barcode) && string.IsNullOrWhiteSpace(name))
                        continue;

                    var product = new Product
                    {
                        ProductType = GetCellString(row, 1),
                        Barcode = barcode,
                        Name = name,
                        SaleMethod = GetCellString(row, 7),
                        SystemCategory = GetCellString(row, 8),
                        StoreCategory = GetCellString(row, 9),
                        ArticleNumber = GetCellString(row, 12),
                        ImageUrl = GetCellString(row, 14),
                        Brand = GetCellString(row, 15),
                        SaleUnit = GetCellString(row, 19),
                        Specification = GetCellString(row, 20) + " " + GetCellString(row, 21),
                        IsPointsEligible = GetCellString(row, 27) == "是",
                        ShelfStatus = GetCellString(row, 28),
                        Supplier = GetCellString(row, 32)
                    };

                    if (decimal.TryParse(GetCellString(row, 4), out var stock)) product.Stock = stock;
                    if (decimal.TryParse(GetCellString(row, 5), out var retailPrice)) product.RetailPrice = retailPrice;
                    if (decimal.TryParse(GetCellString(row, 6), out var costPrice)) product.CostPrice = costPrice;
                    if (decimal.TryParse(GetCellString(row, 13), out var memberPrice)) product.MemberPrice = memberPrice;

                    if (!string.IsNullOrWhiteSpace(product.Barcode))
                    {
                        products.Add(product);
                    }
                }
                return products;
            });
        }

        public Task<List<Member>> ImportMembersAsync(string filePath)
        {
            return Task.Run(() =>
            {
                var members = new List<Member>();
                using var workbook = new XLWorkbook(filePath);
                var worksheet = workbook.Worksheet(1);
                var rows = worksheet.RowsUsed();
                
                bool isFirstRow = true;
                foreach (var row in rows)
                {
                    if (isFirstRow)
                    {
                        isFirstRow = false;
                        continue;
                    }

                    var memberNo = GetCellString(row, 1);
                    var phone = GetCellString(row, 2);
                    if (string.IsNullOrWhiteSpace(memberNo) && string.IsNullOrWhiteSpace(phone))
                        continue;

                    var member = new Member
                    {
                        MemberNo = memberNo,
                        Phone = phone,
                        Name = GetCellString(row, 3),
                        Gender = GetCellString(row, 4),
                        StoreName = GetCellString(row, 7),
                        Bpin = GetCellString(row, 8),
                        Status = GetCellString(row, 12),
                        Identity = GetCellString(row, 13),
                        Address = GetCellString(row, 18),
                        GuidePin = GetCellString(row, 19),
                        GuideName = GetCellString(row, 20)
                    };

                    if (DateTime.TryParse(GetCellString(row, 5), out var birthday)) member.Birthday = birthday;
                    if (DateTime.TryParse(GetCellString(row, 6), out var registerTime)) member.RegisterTime = registerTime;
                    if (decimal.TryParse(GetCellString(row, 9), out var points)) member.TotalPoints = points;
                    if (decimal.TryParse(GetCellString(row, 10), out var balance)) member.Balance = balance;
                    if (decimal.TryParse(GetCellString(row, 11), out var totalSpent)) member.TotalSpent = totalSpent;

                    if (!string.IsNullOrWhiteSpace(member.MemberNo))
                    {
                        members.Add(member);
                    }
                }
                return members;
            });
        }
    }
}
