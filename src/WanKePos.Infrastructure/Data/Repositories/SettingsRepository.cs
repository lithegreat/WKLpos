using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using WanKePos.Domain.Interfaces;
using WanKePos.Domain.Entities;
using WanKePos.Domain.Enums;

namespace WanKePos.Infrastructure.Data.Repositories
{
    public class SettingsRepository : ISettingsRepository
    {
        private readonly PosDbContext _context;

        public SettingsRepository(PosDbContext context)
        {
            _context = context;
        }

        public async Task<StoreSettings> GetSettingsAsync()
        {
            var settings = await _context.StoreSettings.FirstOrDefaultAsync();
            if (settings == null)
            {
                settings = new StoreSettings { StoreName = "默认门店" };
                await _context.StoreSettings.AddAsync(settings);
                await _context.SaveChangesAsync();
            }
            return settings;
        }

        public async Task SaveSettingsAsync(StoreSettings settings)
        {
            var existing = await _context.StoreSettings.FirstOrDefaultAsync(s => s.Id == settings.Id);
            if (existing != null)
            {
                existing.StoreName = settings.StoreName;
                existing.StoreAddress = settings.StoreAddress;
                existing.StorePhone = settings.StorePhone;
                existing.ReceiptHeader = settings.ReceiptHeader;
                existing.ReceiptFooter = settings.ReceiptFooter;
                _context.StoreSettings.Update(existing);
            }
            else
            {
                await _context.StoreSettings.AddAsync(settings);
            }
            await _context.SaveChangesAsync();
        }
    }
}
