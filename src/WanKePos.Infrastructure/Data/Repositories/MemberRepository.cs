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
    /// 会员仓储实现
    /// </summary>
    public class MemberRepository : IMemberRepository
    {
        private readonly PosDbContext _context;

        public MemberRepository(PosDbContext context)
        {
            _context = context;
        }

        public async Task<List<Member>> GetAllAsync()
        {
            return await _context.Members.AsNoTracking().ToListAsync();
        }

        public async Task<Member?> GetByPhoneAsync(string phone)
        {
            return await _context.Members.AsNoTracking().FirstOrDefaultAsync(m => m.Phone == phone);
        }

        public async Task<Member?> GetByMemberNoAsync(string memberNo)
        {
            return await _context.Members.AsNoTracking().FirstOrDefaultAsync(m => m.MemberNo == memberNo);
        }

        public async Task<List<Member>> SearchAsync(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return await _context.Members.AsNoTracking().ToListAsync();

            var lower = keyword.ToLower();
            return await _context.Members
                .AsNoTracking()
                .Where(m => m.Phone.Contains(lower) ||
                            m.Name.ToLower().Contains(lower) ||
                            m.MemberNo.ToLower().Contains(lower))
                .ToListAsync();
        }

        public async Task AddOrUpdateAsync(Member member)
        {
            var existing = await _context.Members.FirstOrDefaultAsync(m => m.MemberNo == member.MemberNo);
            if (existing != null)
            {
                var existingId = existing.Id;
                _context.Entry(existing).CurrentValues.SetValues(member);
                existing.Id = existingId;
            }
            else
            {
                await _context.Members.AddAsync(member);
            }
            await _context.SaveChangesAsync();
        }

        public async Task UpdatePointsAsync(int memberId, decimal pointsChange)
        {
            var member = await _context.Members.FindAsync(memberId);
            if (member != null)
            {
                member.TotalPoints += pointsChange;
                member.LastModified = DateTime.Now;
                await _context.SaveChangesAsync();
            }
        }

        public async Task UpdateBalanceAsync(int memberId, decimal balanceChange)
        {
            var member = await _context.Members.FindAsync(memberId);
            if (member != null)
            {
                member.Balance += balanceChange;
                member.LastModified = DateTime.Now;
                await _context.SaveChangesAsync();
            }
        }

        public async Task RecordConsumptionAsync(int memberId, decimal spentAmount, decimal pointsChange)
        {
            var member = await _context.Members.FindAsync(memberId);
            if (member != null)
            {
                member.TotalSpent += spentAmount;
                member.TotalPoints += pointsChange;
                member.LastModified = DateTime.Now;
                await _context.SaveChangesAsync();
            }
        }

        public async Task<Member?> GetByIdAsync(int id)
        {
            return await _context.Members.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id);
        }

        public async Task DeleteAsync(int memberId)
        {
            var member = await _context.Members.FindAsync(memberId);
            if (member != null)
            {
                _context.Members.Remove(member);
                await _context.SaveChangesAsync();
            }
        }

        public async Task UpdateAsync(Member member)
        {
            var existing = await _context.Members.FindAsync(member.Id);
            if (existing == null)
            {
                throw new InvalidOperationException($"未找到 ID 为 {member.Id} 的会员。");
            }

            if (!string.IsNullOrWhiteSpace(member.Phone))
            {
                var phoneConflict = await _context.Members.AnyAsync(m => m.Phone == member.Phone && m.Id != member.Id);
                if (phoneConflict)
                {
                    throw new InvalidOperationException($"手机号【{member.Phone}】已被其他会员使用！");
                }
            }

            if (!string.IsNullOrWhiteSpace(member.MemberNo))
            {
                var noConflict = await _context.Members.AnyAsync(m => m.MemberNo == member.MemberNo && m.Id != member.Id);
                if (noConflict)
                {
                    throw new InvalidOperationException($"会员卡号【{member.MemberNo}】已被其他会员使用！");
                }
            }

            member.LastModified = DateTime.Now;
            if (existing != member)
            {
                _context.Entry(existing).CurrentValues.SetValues(member);
            }
            await _context.SaveChangesAsync();
        }

        public async Task<int> ImportFromListAsync(List<Member> members)
        {
            var memberNos = members.Where(m => !string.IsNullOrWhiteSpace(m.MemberNo)).Select(m => m.MemberNo).ToList();
            var existingDict = await _context.Members
                .Where(m => memberNos.Contains(m.MemberNo))
                .ToDictionaryAsync(m => m.MemberNo);

            foreach (var member in members)
            {
                if (existingDict.TryGetValue(member.MemberNo, out var existing))
                {
                    var existingId = existing.Id;
                    _context.Entry(existing).CurrentValues.SetValues(member);
                    existing.Id = existingId;
                }
                else
                {
                    await _context.Members.AddAsync(member);
                }
            }
            await _context.SaveChangesAsync();
            return members.Count;
        }
    }
}
