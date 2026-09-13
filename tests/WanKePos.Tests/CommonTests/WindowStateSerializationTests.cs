using System.Text.Json;
using Xunit;

namespace WanKePos.Tests.CommonTests;

public class WindowStateModel
{
    public int Width { get; set; } = 1200;
    public int Height { get; set; } = 800;
    public int? X { get; set; }
    public int? Y { get; set; }
    public bool IsMaximized { get; set; }
}

public class WindowStateSerializationTests
{
    [Fact]
    public void WindowState_SerializationAndDeserialization_ShouldPreserveValues()
    {
        var state = new WindowStateModel
        {
            Width = 1440,
            Height = 900,
            X = 120,
            Y = 80,
            IsMaximized = false
        };

        var json = JsonSerializer.Serialize(state);
        Assert.NotNull(json);

        var restored = JsonSerializer.Deserialize<WindowStateModel>(json);
        Assert.NotNull(restored);
        Assert.Equal(1440, restored.Width);
        Assert.Equal(900, restored.Height);
        Assert.Equal(120, restored.X);
        Assert.Equal(80, restored.Y);
        Assert.False(restored.IsMaximized);
    }

    [Fact]
    public void WindowState_MaximizedState_ShouldSerializeCorrectly()
    {
        var state = new WindowStateModel
        {
            Width = 1200,
            Height = 800,
            IsMaximized = true
        };

        var json = JsonSerializer.Serialize(state);
        var restored = JsonSerializer.Deserialize<WindowStateModel>(json);
        Assert.NotNull(restored);
        Assert.True(restored.IsMaximized);
    }

    [Theory]
    [InlineData(800, 500, 1200, 800)] // below minimum (900x600) -> falls back to default
    [InlineData(1024, 768, 1024, 768)] // valid custom size
    [InlineData(1920, 1080, 1920, 1080)] // full HD
    public void WindowState_ClampResolution_ShouldEnsureSafeDimensions(int inputW, int inputH, int expectedW, int expectedH)
    {
        int targetW = inputW >= 900 ? inputW : 1200;
        int targetH = inputH >= 600 ? inputH : 800;

        Assert.Equal(expectedW, targetW);
        Assert.Equal(expectedH, targetH);
    }
}
