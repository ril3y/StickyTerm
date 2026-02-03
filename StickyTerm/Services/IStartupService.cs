namespace StickyTerm.Services;

/// <summary>
/// Service for managing Windows startup behavior using Task Scheduler.
/// </summary>
public interface IStartupService
{
    /// <summary>
    /// Gets whether startup with Windows is currently enabled.
    /// </summary>
    bool IsStartupEnabled { get; }

    /// <summary>
    /// Enables startup with Windows using Task Scheduler with elevated privileges.
    /// Requires admin privileges to create the task.
    /// </summary>
    bool EnableStartup();

    /// <summary>
    /// Disables startup with Windows by removing the scheduled task.
    /// </summary>
    bool DisableStartup();
}
