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

    [Fact]
    public async Task UpdateAsync_ShouldUpdateMemberDetails_Successfully()
    {
        var (context, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        try
        {
            var repo = new MemberRepository(context);
            var member = new Member
            {
                MemberNo = "M1003",
                Name = "赵六",
                Phone = "13700137000",
                TotalPoints = 10,
                Balance = 100.00m,
                Status = "正常"
            };

            await repo.AddOrUpdateAsync(member);
            var fetched = await repo.GetByPhoneAsync("13700137000");
            Assert.NotNull(fetched);

            fetched.Name = "赵六六";
            fetched.Phone = "13700137999";
            fetched.Status = "禁用";
            fetched.Balance = 250.50m;
            fetched.TotalPoints = 120;

            await repo.UpdateAsync(fetched);

            var updated = await repo.GetByIdAsync(fetched.Id);
            Assert.NotNull(updated);
            Assert.Equal("赵六六", updated.Name);
            Assert.Equal("13700137999", updated.Phone);
            Assert.Equal("禁用", updated.Status);
            Assert.Equal(250.50m, updated.Balance);
            Assert.Equal(120, updated.TotalPoints);
        }
        finally
        {
            connection.Dispose();
        }
    }

    [Fact]
    public async Task UpdateAsync_DuplicatePhone_ShouldThrowInvalidOperationException()
    {
        var (context, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        try
        {
            var repo = new MemberRepository(context);
            var m1 = new Member { MemberNo = "M2001", Name = "会员甲", Phone = "13600000001" };
            var m2 = new Member { MemberNo = "M2002", Name = "会员乙", Phone = "13600000002" };
            await repo.AddOrUpdateAsync(m1);
            await repo.AddOrUpdateAsync(m2);

            var fetchedM2 = await repo.GetByPhoneAsync("13600000002");
            Assert.NotNull(fetchedM2);
            fetchedM2.Phone = "13600000001"; // Duplicate with m1

            await Assert.ThrowsAsync<System.InvalidOperationException>(() => repo.UpdateAsync(fetchedM2));
        }
        finally
        {
            connection.Dispose();
        }
    }
}
