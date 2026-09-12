using System.Threading.Tasks;
using WanKePos.Domain.Entities;
using WanKePos.Infrastructure.Data.Repositories;
using WanKePos.Tests.TestHelpers;
using Xunit;

namespace WanKePos.Tests.MemberTests;

public class MemberRepositoryTests
{
    [Fact]
    public async Task CreateAndGetByPhone_ShouldReturnMemberWithPoints()
    {
        var (context, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        try
        {
            var repo = new MemberRepository(context);
            var member = new Member
            {
                MemberNo = "M1001",
                Name = "张晓华",
                Phone = "13800138000",
                TotalPoints = 100,
                Balance = 500.00m
            };

            await repo.AddOrUpdateAsync(member);

            var fetched = await repo.GetByPhoneAsync("13800138000");
            Assert.NotNull(fetched);
            Assert.Equal("张晓华", fetched.Name);
            Assert.Equal(100, fetched.TotalPoints);
            Assert.Equal(500.00m, fetched.Balance);
        }
        finally
        {
            connection.Dispose();
        }
    }

    [Fact]
    public async Task AddPointsAsync_ShouldAccumulatePoints()
    {
        var (context, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        try
        {
            var repo = new MemberRepository(context);
            var member = new Member
            {
                MemberNo = "M1002",
                Name = "李丽",
                Phone = "13900139000",
                TotalPoints = 50
            };

            await repo.AddOrUpdateAsync(member);
            await repo.UpdatePointsAsync(member.Id, 30);

            var updated = await repo.GetByIdAsync(member.Id);
            Assert.NotNull(updated);
            Assert.Equal(80, updated.TotalPoints);
        }
        finally
        {
            connection.Dispose();
        }
    }
}
