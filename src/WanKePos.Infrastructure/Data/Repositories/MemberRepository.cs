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
            return await _context.Members.ToListAsync();
        }

        public async Task<Member?> GetByPhoneAsync(string phone)
        {
            return await _context.Members.FirstOrDefaultAsync(m => m.Phone == phone);
        }

        public async Task<Member?> GetByMemberNoAsync(string memberNo)
        {
            return await _context.Members.FirstOrDefaultAsync(m => m.MemberNo == memberNo);
        }

        public async Task<List<Member>> SearchAsync(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return await _context.Members.ToListAsync();

            var lower = keyword.ToLower();
            return await _context.Members
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
                existing.Phone = member.Phone;
                existing.Name = member.Name;
                existing.Gender = member.Gender;
                existing.Birthday = member.Birthday;
                existing.StoreName = member.StoreName;
                existing.TotalPoints = member.TotalPoints;
                existing.Balance = member.Balance;
                existing.TotalSpent = member.TotalSpent;
                existing.Status = member.Status;
                existing.Identity = member.Identity;
                existing.Address = member.Address;
                existing.GuidePin = member.GuidePin;
                existing.GuideName = member.GuideName;
                existing.LastModified = DateTime.Now;
            }
            else
            {
                member.CreatedAt = DateTime.Now;
                member.LastModified = DateTime.Now;
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

        public async Task<int> ImportFromListAsync(List<Member> members)
        {
            int count = 0;
            foreach (var member in members)
            {
                var existing = await _context.Members.FirstOrDefaultAsync(m => m.MemberNo == member.MemberNo);
                if (existing != null)
                {
                    existing.Phone = member.Phone;
                    existing.Name = member.Name;
                    existing.Gender = member.Gender;
                    existing.Birthday = member.Birthday;
                    existing.StoreName = member.StoreName;
                    existing.TotalPoints = member.TotalPoints;
                    existing.Balance = member.Balance;
                    existing.TotalSpent = member.TotalSpent;
                    existing.Status = member.Status;
                    existing.Identity = member.Identity;
                    existing.Address = member.Address;
                    existing.GuidePin = member.GuidePin;
                    existing.GuideName = member.GuideName;
                    existing.LastModified = DateTime.Now;
                }
                else
                {
                    member.CreatedAt = DateTime.Now;
                    member.LastModified = DateTime.Now;
                    await _context.Members.AddAsync(member);
                }
                count++;
            }
            await _context.SaveChangesAsync();
            return count;
        }
    }
}
