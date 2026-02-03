using StickyTerm.Models;

namespace StickyTerm.Tests.Models;

public class ApplyResultTests
{
    [Fact]
    public void Succeeded_CreatesSuccessResult()
    {
        var device = new ComDevice { FriendlyName = "Test Device" };
        var rule = new PortRule { Name = "Test Rule" };

        var result = ApplyResult.Succeeded(device, rule, "COM5", "COM10");

        Assert.True(result.Success);
        Assert.Equal("COM5", result.OldComPort);
        Assert.Equal("COM10", result.NewComPort);
        Assert.True(result.RequiresRestart);
        Assert.Same(device, result.Device);
        Assert.Same(rule, result.Rule);
    }

    [Fact]
    public void Succeeded_WithoutRestart_SetsFlag()
    {
        var device = new ComDevice { FriendlyName = "Test Device" };
        var rule = new PortRule { Name = "Test Rule" };

        var result = ApplyResult.Succeeded(device, rule, "COM5", "COM10", requiresRestart: false);

        Assert.False(result.RequiresRestart);
    }

    [Fact]
    public void Failed_CreatesFailureResult()
    {
        var device = new ComDevice { FriendlyName = "Test Device" };
        var rule = new PortRule { Name = "Test Rule" };
        var exception = new Exception("Test error");

        var result = ApplyResult.Failed(device, rule, "Failed message", exception);

        Assert.False(result.Success);
        Assert.Equal("Failed message", result.Message);
        Assert.Same(exception, result.Exception);
    }

    [Fact]
    public void NoChange_CreatesNoChangeResult()
    {
        var device = new ComDevice { FriendlyName = "Test Device", ComPort = "COM10" };
        var rule = new PortRule { Name = "Test Rule", TargetComPort = "COM10" };

        var result = ApplyResult.NoChange(device, rule);

        Assert.True(result.Success);
        Assert.False(result.RequiresRestart);
        Assert.Equal("COM10", result.OldComPort);
        Assert.Equal("COM10", result.NewComPort);
        Assert.Contains("already on", result.Message);
    }

    [Fact]
    public void Conflict_CreatesConflictResult()
    {
        var device = new ComDevice { FriendlyName = "Test Device" };
        var rule = new PortRule { Name = "Test Rule", TargetComPort = "COM10" };

        var result = ApplyResult.Conflict(device, rule, "Other Device");

        Assert.False(result.Success);
        Assert.Contains("COM10", result.Message);
        Assert.Contains("Other Device", result.Message);
    }

    [Fact]
    public void DryRun_CreatesDryRunResult()
    {
        var device = new ComDevice { FriendlyName = "Test Device" };
        var rule = new PortRule { Name = "Test Rule" };

        var result = ApplyResult.DryRun(device, rule, "COM5", "COM10");

        Assert.True(result.Success);
        Assert.False(result.RequiresRestart);
        Assert.Contains("DRY RUN", result.Message);
        Assert.Equal("COM5", result.OldComPort);
        Assert.Equal("COM10", result.NewComPort);
    }
}
