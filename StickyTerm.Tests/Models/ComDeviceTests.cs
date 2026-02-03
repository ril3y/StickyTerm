using StickyTerm.Models;

namespace StickyTerm.Tests.Models;

public class ComDeviceTests
{
    [Fact]
    public void DisplayName_WithComPort_IncludesBoth()
    {
        var device = new ComDevice
        {
            ComPort = "COM5",
            FriendlyName = "USB Serial Port"
        };

        Assert.Equal("COM5 - USB Serial Port", device.DisplayName);
    }

    [Fact]
    public void DisplayName_WithoutComPort_ReturnsFriendlyNameOnly()
    {
        var device = new ComDevice
        {
            ComPort = "",
            FriendlyName = "USB Serial Port"
        };

        Assert.Equal("USB Serial Port", device.DisplayName);
    }

    [Fact]
    public void UniqueIdentifier_WithVidPidSerial_ReturnsFullIdentifier()
    {
        var device = new ComDevice
        {
            Vid = "0403",
            Pid = "6001",
            SerialNumber = "A12345"
        };

        Assert.Equal("VID_0403&PID_6001&SN_A12345", device.UniqueIdentifier);
    }

    [Fact]
    public void UniqueIdentifier_WithoutSerial_ReturnsPnpDeviceId()
    {
        var device = new ComDevice
        {
            Vid = "0403",
            Pid = "6001",
            SerialNumber = "",
            PnpDeviceId = "USB\\VID_0403&PID_6001\\12345"
        };

        Assert.Equal("USB\\VID_0403&PID_6001\\12345", device.UniqueIdentifier);
    }

    [Fact]
    public void UniqueIdentifier_WithNothingUseful_ReturnsFriendlyName()
    {
        var device = new ComDevice
        {
            FriendlyName = "Some Device"
        };

        Assert.Equal("Some Device", device.UniqueIdentifier);
    }

    [Fact]
    public void Stability_WithSerialNumber_ReturnsStable()
    {
        var device = new ComDevice
        {
            SerialNumber = "ABC123"
        };

        Assert.Equal(DeviceStability.Stable, device.Stability);
    }

    [Fact]
    public void Stability_WithVidPidNoSerial_ReturnsUnstable()
    {
        var device = new ComDevice
        {
            Vid = "0403",
            Pid = "6001",
            SerialNumber = ""
        };

        Assert.Equal(DeviceStability.Unstable, device.Stability);
    }

    [Fact]
    public void Stability_WithNothing_ReturnsUnknown()
    {
        var device = new ComDevice();

        Assert.Equal(DeviceStability.Unknown, device.Stability);
    }

    [Fact]
    public void Clone_CreatesIdenticalCopy()
    {
        var original = new ComDevice
        {
            ComPort = "COM5",
            FriendlyName = "USB Serial Port",
            DeviceType = "FTDI",
            Vid = "0403",
            Pid = "6001",
            SerialNumber = "A12345",
            PnpDeviceId = "USB\\VID_0403&PID_6001\\A12345",
            Driver = "FTDIBUS",
            Manufacturer = "FTDI",
            InstancePath = "USB\\VID_0403&PID_6001\\A12345",
            DeviceClass = "Ports",
            IsPresent = true,
            HasSerialNumber = true,
            RegistryKeyPath = "HKLM\\SYSTEM\\CurrentControlSet\\Enum\\USB\\VID_0403&PID_6001\\A12345"
        };

        var clone = original.Clone();

        Assert.Equal(original.ComPort, clone.ComPort);
        Assert.Equal(original.FriendlyName, clone.FriendlyName);
        Assert.Equal(original.DeviceType, clone.DeviceType);
        Assert.Equal(original.Vid, clone.Vid);
        Assert.Equal(original.Pid, clone.Pid);
        Assert.Equal(original.SerialNumber, clone.SerialNumber);
        Assert.Equal(original.PnpDeviceId, clone.PnpDeviceId);
        Assert.Equal(original.Driver, clone.Driver);
        Assert.Equal(original.IsPresent, clone.IsPresent);
        Assert.Equal(original.HasSerialNumber, clone.HasSerialNumber);
    }

    [Fact]
    public void Clone_CreatesIndependentCopy()
    {
        var original = new ComDevice { ComPort = "COM5" };
        var clone = original.Clone();

        clone.ComPort = "COM10";

        Assert.Equal("COM5", original.ComPort);
        Assert.Equal("COM10", clone.ComPort);
    }

    [Theory]
    [InlineData("ABC123", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void HasSerialNumber_SetCorrectly(string? serial, bool expected)
    {
        var device = new ComDevice
        {
            SerialNumber = serial ?? "",
            HasSerialNumber = !string.IsNullOrEmpty(serial)
        };

        Assert.Equal(expected, device.HasSerialNumber);
    }
}
