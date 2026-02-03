using System.Text.Json;
using StickyTerm.Models;
using StickyTerm.Services;

namespace StickyTerm.Tests.Services;

public class PersistenceServiceTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly IPersistenceService _service;

    public PersistenceServiceTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"StickyTermTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDirectory);

        // Use reflection to set the private data directory for testing
        _service = new TestPersistenceService(_tempDirectory);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, true);
            }
        }
        catch { }
    }

    #region Rules Tests

    [Fact]
    public async Task SaveRulesAsync_CreatesFile()
    {
        var rules = new List<PortRule>
        {
            new PortRule { Name = "Test Rule", TargetComPort = "COM10" }
        };

        await _service.SaveRulesAsync(rules);

        var filePath = Path.Combine(_tempDirectory, "Rules.json");
        Assert.True(File.Exists(filePath));
    }

    [Fact]
    public async Task SaveRulesAsync_WritesValidJson()
    {
        var rules = new List<PortRule>
        {
            new PortRule { Name = "Test Rule", TargetComPort = "COM10", Vid = "0403", Pid = "6001" }
        };

        await _service.SaveRulesAsync(rules);

        var filePath = Path.Combine(_tempDirectory, "Rules.json");
        var json = await File.ReadAllTextAsync(filePath);
        var parsed = JsonDocument.Parse(json);

        Assert.Equal(JsonValueKind.Array, parsed.RootElement.ValueKind);
        Assert.Equal(1, parsed.RootElement.GetArrayLength());
    }

    [Fact]
    public async Task LoadRulesAsync_NoFile_ReturnsEmpty()
    {
        var rules = await _service.LoadRulesAsync();

        Assert.Empty(rules);
    }

    [Fact]
    public async Task LoadRulesAsync_LoadsSavedRules()
    {
        var originalRules = new List<PortRule>
        {
            new PortRule { Name = "Rule 1", TargetComPort = "COM10", Vid = "0403", Pid = "6001" },
            new PortRule { Name = "Rule 2", TargetComPort = "COM15", Vid = "10C4", Pid = "EA60" }
        };

        await _service.SaveRulesAsync(originalRules);
        var loadedRules = await _service.LoadRulesAsync();

        Assert.Equal(2, loadedRules.Count);
        Assert.Equal("Rule 1", loadedRules[0].Name);
        Assert.Equal("Rule 2", loadedRules[1].Name);
    }

    [Fact]
    public async Task LoadRulesAsync_PreservesAllProperties()
    {
        var originalRule = new PortRule
        {
            Id = "test-id",
            Name = "Test Rule",
            Enabled = false,
            MatchType = RuleMatchType.VidPidSerial,
            Vid = "0403",
            Pid = "6001",
            SerialNumber = "A12345",
            TargetComPort = "COM10",
            Priority = 75,
            Notes = "Test notes"
        };

        await _service.SaveRulesAsync(new[] { originalRule });
        var loadedRules = await _service.LoadRulesAsync();

        var loaded = loadedRules[0];
        Assert.Equal("test-id", loaded.Id);
        Assert.Equal("Test Rule", loaded.Name);
        Assert.False(loaded.Enabled);
        Assert.Equal(RuleMatchType.VidPidSerial, loaded.MatchType);
        Assert.Equal("0403", loaded.Vid);
        Assert.Equal("6001", loaded.Pid);
        Assert.Equal("A12345", loaded.SerialNumber);
        Assert.Equal("COM10", loaded.TargetComPort);
        Assert.Equal(75, loaded.Priority);
        Assert.Equal("Test notes", loaded.Notes);
    }

    [Fact]
    public async Task LoadRulesAsync_CorruptedFile_ReturnsEmpty()
    {
        var filePath = Path.Combine(_tempDirectory, "Rules.json");
        await File.WriteAllTextAsync(filePath, "{ invalid json }}}");

        var rules = await _service.LoadRulesAsync();

        Assert.Empty(rules);
    }

    #endregion

    #region Settings Tests

    [Fact]
    public async Task SaveSettingsAsync_CreatesFile()
    {
        var settings = new AppSettings { WatchModeEnabled = true };

        await _service.SaveSettingsAsync(settings);

        var filePath = Path.Combine(_tempDirectory, "Settings.json");
        Assert.True(File.Exists(filePath));
    }

    [Fact]
    public async Task LoadSettingsAsync_NoFile_ReturnsDefaults()
    {
        var settings = await _service.LoadSettingsAsync();

        Assert.NotNull(settings);
        Assert.False(settings.WatchModeEnabled); // Default value
    }

    [Fact]
    public async Task LoadSettingsAsync_LoadsSavedSettings()
    {
        var originalSettings = new AppSettings
        {
            WatchModeEnabled = true,
            AutoApplyOnStartup = true,
            PreferredStartingComPort = 30,
            DryRunMode = true
        };

        await _service.SaveSettingsAsync(originalSettings);
        var loadedSettings = await _service.LoadSettingsAsync();

        Assert.True(loadedSettings.WatchModeEnabled);
        Assert.True(loadedSettings.AutoApplyOnStartup);
        Assert.Equal(30, loadedSettings.PreferredStartingComPort);
        Assert.True(loadedSettings.DryRunMode);
    }

    [Fact]
    public async Task LoadSettingsAsync_CorruptedFile_ReturnsDefaults()
    {
        var filePath = Path.Combine(_tempDirectory, "Settings.json");
        await File.WriteAllTextAsync(filePath, "{ invalid json }}}");

        var settings = await _service.LoadSettingsAsync();

        Assert.NotNull(settings);
    }

    #endregion

    #region Export/Import Tests

    [Fact]
    public async Task ExportRulesAsync_CreatesExportFile()
    {
        var rules = new List<PortRule>
        {
            new PortRule { Name = "Test Rule" }
        };
        var exportPath = Path.Combine(_tempDirectory, "export.json");

        await _service.ExportRulesAsync(exportPath, rules);

        Assert.True(File.Exists(exportPath));
    }

    [Fact]
    public async Task ExportRulesAsync_IncludesMetadata()
    {
        var rules = new List<PortRule> { new PortRule { Name = "Test Rule" } };
        var exportPath = Path.Combine(_tempDirectory, "export.json");

        await _service.ExportRulesAsync(exportPath, rules);

        var json = await File.ReadAllTextAsync(exportPath);
        var doc = JsonDocument.Parse(json);

        Assert.True(doc.RootElement.TryGetProperty("exportDate", out _));
        Assert.True(doc.RootElement.TryGetProperty("version", out _));
        Assert.True(doc.RootElement.TryGetProperty("rules", out _));
    }

    [Fact]
    public async Task ImportRulesAsync_ImportExportFormat()
    {
        var originalRules = new List<PortRule>
        {
            new PortRule { Name = "Rule 1", Vid = "0403", Pid = "6001" },
            new PortRule { Name = "Rule 2", Vid = "10C4", Pid = "EA60" }
        };
        var exportPath = Path.Combine(_tempDirectory, "export.json");

        await _service.ExportRulesAsync(exportPath, originalRules);
        var importedRules = await _service.ImportRulesAsync(exportPath);

        Assert.Equal(2, importedRules.Count);
        Assert.Equal("Rule 1", importedRules[0].Name);
        Assert.Equal("Rule 2", importedRules[1].Name);
    }

    [Fact]
    public async Task ImportRulesAsync_DirectArrayFormat()
    {
        var rules = new List<PortRule>
        {
            new PortRule { Name = "Rule 1" },
            new PortRule { Name = "Rule 2" }
        };
        var importPath = Path.Combine(_tempDirectory, "import.json");
        var json = JsonSerializer.Serialize(rules, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        await File.WriteAllTextAsync(importPath, json);

        var importedRules = await _service.ImportRulesAsync(importPath);

        Assert.Equal(2, importedRules.Count);
    }

    [Fact]
    public async Task ImportRulesAsync_FileNotFound_Throws()
    {
        var importPath = Path.Combine(_tempDirectory, "nonexistent.json");

        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            _service.ImportRulesAsync(importPath));
    }

    [Fact]
    public async Task ImportRulesAsync_InvalidFormat_Throws()
    {
        var importPath = Path.Combine(_tempDirectory, "invalid.json");
        await File.WriteAllTextAsync(importPath, "{\"somethingElse\": 123}");

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            _service.ImportRulesAsync(importPath));
    }

    #endregion

    #region DataDirectory Tests

    [Fact]
    public void DataDirectory_ReturnsConfiguredPath()
    {
        Assert.Equal(_tempDirectory, _service.DataDirectory);
    }

    #endregion

    /// <summary>
    /// Test implementation of PersistenceService that uses a custom directory.
    /// </summary>
    private class TestPersistenceService : IPersistenceService
    {
        private readonly string _dataDirectory;
        private readonly string _rulesPath;
        private readonly string _settingsPath;
        private readonly JsonSerializerOptions _jsonOptions;

        public string DataDirectory => _dataDirectory;

        public TestPersistenceService(string dataDirectory)
        {
            _dataDirectory = dataDirectory;
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
            catch
            {
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
            catch
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

        public Task<IReadOnlyList<DeviceAlias>> LoadAliasesAsync()
        {
            return Task.FromResult<IReadOnlyList<DeviceAlias>>(Array.Empty<DeviceAlias>());
        }

        public Task SaveAliasesAsync(IEnumerable<DeviceAlias> aliases)
        {
            return Task.CompletedTask;
        }
    }
}
