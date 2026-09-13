using System;
using System.Collections.Generic;
using WanKePos.Domain.Entities;
using WanKePos.Domain.Enums;
using Xunit;

namespace WanKePos.Tests.EntityTests;

public class MemberEntityTests
{
    [Fact]
    public void Member_PropertyChanged_FiresCorrectlyForProperties()
    {
        var member = new Member();
        var changedProperties = new List<string>();
        member.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName != null)
                changedProperties.Add(e.PropertyName);
        };

        member.Name = "张三";
        member.Phone = "13812345678";
        member.Balance = 100.50m;
        member.TotalPoints = 50;
        member.Status = "正常";
        member.Identity = "黄金会员";
        member.Gender = "先生";
        member.StoreName = "总店";
        member.SyncStatus = SyncStatus.Synced;

        Assert.Contains(nameof(Member.Name), changedProperties);
        Assert.Contains(nameof(Member.Phone), changedProperties);
        Assert.Contains(nameof(Member.Balance), changedProperties);
        Assert.Contains(nameof(Member.TotalPoints), changedProperties);
        Assert.Contains(nameof(Member.Status), changedProperties);
        Assert.Contains(nameof(Member.Identity), changedProperties);
        Assert.Contains(nameof(Member.Gender), changedProperties);
        Assert.Contains(nameof(Member.StoreName), changedProperties);
        Assert.Contains(nameof(Member.SyncStatus), changedProperties);
    }

    [Fact]
    public void Member_PropertyChanged_DoesNotFireWhenSameValue()
    {
        var member = new Member { Name = "李四", Balance = 200m };
        var changedProperties = new List<string>();
        member.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName != null)
                changedProperties.Add(e.PropertyName);
        };

        // 设置相同的值
        member.Name = "李四";
        member.Balance = 200m;

        Assert.Empty(changedProperties);
    }
}
