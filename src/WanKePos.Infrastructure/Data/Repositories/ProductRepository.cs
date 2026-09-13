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
            return await _context.Products.AsNoTracking().ToListAsync();
        }

        public async Task<Product?> GetByIdAsync(int id)
        {
            return await _context.Products.FindAsync(id);
        }

        public async Task<Product?> GetByBarcodeAsync(string barcode)
        {
            return await _context.Products.FirstOrDefaultAsync(p => p.Barcode == barcode);
        }

        public async Task<List<Product>> SearchAsync(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return await _context.Products.AsNoTracking().ToListAsync();

            var lower = keyword.ToLower();
            return await _context.Products
                .AsNoTracking()
                .Where(p => p.Barcode.ToLower().Contains(lower) || p.Name.ToLower().Contains(lower))
                .ToListAsync();
        }

        public async Task<List<Product>> GetByCategoryAsync(string category)
        {
            if (string.IsNullOrWhiteSpace(category) || category == WanKePos.Domain.CategoryConstants.All)
                return await _context.Products.AsNoTracking().ToListAsync();

            return await _context.Products
                .AsNoTracking()
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
            Product? existing = null;
            if (product.Id > 0)
            {
                existing = await _context.Products.FindAsync(product.Id);
            }
            if (existing == null && !string.IsNullOrEmpty(product.Barcode))
            {
                existing = await _context.Products.FirstOrDefaultAsync(p => p.Barcode == product.Barcode);
            }

            if (existing != null)
            {
                var existingId = existing.Id;
                _context.Entry(existing).CurrentValues.SetValues(product);
                existing.Id = existingId; // preserve PK
            }
            else
            {
                await _context.Products.AddAsync(product);
            }
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Product product)
        {
            var existing = await _context.Products.FindAsync(product.Id);
            if (existing == null)
            {
                throw new InvalidOperationException($"未找到 ID 为 {product.Id} 的商品。");
            }

            if (!string.IsNullOrWhiteSpace(product.Barcode))
            {
                var barcodeConflict = await _context.Products.AnyAsync(p => p.Barcode == product.Barcode && p.Id != product.Id);
                if (barcodeConflict)
                {
                    throw new InvalidOperationException($"条码【{product.Barcode}】已被其他商品使用！");
                }
            }

            if (existing != product)
            {
                _context.Entry(existing).CurrentValues.SetValues(product);
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                throw new InvalidOperationException("保存商品修改失败，可能存在重复的条码或数据冲突。", ex);
            }
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

        public async Task BatchUpdateStockAsync(Dictionary<int, decimal> stockChanges)
        {
            var productIds = stockChanges.Keys.ToList();
            var products = await _context.Products
                .Where(p => productIds.Contains(p.Id))
                .ToListAsync();

            foreach (var product in products)
            {
                if (stockChanges.TryGetValue(product.Id, out var change))
                {
                    product.Stock += change;
                }
            }
            await _context.SaveChangesAsync();
        }

        public async Task<int> ImportFromListAsync(List<Product> products)
        {
            var barcodes = products.Where(p => !string.IsNullOrWhiteSpace(p.Barcode)).Select(p => p.Barcode).ToList();
            var existingDict = await _context.Products
                .Where(p => barcodes.Contains(p.Barcode))
                .ToDictionaryAsync(p => p.Barcode);

            foreach (var product in products)
            {
                if (existingDict.TryGetValue(product.Barcode, out var existing))
                {
                    var existingId = existing.Id;
                    _context.Entry(existing).CurrentValues.SetValues(product);
                    existing.Id = existingId;
                }
                else
                {
                    await _context.Products.AddAsync(product);
                }
            }
            await _context.SaveChangesAsync();
            return products.Count;
        }

        public async Task DeleteAsync(int productId)
        {
            var product = await _context.Products.FindAsync(productId);
            if (product != null)
            {
                var hasOrderItems = await _context.OrderItems.AnyAsync(oi => oi.ProductId == productId);
                if (hasOrderItems)
                {
                    throw new InvalidOperationException("该商品存在历史销售订单记录，不可直接删除！建议将状态设为下架。");
                }

                var hasPurchaseItems = await _context.PurchaseOrderItems.AnyAsync(poi => poi.ProductId == productId);
                if (hasPurchaseItems)
                {
                    throw new InvalidOperationException("该商品存在历史采购单据记录，不可直接删除！");
                }

                _context.Products.Remove(product);
                await _context.SaveChangesAsync();
            }
        }
    }
}
