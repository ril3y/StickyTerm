using StickyTerm.Models;
using StickyTerm.Services;

namespace StickyTerm.Tests.Services;

public class LoggingServiceTests
{
    private readonly LoggingService _service;

    public LoggingServiceTests()
    {
        _service = new LoggingService();
    }

    #region Log Tests

    [Fact]
    public void Log_AddsEntryToList()
    {
        var entry = LogEntry.Info("Test message");

        _service.Log(entry);

        Assert.Single(_service.Entries);
        Assert.Equal("Test message", _service.Entries[0].Message);
    }

    [Fact]
    public void Log_MultipleEntries_MaintainsOrder()
    {
        _service.Log(LogEntry.Info("First"));
        _service.Log(LogEntry.Info("Second"));
        _service.Log(LogEntry.Info("Third"));

        Assert.Equal(3, _service.Entries.Count);
        Assert.Equal("First", _service.Entries[0].Message);
        Assert.Equal("Second", _service.Entries[1].Message);
        Assert.Equal("Third", _service.Entries[2].Message);
    }

    [Fact]
    public void Log_RaisesEntryAddedEvent()
    {
        LogEntry? receivedEntry = null;
        _service.EntryAdded += (s, e) => receivedEntry = e;

        var entry = LogEntry.Info("Test message");
        _service.Log(entry);

        Assert.NotNull(receivedEntry);
        Assert.Equal("Test message", receivedEntry.Message);
    }

    #endregion

    #region Clear Tests

    [Fact]
    public void Clear_RemovesAllEntries()
    {
        _service.Log(LogEntry.Info("Entry 1"));
        _service.Log(LogEntry.Info("Entry 2"));
        _service.Log(LogEntry.Info("Entry 3"));

        _service.Clear();

        Assert.Empty(_service.Entries);
    }

    #endregion

    #region GetCurrentLogFilePath Tests

    [Fact]
    public void GetCurrentLogFilePath_ContainsDate()
    {
        var path = _service.GetCurrentLogFilePath();
        var expectedDate = DateTime.Now.ToString("yyyy-MM-dd");

        Assert.Contains(expectedDate, path);
    }

    [Fact]
    public void GetCurrentLogFilePath_EndsWithLog()
    {
        var path = _service.GetCurrentLogFilePath();

        Assert.EndsWith(".log", path);
    }

    [Fact]
    public void GetCurrentLogFilePath_ContainsStickyTerm()
    {
        var path = _service.GetCurrentLogFilePath();

        Assert.Contains("StickyTerm", path);
    }

    #endregion

    #region Entries Thread Safety Tests

    [Fact]
    public void Entries_ReturnsSnapshot()
    {
        _service.Log(LogEntry.Info("Entry 1"));

        var snapshot = _service.Entries;
        _service.Log(LogEntry.Info("Entry 2"));

        // Snapshot should not include the new entry
        Assert.Single(snapshot);
    }

    #endregion
}

public class LogEntryTests
{
    #region Factory Method Tests

    [Fact]
    public void Info_CreatesInfoLevel()
    {
        var entry = LogEntry.Info("Test message");

        Assert.Equal(LogLevel.Info, entry.Level);
        Assert.Equal("Test message", entry.Message);
        Assert.True(entry.Success);
    }

    [Fact]
    public void Info_WithDetails_IncludesDetails()
    {
        var entry = LogEntry.Info("Test message", "Details here");

        Assert.Equal("Details here", entry.Details);
    }

    [Fact]
    public void Warning_CreatesWarningLevel()
    {
        var entry = LogEntry.Warning("Warning message");

        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal("Warning message", entry.Message);
    }

    [Fact]
    public void Error_CreatesErrorLevel()
    {
        var entry = LogEntry.Error("Error message");

        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Equal("Error message", entry.Message);
        Assert.False(entry.Success);
    }

    [Fact]
    public void PortChange_CreatesPortChangeEntry()
    {
        var entry = LogEntry.PortChange("USB Serial Port", "COM5", "COM10", "Test Rule");

        Assert.Equal(LogLevel.Info, entry.Level);
        Assert.Equal(LogCategory.PortChange, entry.Category);
        Assert.Equal("USB Serial Port", entry.DeviceName);
        Assert.Equal("COM5", entry.OldComPort);
        Assert.Equal("COM10", entry.NewComPort);
        Assert.Equal("Test Rule", entry.RuleName);
    }

    [Fact]
    public void DeviceArrival_CreatesDeviceEventEntry()
    {
        var entry = LogEntry.DeviceArrival("USB Serial Port", "COM5");

        Assert.Equal(LogLevel.Info, entry.Level);
        Assert.Equal(LogCategory.DeviceEvent, entry.Category);
        Assert.Equal("USB Serial Port", entry.DeviceName);
        Assert.Equal("COM5", entry.NewComPort);
    }

    [Fact]
    public void DeviceRemoval_CreatesDeviceEventEntry()
    {
        var entry = LogEntry.DeviceRemoval("USB Serial Port", "COM5");

        Assert.Equal(LogLevel.Info, entry.Level);
        Assert.Equal(LogCategory.DeviceEvent, entry.Category);
        Assert.Equal("USB Serial Port", entry.DeviceName);
        Assert.Equal("COM5", entry.OldComPort);
    }

    #endregion

    #region Property Tests

    [Fact]
    public void FormattedTimestamp_CorrectFormat()
    {
        var entry = new LogEntry { Timestamp = new DateTime(2024, 6, 15, 14, 30, 45, 123) };

        Assert.Equal("2024-06-15 14:30:45.123", entry.FormattedTimestamp);
    }

    [Fact]
    public void Summary_WithPortChange_ShowsChange()
    {
        var entry = new LogEntry
        {
            DeviceName = "USB Serial Port",
            OldComPort = "COM5",
            NewComPort = "COM10"
        };

        Assert.Equal("USB Serial Port: COM5 -> COM10", entry.Summary);
    }

    [Fact]
    public void Summary_WithoutPortChange_ShowsMessage()
    {
        var entry = new LogEntry { Message = "Test message" };

        Assert.Equal("Test message", entry.Summary);
    }

    #endregion
}
