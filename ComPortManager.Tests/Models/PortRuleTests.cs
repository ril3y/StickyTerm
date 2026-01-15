using ComPortManager.Models;

namespace ComPortManager.Tests.Models;

public class PortRuleTests
{
    #region TargetPortNumber Tests

    [Theory]
    [InlineData("COM5", 5)]
    [InlineData("COM15", 15)]
    [InlineData("COM100", 100)]
    [InlineData("com5", 5)]
    [InlineData("", 0)]
    [InlineData("COMX", 0)]
    public void TargetPortNumber_ParsesCorrectly(string comPort, int expected)
    {
        var rule = new PortRule { TargetComPort = comPort };
        Assert.Equal(expected, rule.TargetPortNumber);
    }

    #endregion

    #region VidPidSerial Matching Tests

    [Fact]
    public void Matches_VidPidSerial_ExactMatch_ReturnsTrue()
    {
        var rule = new PortRule
        {
            Enabled = true,
            MatchType = RuleMatchType.VidPidSerial,
            Vid = "0403",
            Pid = "6001",
            SerialNumber = "A12345"
        };

        var device = new ComDevice
        {
            Vid = "0403",
            Pid = "6001",
            SerialNumber = "A12345"
        };

        Assert.True(rule.Matches(device));
    }

    [Fact]
    public void Matches_VidPidSerial_CaseInsensitive_ReturnsTrue()
    {
        var rule = new PortRule
        {
            Enabled = true,
            MatchType = RuleMatchType.VidPidSerial,
            Vid = "0403",
            Pid = "6001",
            SerialNumber = "a12345"
        };

        var device = new ComDevice
        {
            Vid = "0403",
            Pid = "6001",
            SerialNumber = "A12345"
        };

        Assert.True(rule.Matches(device));
    }

    [Fact]
    public void Matches_VidPidSerial_DifferentSerial_ReturnsFalse()
    {
        var rule = new PortRule
        {
            Enabled = true,
            MatchType = RuleMatchType.VidPidSerial,
            Vid = "0403",
            Pid = "6001",
            SerialNumber = "A12345"
        };

        var device = new ComDevice
        {
            Vid = "0403",
            Pid = "6001",
            SerialNumber = "B67890"
        };

        Assert.False(rule.Matches(device));
    }

    [Fact]
    public void Matches_VidPidSerial_MissingRuleSerial_ReturnsFalse()
    {
        var rule = new PortRule
        {
            Enabled = true,
            MatchType = RuleMatchType.VidPidSerial,
            Vid = "0403",
            Pid = "6001",
            SerialNumber = ""
        };

        var device = new ComDevice
        {
            Vid = "0403",
            Pid = "6001",
            SerialNumber = "A12345"
        };

        Assert.False(rule.Matches(device));
    }

    #endregion

    #region VidPid Matching Tests

    [Fact]
    public void Matches_VidPid_ExactMatch_ReturnsTrue()
    {
        var rule = new PortRule
        {
            Enabled = true,
            MatchType = RuleMatchType.VidPid,
            Vid = "0403",
            Pid = "6001"
        };

        var device = new ComDevice
        {
            Vid = "0403",
            Pid = "6001"
        };

        Assert.True(rule.Matches(device));
    }

    [Fact]
    public void Matches_VidPid_IgnoresSerial_ReturnsTrue()
    {
        var rule = new PortRule
        {
            Enabled = true,
            MatchType = RuleMatchType.VidPid,
            Vid = "0403",
            Pid = "6001"
        };

        var device = new ComDevice
        {
            Vid = "0403",
            Pid = "6001",
            SerialNumber = "A12345"
        };

        Assert.True(rule.Matches(device));
    }

    [Fact]
    public void Matches_VidPid_DifferentVid_ReturnsFalse()
    {
        var rule = new PortRule
        {
            Enabled = true,
            MatchType = RuleMatchType.VidPid,
            Vid = "0403",
            Pid = "6001"
        };

        var device = new ComDevice
        {
            Vid = "10C4",
            Pid = "6001"
        };

        Assert.False(rule.Matches(device));
    }

    [Fact]
    public void Matches_VidPid_DifferentPid_ReturnsFalse()
    {
        var rule = new PortRule
        {
            Enabled = true,
            MatchType = RuleMatchType.VidPid,
            Vid = "0403",
            Pid = "6001"
        };

        var device = new ComDevice
        {
            Vid = "0403",
            Pid = "6015"
        };

        Assert.False(rule.Matches(device));
    }

    #endregion

    #region InstanceId Matching Tests

    [Fact]
    public void Matches_InstanceId_ExactMatch_ReturnsTrue()
    {
        var rule = new PortRule
        {
            Enabled = true,
            MatchType = RuleMatchType.InstanceId,
            InstanceIdPattern = "USB\\VID_0403&PID_6001\\A12345"
        };

        var device = new ComDevice
        {
            PnpDeviceId = "USB\\VID_0403&PID_6001\\A12345"
        };

        Assert.True(rule.Matches(device));
    }

    [Fact]
    public void Matches_InstanceId_WildcardEnd_ReturnsTrue()
    {
        var rule = new PortRule
        {
            Enabled = true,
            MatchType = RuleMatchType.InstanceId,
            InstanceIdPattern = "USB\\VID_0403&PID_6001*"
        };

        var device = new ComDevice
        {
            PnpDeviceId = "USB\\VID_0403&PID_6001\\A12345"
        };

        Assert.True(rule.Matches(device));
    }

    [Fact]
    public void Matches_InstanceId_WildcardMiddle_ReturnsTrue()
    {
        var rule = new PortRule
        {
            Enabled = true,
            MatchType = RuleMatchType.InstanceId,
            InstanceIdPattern = "USB\\VID_0403*A12345"
        };

        var device = new ComDevice
        {
            PnpDeviceId = "USB\\VID_0403&PID_6001\\A12345"
        };

        Assert.True(rule.Matches(device));
    }

    [Fact]
    public void Matches_InstanceId_NoMatch_ReturnsFalse()
    {
        var rule = new PortRule
        {
            Enabled = true,
            MatchType = RuleMatchType.InstanceId,
            InstanceIdPattern = "USB\\VID_10C4*"
        };

        var device = new ComDevice
        {
            PnpDeviceId = "USB\\VID_0403&PID_6001\\A12345"
        };

        Assert.False(rule.Matches(device));
    }

    #endregion

    #region FriendlyName Matching Tests

    [Fact]
    public void Matches_FriendlyName_Contains_ReturnsTrue()
    {
        var rule = new PortRule
        {
            Enabled = true,
            MatchType = RuleMatchType.FriendlyName,
            FriendlyNamePattern = "FTDI"
        };

        var device = new ComDevice
        {
            FriendlyName = "USB Serial Port (FTDI)"
        };

        Assert.True(rule.Matches(device));
    }

    [Fact]
    public void Matches_FriendlyName_Wildcard_ReturnsTrue()
    {
        var rule = new PortRule
        {
            Enabled = true,
            MatchType = RuleMatchType.FriendlyName,
            FriendlyNamePattern = "*Serial*"
        };

        var device = new ComDevice
        {
            FriendlyName = "USB Serial Port"
        };

        Assert.True(rule.Matches(device));
    }

    [Fact]
    public void Matches_FriendlyName_CaseInsensitive_ReturnsTrue()
    {
        var rule = new PortRule
        {
            Enabled = true,
            MatchType = RuleMatchType.FriendlyName,
            FriendlyNamePattern = "ftdi"
        };

        var device = new ComDevice
        {
            FriendlyName = "FTDI USB Serial Port"
        };

        Assert.True(rule.Matches(device));
    }

    [Fact]
    public void Matches_FriendlyName_NoMatch_ReturnsFalse()
    {
        var rule = new PortRule
        {
            Enabled = true,
            MatchType = RuleMatchType.FriendlyName,
            FriendlyNamePattern = "Prolific"
        };

        var device = new ComDevice
        {
            FriendlyName = "FTDI USB Serial Port"
        };

        Assert.False(rule.Matches(device));
    }

    #endregion

    #region Disabled Rule Tests

    [Fact]
    public void Matches_DisabledRule_ReturnsFalse()
    {
        var rule = new PortRule
        {
            Enabled = false,
            MatchType = RuleMatchType.VidPidSerial,
            Vid = "0403",
            Pid = "6001",
            SerialNumber = "A12345"
        };

        var device = new ComDevice
        {
            Vid = "0403",
            Pid = "6001",
            SerialNumber = "A12345"
        };

        Assert.False(rule.Matches(device));
    }

    #endregion

    #region MatchDescription Tests

    [Fact]
    public void MatchDescription_VidPidSerial_FormatsCorrectly()
    {
        var rule = new PortRule
        {
            MatchType = RuleMatchType.VidPidSerial,
            Vid = "0403",
            Pid = "6001",
            SerialNumber = "A12345"
        };

        Assert.Equal("VID:0403 PID:6001 Serial:A12345", rule.MatchDescription);
    }

    [Fact]
    public void MatchDescription_VidPid_FormatsCorrectly()
    {
        var rule = new PortRule
        {
            MatchType = RuleMatchType.VidPid,
            Vid = "0403",
            Pid = "6001"
        };

        Assert.Equal("VID:0403 PID:6001", rule.MatchDescription);
    }

    [Fact]
    public void MatchDescription_InstanceId_FormatsCorrectly()
    {
        var rule = new PortRule
        {
            MatchType = RuleMatchType.InstanceId,
            InstanceIdPattern = "USB\\VID_0403*"
        };

        Assert.Equal("Instance: USB\\VID_0403*", rule.MatchDescription);
    }

    [Fact]
    public void MatchDescription_FriendlyName_FormatsCorrectly()
    {
        var rule = new PortRule
        {
            MatchType = RuleMatchType.FriendlyName,
            FriendlyNamePattern = "*Serial*"
        };

        Assert.Equal("Name: *Serial*", rule.MatchDescription);
    }

    #endregion

    #region FromDevice Tests

    [Fact]
    public void FromDevice_WithSerial_CreatesVidPidSerialRule()
    {
        var device = new ComDevice
        {
            FriendlyName = "USB Serial Port",
            Vid = "0403",
            Pid = "6001",
            SerialNumber = "A12345"
        };

        var rule = PortRule.FromDevice(device, "COM10");

        Assert.Equal(RuleMatchType.VidPidSerial, rule.MatchType);
        Assert.Equal("0403", rule.Vid);
        Assert.Equal("6001", rule.Pid);
        Assert.Equal("A12345", rule.SerialNumber);
        Assert.Equal("COM10", rule.TargetComPort);
        Assert.Equal(100, rule.Priority);
    }

    [Fact]
    public void FromDevice_WithoutSerial_CreatesVidPidRule()
    {
        var device = new ComDevice
        {
            FriendlyName = "USB Serial Port",
            Vid = "0403",
            Pid = "6001",
            SerialNumber = ""
        };

        var rule = PortRule.FromDevice(device, "COM10");

        Assert.Equal(RuleMatchType.VidPid, rule.MatchType);
        Assert.Equal("0403", rule.Vid);
        Assert.Equal("6001", rule.Pid);
        Assert.Equal(50, rule.Priority);
    }

    #endregion

    #region Clone Tests

    [Fact]
    public void Clone_CreatesIdenticalCopy()
    {
        var original = new PortRule
        {
            Id = "test-id",
            Name = "Test Rule",
            Enabled = true,
            MatchType = RuleMatchType.VidPidSerial,
            Vid = "0403",
            Pid = "6001",
            SerialNumber = "A12345",
            TargetComPort = "COM10",
            Priority = 100,
            Notes = "Test notes",
            ApplyCount = 5
        };

        var clone = original.Clone();

        Assert.Equal(original.Id, clone.Id);
        Assert.Equal(original.Name, clone.Name);
        Assert.Equal(original.Enabled, clone.Enabled);
        Assert.Equal(original.MatchType, clone.MatchType);
        Assert.Equal(original.Vid, clone.Vid);
        Assert.Equal(original.Pid, clone.Pid);
        Assert.Equal(original.SerialNumber, clone.SerialNumber);
        Assert.Equal(original.TargetComPort, clone.TargetComPort);
        Assert.Equal(original.Priority, clone.Priority);
        Assert.Equal(original.Notes, clone.Notes);
        Assert.Equal(original.ApplyCount, clone.ApplyCount);
    }

    #endregion
}
