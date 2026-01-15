using ComPortManager.Models;

namespace ComPortManager.Services;

/// <summary>
/// Service for persisting application data.
/// </summary>
public interface IPersistenceService
{
    /// <summary>
    /// Gets the application data directory.
    /// </summary>
    string DataDirectory { get; }

    /// <summary>
    /// Loads rules from storage.
    /// </summary>
    Task<IReadOnlyList<PortRule>> LoadRulesAsync();

    /// <summary>
    /// Saves rules to storage.
    /// </summary>
    Task SaveRulesAsync(IEnumerable<PortRule> rules);

    /// <summary>
    /// Loads application settings.
    /// </summary>
    Task<AppSettings> LoadSettingsAsync();

    /// <summary>
    /// Saves application settings.
    /// </summary>
    Task SaveSettingsAsync(AppSettings settings);

    /// <summary>
    /// Exports rules to a file.
    /// </summary>
    Task ExportRulesAsync(string filePath, IEnumerable<PortRule> rules);

    /// <summary>
    /// Imports rules from a file.
    /// </summary>
    Task<IReadOnlyList<PortRule>> ImportRulesAsync(string filePath);
}
