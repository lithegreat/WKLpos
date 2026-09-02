using System.Collections.Generic;
using System.Threading.Tasks;
using WanKePos.Domain.Entities;

namespace WanKePos.Domain.Interfaces;

public interface IMemberRepository
{
    Task<List<Member>> GetAllAsync();
    Task<Member?> GetByPhoneAsync(string phone);
    Task<Member?> GetByMemberNoAsync(string memberNo);
    Task<List<Member>> SearchAsync(string keyword);
    Task AddOrUpdateAsync(Member member);
    Task UpdatePointsAsync(int memberId, decimal pointsChange);
    Task UpdateBalanceAsync(int memberId, decimal balanceChange);
    Task<int> ImportFromListAsync(List<Member> members);
}
