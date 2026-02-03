namespace StickyTerm.Services;

/// <summary>
/// Service for running an optional HTTP API server.
/// Allows AI coding assistants to query COM port information.
/// </summary>
public interface IApiServerService : IDisposable
{
    /// <summary>
    /// Gets whether the server is currently running.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Gets the URL the server is listening on.
    /// </summary>
    string? ListeningUrl { get; }

    /// <summary>
    /// Starts the API server.
    /// </summary>
    Task StartAsync();

    /// <summary>
    /// Stops the API server.
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// Sets the callback to rescan devices before API requests.
    /// </summary>
    void SetRescanCallback(Func<Task> rescanCallback);

    /// <summary>
    /// Raised when the server status changes.
    /// </summary>
    event EventHandler<bool>? StatusChanged;
}
