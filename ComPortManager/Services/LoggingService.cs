using System.IO;
using System.Text;
using ComPortManager.Models;

namespace ComPortManager.Services;

/// <summary>
/// File-based logging service.
/// </summary>
public class LoggingService : ILoggingService
{
    private readonly List<LogEntry> _entries = [];
    private readonly string _logDirectory;
    private readonly object _lock = new();
    private readonly int _maxInMemoryEntries = 1000;

    public IReadOnlyList<LogEntry> Entries
    {
        get
        {
            lock (_lock)
            {
                return _entries.ToList();
            }
        }
    }

    public event EventHandler<LogEntry>? EntryAdded;

    public LoggingService()
    {
        _logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ComPortManager",
            "Logs");

        Directory.CreateDirectory(_logDirectory);

        // Clean up old logs
        CleanupOldLogs(30);
    }

    public void Log(LogEntry entry)
    {
        lock (_lock)
        {
            _entries.Add(entry);

            // Trim old entries if needed
            if (_entries.Count > _maxInMemoryEntries)
            {
                _entries.RemoveRange(0, _entries.Count - _maxInMemoryEntries);
            }
        }

        // Write to file asynchronously
        WriteToFileAsync(entry).ConfigureAwait(false);

        // Raise event
        EntryAdded?.Invoke(this, entry);
    }

    public void Clear()
    {
        lock (_lock)
        {
            _entries.Clear();
        }
    }

    public string GetCurrentLogFilePath()
    {
        return Path.Combine(_logDirectory, $"ComPortManager_{DateTime.Now:yyyy-MM-dd}.log");
    }

    public async Task ExportAsync(string filePath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("COM Port Manager Log Export");
        sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine(new string('-', 80));
        sb.AppendLine();

        List<LogEntry> snapshot;
        lock (_lock)
        {
            snapshot = _entries.ToList();
        }

        foreach (var entry in snapshot)
        {
            sb.AppendLine(FormatEntry(entry));
        }

        await File.WriteAllTextAsync(filePath, sb.ToString());
    }

    private async Task WriteToFileAsync(LogEntry entry)
    {
        try
        {
            var logPath = GetCurrentLogFilePath();
            var line = FormatEntry(entry) + Environment.NewLine;
            await File.AppendAllTextAsync(logPath, line);
        }
        catch
        {
            // Ignore file write errors to prevent cascading failures
        }
    }

    private static string FormatEntry(LogEntry entry)
    {
        var level = entry.Level.ToString().ToUpperInvariant().PadRight(7);
        var category = entry.Category != LogCategory.General
            ? $"[{entry.Category}] "
            : string.Empty;

        var message = entry.Message;
        if (!string.IsNullOrEmpty(entry.OldComPort) && !string.IsNullOrEmpty(entry.NewComPort))
        {
            message = $"{entry.DeviceName}: {entry.OldComPort} -> {entry.NewComPort}";
            if (!string.IsNullOrEmpty(entry.RuleName))
                message += $" (Rule: {entry.RuleName})";
        }

        var line = $"{entry.FormattedTimestamp} [{level}] {category}{message}";

        if (!string.IsNullOrEmpty(entry.Details))
        {
            line += Environment.NewLine + "    " + entry.Details.Replace(Environment.NewLine, Environment.NewLine + "    ");
        }

        return line;
    }

    private void CleanupOldLogs(int retentionDays)
    {
        try
        {
            var cutoff = DateTime.Now.AddDays(-retentionDays);
            var files = Directory.GetFiles(_logDirectory, "*.log");

            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);
                if (fileInfo.LastWriteTime < cutoff)
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch
                    {
                        // Ignore individual file deletion errors
                    }
                }
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}
