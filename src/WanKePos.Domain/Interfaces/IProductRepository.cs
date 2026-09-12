using System.Collections.Generic;
using System.Threading.Tasks;
using WanKePos.Domain.Entities;

namespace WanKePos.Domain.Interfaces;

public interface IProductRepository
{
    Task<List<Product>> GetAllAsync();
    Task<Product?> GetByIdAsync(int id);
    Task<Product?> GetByBarcodeAsync(string barcode);
    Task<List<Product>> SearchAsync(string keyword);
    Task<List<Product>> GetByCategoryAsync(string category);
    Task<List<string>> GetCategoriesAsync();
    Task AddOrUpdateAsync(Product product);
    Task UpdateStockAsync(int productId, decimal quantityChange);
    Task BatchUpdateStockAsync(Dictionary<int, decimal> stockChanges);
    Task<int> ImportFromListAsync(List<Product> products);
    Task DeleteAsync(int productId);
}
