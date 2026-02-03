using StickyTerm.Models;

namespace StickyTerm.Services;

/// <summary>
/// Service for application logging.
/// </summary>
public interface ILoggingService
{
    /// <summary>
    /// Gets all log entries.
    /// </summary>
    IReadOnlyList<LogEntry> Entries { get; }

    /// <summary>
    /// Logs an entry.
    /// </summary>
    void Log(LogEntry entry);

    /// <summary>
    /// Clears all log entries.
    /// </summary>
    void Clear();

    /// <summary>
    /// Exports logs to a file.
    /// </summary>
    Task ExportAsync(string filePath);

    /// <summary>
    /// Gets the log file path for today.
    /// </summary>
    string GetCurrentLogFilePath();

    /// <summary>
    /// Event raised when a new entry is logged.
    /// </summary>
    event EventHandler<LogEntry>? EntryAdded;
}
