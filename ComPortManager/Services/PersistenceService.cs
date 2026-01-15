using System.IO;
using System.Text.Json;
using ComPortManager.Models;

namespace ComPortManager.Services;

/// <summary>
/// JSON file-based persistence service.
/// </summary>
public class PersistenceService : IPersistenceService
{
    private readonly string _dataDirectory;
    private readonly string _rulesPath;
    private readonly string _settingsPath;
    private readonly JsonSerializerOptions _jsonOptions;

    public string DataDirectory => _dataDirectory;

    public PersistenceService()
    {
        _dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ComPortManager");

        Directory.CreateDirectory(_dataDirectory);

        _rulesPath = Path.Combine(_dataDirectory, "Rules.json");
        _settingsPath = Path.Combine(_dataDirectory, "Settings.json");

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };
    }

    public async Task<IReadOnlyList<PortRule>> LoadRulesAsync()
    {
        if (!File.Exists(_rulesPath))
            return Array.Empty<PortRule>();

        try
        {
            var json = await File.ReadAllTextAsync(_rulesPath);
            var rules = JsonSerializer.Deserialize<List<PortRule>>(json, _jsonOptions);
            return rules ?? new List<PortRule>();
        }
        catch (Exception)
        {
            // Return empty list on error, will be overwritten on save
            return Array.Empty<PortRule>();
        }
    }

    public async Task SaveRulesAsync(IEnumerable<PortRule> rules)
    {
        var json = JsonSerializer.Serialize(rules.ToList(), _jsonOptions);
        await File.WriteAllTextAsync(_rulesPath, json);
    }

    public async Task<AppSettings> LoadSettingsAsync()
    {
        if (!File.Exists(_settingsPath))
            return AppSettings.CreateDefault();

        try
        {
            var json = await File.ReadAllTextAsync(_settingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions);
            return settings ?? AppSettings.CreateDefault();
        }
        catch (Exception)
        {
            return AppSettings.CreateDefault();
        }
    }

    public async Task SaveSettingsAsync(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, _jsonOptions);
        await File.WriteAllTextAsync(_settingsPath, json);
    }

    public async Task ExportRulesAsync(string filePath, IEnumerable<PortRule> rules)
    {
        var export = new
        {
            ExportDate = DateTime.Now,
            Version = "1.0",
            Rules = rules.ToList()
        };

        var json = JsonSerializer.Serialize(export, _jsonOptions);
        await File.WriteAllTextAsync(filePath, json);
    }

    public async Task<IReadOnlyList<PortRule>> ImportRulesAsync(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Import file not found", filePath);

        var json = await File.ReadAllTextAsync(filePath);
        var doc = JsonDocument.Parse(json);

        // Handle both direct array and wrapped format
        if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            return JsonSerializer.Deserialize<List<PortRule>>(json, _jsonOptions) ?? new List<PortRule>();
        }

        if (doc.RootElement.TryGetProperty("rules", out var rulesElement) ||
            doc.RootElement.TryGetProperty("Rules", out rulesElement))
        {
            var rulesJson = rulesElement.GetRawText();
            return JsonSerializer.Deserialize<List<PortRule>>(rulesJson, _jsonOptions) ?? new List<PortRule>();
        }

        throw new InvalidDataException("Invalid import file format");
    }
}
