using WanKePos.Infrastructure.Services;
using Xunit;

namespace WanKePos.Tests.UpdateTests;

public class SemVerComparisonTests
{
    [Theory]
    // 基础版本比较
    [InlineData("0.2.1", "0.2.2", true)]
    [InlineData("0.2.1", "0.3.0", true)]
    [InlineData("0.2.1", "1.0.0", true)]
    [InlineData("0.2.2", "0.2.1", false)]
    [InlineData("0.2.1", "0.2.1", false)]
    // 带 v 前缀
    [InlineData("v0.2.1", "v0.2.2", true)]
    [InlineData("v0.2.2", "v0.2.1", false)]
    // 跨主/次版本与预览版：核心版本高者胜
    [InlineData("0.2.1", "0.2.2-pre", true)]
    [InlineData("0.2.1", "0.3.0-preview", true)]
    [InlineData("0.2.2", "0.2.1-pre", false)]
    // 相同核心版本下：正式版高于预览版
    [InlineData("0.2.1-pre", "0.2.1", true)]
    [InlineData("0.2.1", "0.2.1-pre", false)]
    // 预览版序列递增
    [InlineData("0.2.2-pre1", "0.2.2-pre2", true)]
    [InlineData("0.2.2-pre2", "0.2.2-pre1", false)]
    [InlineData("0.2.2-preview1", "0.2.2-preview2", true)]
    public void IsNewerVersion_ShouldCorrectlyCompareVersions(string current, string remote, bool expected)
    {
        var result = UpdateService.IsNewerVersion(current, remote);
        Assert.Equal(expected, result);
    }
}
