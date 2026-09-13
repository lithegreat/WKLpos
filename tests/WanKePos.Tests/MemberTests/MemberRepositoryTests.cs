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

    [Fact]
    public async Task UpdateAsync_DuplicateMemberNo_ShouldThrowInvalidOperationException()
    {
        var (context, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        try
        {
            var repo = new MemberRepository(context);
            var m1 = new Member { MemberNo = "M3001", Name = "会员A", Phone = "13500000001" };
            var m2 = new Member { MemberNo = "M3002", Name = "会员B", Phone = "13500000002" };
            await repo.AddOrUpdateAsync(m1);
            await repo.AddOrUpdateAsync(m2);

            var fetchedM2 = await repo.GetByPhoneAsync("13500000002");
            Assert.NotNull(fetchedM2);
            fetchedM2.MemberNo = "M3001"; // Duplicate card number

            await Assert.ThrowsAsync<System.InvalidOperationException>(() => repo.UpdateAsync(fetchedM2));
        }
        finally
        {
            connection.Dispose();
        }
    }

    [Fact]
    public async Task UpdateAsync_NonExistentMember_ShouldThrowInvalidOperationException()
    {
        var (context, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        try
        {
            var repo = new MemberRepository(context);
            var ghostMember = new Member { Id = 99999, MemberNo = "M9999", Name = "不存在", Phone = "13999999999" };
            await Assert.ThrowsAsync<System.InvalidOperationException>(() => repo.UpdateAsync(ghostMember));
        }
        finally
        {
            connection.Dispose();
        }
    }

    [Fact]
    public async Task UpdateAsync_PreservingSamePhoneAndMemberNo_ShouldSucceed()
    {
        var (context, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        try
        {
            var repo = new MemberRepository(context);
            var member = new Member { MemberNo = "M4001", Name = "原名", Phone = "13400000001", Balance = 50 };
            await repo.AddOrUpdateAsync(member);

            var fetched = await repo.GetByPhoneAsync("13400000001");
            Assert.NotNull(fetched);
            fetched.Name = "修改后的新名";
            fetched.Balance = 150;

            await repo.UpdateAsync(fetched);

            var updated = await repo.GetByIdAsync(fetched.Id);
            Assert.NotNull(updated);
            Assert.Equal("修改后的新名", updated.Name);
            Assert.Equal(150, updated.Balance);
        }
        finally
        {
            connection.Dispose();
        }
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveMemberSuccessfully()
    {
        var (context, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        try
        {
            var repo = new MemberRepository(context);
            var member = new Member { MemberNo = "M5001", Name = "待删除会员", Phone = "13300000001" };
            await repo.AddOrUpdateAsync(member);

            var fetched = await repo.GetByPhoneAsync("13300000001");
            Assert.NotNull(fetched);

            await repo.DeleteAsync(fetched.Id);

            var afterDelete = await repo.GetByIdAsync(fetched.Id);
            Assert.Null(afterDelete);
        }
        finally
        {
            connection.Dispose();
        }
    }

    [Fact]
    public async Task SearchAsync_ShouldMatchPhoneNameOrMemberNo()
    {
        var (context, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        try
        {
            var repo = new MemberRepository(context);
            await repo.AddOrUpdateAsync(new Member { MemberNo = "HY8801", Name = "陈大文", Phone = "18800001111" });
            await repo.AddOrUpdateAsync(new Member { MemberNo = "HY8802", Name = "李晓明", Phone = "18800002222" });
            await repo.AddOrUpdateAsync(new Member { MemberNo = "HY9901", Name = "王晓红", Phone = "18900003333" });

            // 搜索手机号片段
            var phoneMatches = await repo.SearchAsync("2222");
            Assert.Single(phoneMatches);
            Assert.Equal("李晓明", phoneMatches[0].Name);

            // 搜索姓名片段
            var nameMatches = await repo.SearchAsync("晓");
            Assert.Equal(2, nameMatches.Count);

            // 搜索卡号片段
            var noMatches = await repo.SearchAsync("HY88");
            Assert.Equal(2, noMatches.Count);
        }
        finally
        {
            connection.Dispose();
        }
    }

    [Fact]
    public async Task UpdateBalanceAsync_ShouldIncrementAndDecrement()
    {
        var (context, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        try
        {
            var repo = new MemberRepository(context);
            var member = new Member { MemberNo = "M6001", Name = "储值会员", Phone = "13200000001", Balance = 100m };
            await repo.AddOrUpdateAsync(member);

            // 充值 200
            await repo.UpdateBalanceAsync(member.Id, 200m);
            var afterRecharge = await repo.GetByIdAsync(member.Id);
            Assert.NotNull(afterRecharge);
            Assert.Equal(300m, afterRecharge.Balance);

            // 消费扣款 80
            await repo.UpdateBalanceAsync(member.Id, -80m);
            var afterSpend = await repo.GetByIdAsync(member.Id);
            Assert.NotNull(afterSpend);
            Assert.Equal(220m, afterSpend.Balance);
        }
        finally
        {
            connection.Dispose();
        }
    }

    [Fact]
    public async Task RecordConsumptionAsync_ShouldAccumulateSpentAndPoints()
    {
        var (context, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        try
        {
            var repo = new MemberRepository(context);
            var member = new Member { MemberNo = "M7001", Name = "消费积分会员", Phone = "13100000001", TotalSpent = 100m, TotalPoints = 10 };
            await repo.AddOrUpdateAsync(member);

            // 消费 200 元，获 20 积分
            await repo.RecordConsumptionAsync(member.Id, 200m, 20m);

            var updated = await repo.GetByIdAsync(member.Id);
            Assert.NotNull(updated);
            Assert.Equal(300m, updated.TotalSpent);
            Assert.Equal(30m, updated.TotalPoints);
        }
        finally
        {
            connection.Dispose();
        }
    }
}
