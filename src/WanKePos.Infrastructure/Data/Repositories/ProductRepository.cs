using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WanKePos.Domain.Interfaces;
using WanKePos.Domain.Entities;
using WanKePos.Domain.Enums;

namespace WanKePos.Infrastructure.Data.Repositories
{
    /// <summary>
    /// 商品仓储实现
    /// </summary>
    public class ProductRepository : IProductRepository
    {
        private readonly PosDbContext _context;

        public ProductRepository(PosDbContext context)
        {
            _context = context;
        }

        public async Task<List<Product>> GetAllAsync()
        {
            return await _context.Products.ToListAsync();
        }

        public async Task<Product?> GetByBarcodeAsync(string barcode)
        {
            return await _context.Products.FirstOrDefaultAsync(p => p.Barcode == barcode);
        }

        public async Task<List<Product>> SearchAsync(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return await _context.Products.ToListAsync();

            var lower = keyword.ToLower();
            return await _context.Products
                .Where(p => p.Barcode.ToLower().Contains(lower) || p.Name.ToLower().Contains(lower))
                .ToListAsync();
        }

        public async Task<List<Product>> GetByCategoryAsync(string category)
        {
            if (string.IsNullOrWhiteSpace(category) || category == "全部")
                return await _context.Products.ToListAsync();

            return await _context.Products
                .Where(p => p.StoreCategory == category)
                .ToListAsync();
        }

        public async Task<List<string>> GetCategoriesAsync()
        {
            return await _context.Products
                .Where(p => p.StoreCategory != null && p.StoreCategory != "")
                .Select(p => p.StoreCategory!)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();
        }

        public async Task AddOrUpdateAsync(Product product)
        {
            var existing = await _context.Products.FirstOrDefaultAsync(p => p.Barcode == product.Barcode);
            if (existing != null)
            {
                existing.Name = product.Name;
                existing.ProductType = product.ProductType;
                existing.Stock = product.Stock;
                existing.RetailPrice = product.RetailPrice;
                existing.CostPrice = product.CostPrice;
                existing.MemberPrice = product.MemberPrice;
                existing.SaleMethod = product.SaleMethod;
                existing.SystemCategory = product.SystemCategory;
                existing.StoreCategory = product.StoreCategory;
                existing.ArticleNumber = product.ArticleNumber;
                existing.Brand = product.Brand;
                existing.SaleUnit = product.SaleUnit;
                existing.Specification = product.Specification;
                existing.SpecUnit = product.SpecUnit;
                existing.IsPointsEligible = product.IsPointsEligible;
                existing.ShelfStatus = product.ShelfStatus;
                existing.Supplier = product.Supplier;
                existing.ImageUrl = product.ImageUrl;
                existing.LastModified = DateTime.Now;
            }
            else
            {
                product.CreatedAt = DateTime.Now;
                product.LastModified = DateTime.Now;
                await _context.Products.AddAsync(product);
            }
            await _context.SaveChangesAsync();
        }

        public async Task UpdateStockAsync(int productId, decimal quantityChange)
        {
            var product = await _context.Products.FindAsync(productId);
            if (product != null)
            {
                product.Stock += quantityChange;
                product.LastModified = DateTime.Now;
                await _context.SaveChangesAsync();
            }
        }

        public async Task<int> ImportFromListAsync(List<Product> products)
        {
            int count = 0;
            foreach (var product in products)
            {
                var existing = await _context.Products.FirstOrDefaultAsync(p => p.Barcode == product.Barcode);
                if (existing != null)
                {
                    existing.Name = product.Name;
                    existing.ProductType = product.ProductType;
                    existing.Stock = product.Stock;
                    existing.RetailPrice = product.RetailPrice;
                    existing.CostPrice = product.CostPrice;
                    existing.MemberPrice = product.MemberPrice;
                    existing.SaleMethod = product.SaleMethod;
                    existing.SystemCategory = product.SystemCategory;
                    existing.StoreCategory = product.StoreCategory;
                    existing.ArticleNumber = product.ArticleNumber;
                    existing.Brand = product.Brand;
                    existing.SaleUnit = product.SaleUnit;
                    existing.Specification = product.Specification;
                    existing.SpecUnit = product.SpecUnit;
                    existing.IsPointsEligible = product.IsPointsEligible;
                    existing.ShelfStatus = product.ShelfStatus;
                    existing.Supplier = product.Supplier;
                    existing.ImageUrl = product.ImageUrl;
                    existing.LastModified = DateTime.Now;
                }
                else
                {
                    product.CreatedAt = DateTime.Now;
                    product.LastModified = DateTime.Now;
                    await _context.Products.AddAsync(product);
                }
                count++;
            }
            await _context.SaveChangesAsync();
            return count;
        }
    }
}
