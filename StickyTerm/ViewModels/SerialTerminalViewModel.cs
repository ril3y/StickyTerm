using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StickyTerm.Helpers;
using StickyTerm.Models;
using StickyTerm.Services;

namespace StickyTerm.ViewModels;

/// <summary>
/// Event args for styled terminal content.
/// </summary>
public class TerminalContentEventArgs : EventArgs
{
    public IEnumerable<Run> Runs { get; }
    public bool IsNewLine { get; }

    public TerminalContentEventArgs(IEnumerable<Run> runs, bool isNewLine = false)
    {
        Runs = runs;
        IsNewLine = isNewLine;
    }
}

/// <summary>
/// ViewModel for the Serial Terminal tab.
/// </summary>
public partial class SerialTerminalViewModel : ObservableObject
{
    private readonly ISerialTerminalService _terminalService;
    private readonly ILoggingService _logger;
    private readonly AnsiParser _ansiParser = new();
    private readonly StringBuilder _terminalBuffer = new();
    private int _paragraphCount;

    /// <summary>
    /// Event raised when styled content should be appended to the terminal.
    /// </summary>
    public event EventHandler<TerminalContentEventArgs>? ContentAppended;

    /// <summary>
    /// Event raised when the terminal should be cleared.
    /// </summary>
    public event EventHandler? TerminalCleared;

    // Connection state
    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private string _selectedPort = string.Empty;

    [ObservableProperty]
    private ObservableCollection<string> _availablePorts = [];

    // Settings
    [ObservableProperty]
    private SerialTerminalSettings _settings = new();

    // Control line states
    [ObservableProperty]
    private bool _dtrState = true;

    [ObservableProperty]
    private bool _rtsState = true;

    [ObservableProperty]
    private bool _ctsState;

    [ObservableProperty]
    private bool _dsrState;

    [ObservableProperty]
    private bool _cdState;

    // Terminal data - kept for plain text copy functionality
    [ObservableProperty]
    private string _terminalOutput = string.Empty;

    [ObservableProperty]
    private string _inputText = string.Empty;

    [ObservableProperty]
    private bool _sendAsHex;

    // Command history
    private readonly List<string> _commandHistory = [];
    private int _historyIndex = -1;
    private string _savedInput = string.Empty;

    // Statistics
    [ObservableProperty]
    private long _bytesReceived;

    [ObservableProperty]
    private long _bytesSent;

    [ObservableProperty]
    private string _statusMessage = "Disconnected";

    // Options for binding
    public int[] AvailableBaudRates => SerialTerminalSettings.AvailableBaudRates;
    public int[] AvailableDataBits => SerialTerminalSettings.AvailableDataBits;
    public StopBits[] AvailableStopBits => [StopBits.One, StopBits.OnePointFive, StopBits.Two];
    public Parity[] AvailableParities => [Parity.None, Parity.Odd, Parity.Even, Parity.Mark, Parity.Space];
    public Handshake[] AvailableHandshakes => [Handshake.None, Handshake.XOnXOff, Handshake.RequestToSend, Handshake.RequestToSendXOnXOff];
    public LineEnding[] AvailableLineEndings => [LineEnding.None, LineEnding.CR, LineEnding.LF, LineEnding.CRLF];

    public SerialTerminalViewModel(ISerialTerminalService terminalService, ILoggingService logger)
    {
        _terminalService = terminalService;
        _logger = logger;

        // Subscribe to terminal events
        _terminalService.DataReceived += OnDataReceived;
        _terminalService.ErrorOccurred += OnErrorOccurred;
        _terminalService.Connected += OnConnected;
        _terminalService.Disconnected += OnDisconnected;
        _terminalService.ControlLineChanged += OnControlLineChanged;

        // Initial port refresh
        RefreshPorts();
    }

    [RelayCommand]
    private void RefreshPorts()
    {
        var currentPort = SelectedPort;
        AvailablePorts.Clear();

        foreach (var port in _terminalService.GetAvailablePorts())
        {
            AvailablePorts.Add(port);
        }

        // Restore selection if still available
        if (!string.IsNullOrEmpty(currentPort) && AvailablePorts.Contains(currentPort))
        {
            SelectedPort = currentPort;
        }
        else if (AvailablePorts.Count > 0)
        {
            SelectedPort = AvailablePorts[0];
        }
    }

    [RelayCommand]
    private async Task ToggleConnectionAsync()
    {
        if (IsConnected)
        {
            await DisconnectAsync();
        }
        else
        {
            await ConnectAsync();
        }
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        if (string.IsNullOrEmpty(SelectedPort))
        {
            StatusMessage = "Select a port first";
            return;
        }

        StatusMessage = $"Connecting to {SelectedPort}...";

        // Apply current settings to service
        Settings.DtrEnabled = DtrState;
        Settings.RtsEnabled = RtsState;

        var success = await _terminalService.ConnectAsync(SelectedPort, Settings);

        if (success)
        {
            StatusMessage = $"Connected to {SelectedPort} at {Settings.BaudRate} baud";
            _logger.Log(LogEntry.Info($"Terminal connected to {SelectedPort}"));
        }
        else
        {
            StatusMessage = $"Failed to connect to {SelectedPort}";
        }
    }

    [RelayCommand]
    private async Task DisconnectAsync()
    {
        await _terminalService.DisconnectAsync();
        StatusMessage = "Disconnected";
        _logger.Log(LogEntry.Info("Terminal disconnected"));
    }

    [RelayCommand]
    private async Task SendAsync()
    {
        if (!IsConnected)
        {
            StatusMessage = "Not connected";
            return;
        }

        if (string.IsNullOrEmpty(InputText))
            return;

        var textToSend = InputText;

        // Add to history
        if (_commandHistory.Count == 0 || _commandHistory[^1] != textToSend)
        {
            _commandHistory.Add(textToSend);
        }
        _historyIndex = _commandHistory.Count;

        if (SendAsHex)
        {
            try
            {
                await _terminalService.SendHexStringAsync(textToSend);

                // Count bytes for statistics
                var parts = textToSend.Replace("0x", " ").Split(' ', StringSplitOptions.RemoveEmptyEntries);
                BytesSent += parts.Length;

                if (Settings.LocalEcho)
                {
                    AppendToTerminal($"> [HEX] {textToSend}\n", isEcho: true);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Invalid hex: {ex.Message}";
                return;
            }
        }
        else
        {
            // Local echo
            if (Settings.LocalEcho)
            {
                AppendToTerminal($"> {textToSend}\n", isEcho: true);
            }

            await _terminalService.SendTextAsync(textToSend);
            BytesSent += Encoding.UTF8.GetByteCount(textToSend) +
                         Encoding.UTF8.GetByteCount(Settings.SendLineEnding.ToLineEndingString());
        }

        InputText = string.Empty;
    }

    [RelayCommand]
    private void HistoryUp()
    {
        if (_commandHistory.Count == 0)
            return;

        if (_historyIndex == _commandHistory.Count)
        {
            // Save current input before navigating
            _savedInput = InputText;
        }

        if (_historyIndex > 0)
        {
            _historyIndex--;
            InputText = _commandHistory[_historyIndex];
        }
    }

    [RelayCommand]
    private void HistoryDown()
    {
        if (_commandHistory.Count == 0)
            return;

        if (_historyIndex < _commandHistory.Count - 1)
        {
            _historyIndex++;
            InputText = _commandHistory[_historyIndex];
        }
        else if (_historyIndex == _commandHistory.Count - 1)
        {
            _historyIndex = _commandHistory.Count;
            InputText = _savedInput;
        }
    }

    [RelayCommand]
    private void ClearTerminal()
    {
        _terminalBuffer.Clear();
        TerminalOutput = string.Empty;
        _paragraphCount = 0;
        _ansiParser.Reset();
        TerminalCleared?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void ClearStatus()
    {
        StatusMessage = IsConnected ? $"Connected to {SelectedPort}" : "Disconnected";
    }

    [RelayCommand]
    private void ToggleDtr()
    {
        DtrState = !DtrState;
        if (IsConnected)
        {
            _terminalService.SetDtr(DtrState);
        }
    }

    [RelayCommand]
    private void ToggleRts()
    {
        RtsState = !RtsState;
        if (IsConnected)
        {
            _terminalService.SetRts(RtsState);
        }
    }

    [RelayCommand]
    private void CopyTerminalOutput()
    {
        if (!string.IsNullOrEmpty(TerminalOutput))
        {
            Clipboard.SetText(TerminalOutput);
            StatusMessage = "Terminal output copied to clipboard";
        }
    }

    /// <summary>
    /// Sets the selected port from an external device (e.g., MainViewModel.SelectedDevice).
    /// </summary>
    public void UseDevicePort(string? comPort)
    {
        if (string.IsNullOrEmpty(comPort))
            return;

        RefreshPorts();

        if (AvailablePorts.Contains(comPort))
        {
            SelectedPort = comPort;
            StatusMessage = $"Port set to {comPort}";
        }
        else
        {
            StatusMessage = $"Port {comPort} not available";
        }
    }

    private void OnDataReceived(object? sender, byte[] data)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            BytesReceived += data.Length;

            if (Settings.HexMode)
            {
                AppendHexData(data);
            }
            else if (Settings.ParseAnsiCodes)
            {
                AppendAnsiData(data);
            }
            else
            {
                AppendPlainData(data);
            }

            UpdateControlLineStates();
        });
    }

    private void AppendPlainData(byte[] data)
    {
        var text = Encoding.UTF8.GetString(data);
        var runs = new List<Run>();

        // Handle line breaks for timestamps
        var parts = text.Split('\n');
        for (int i = 0; i < parts.Length; i++)
        {
            var part = parts[i];

            // Add timestamp at start of each new line if enabled
            if (Settings.ShowTimestamps && (i > 0 || _paragraphCount == 0))
            {
                var timestampRun = new Run($"[{DateTime.Now:HH:mm:ss.fff}] ");
                timestampRun.SetResourceReference(Run.ForegroundProperty, "TerminalTimestampBrush");
                runs.Add(timestampRun);
            }

            if (!string.IsNullOrEmpty(part))
            {
                runs.Add(new Run(part));
            }

            // If there are more parts, this was a line break
            if (i < parts.Length - 1)
            {
                runs.Add(new Run("\n"));
                _paragraphCount++;
            }
        }

        if (runs.Count > 0)
        {
            // Update plain text buffer for copy functionality
            AppendToTerminal(text);

            // Notify UI to append styled content
            ContentAppended?.Invoke(this, new TerminalContentEventArgs(runs));
        }
    }

    private void AppendHexData(byte[] data)
    {
        var runs = new List<Run>();

        if (Settings.ShowTimestamps)
        {
            var timestampRun = new Run($"[{DateTime.Now:HH:mm:ss.fff}] ");
            timestampRun.SetResourceReference(Run.ForegroundProperty, "TerminalTimestampBrush");
            runs.Add(timestampRun);
        }

        // Format: "48 65 6C 6C 6F  |Hello|"
        var hex = BitConverter.ToString(data).Replace("-", " ");
        var ascii = new string(data.Select(b => b >= 32 && b < 127 ? (char)b : '.').ToArray());

        runs.Add(new Run($"{hex}  |{ascii}|\n"));
        _paragraphCount++;

        // Update plain text buffer
        var plainText = string.Join("", runs.Select(r => r.Text));
        AppendToTerminal(plainText);

        // Notify UI to append styled content
        ContentAppended?.Invoke(this, new TerminalContentEventArgs(runs));
    }

    private void AppendAnsiData(byte[] data)
    {
        var text = Encoding.UTF8.GetString(data);
        var runs = new List<Run>();

        // Parse ANSI codes and create styled runs
        var segments = _ansiParser.Parse(text);

        foreach (var segment in segments)
        {
            // Handle line breaks - split into multiple segments
            var parts = segment.Text.Split('\n');
            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];

                // Add timestamp at start of each new line if enabled
                if (i > 0 || (_paragraphCount == 0 && runs.Count == 0))
                {
                    if (Settings.ShowTimestamps && (i > 0 || _paragraphCount == 0))
                    {
                        var timestampRun = new Run($"[{DateTime.Now:HH:mm:ss.fff}] ");
                        timestampRun.SetResourceReference(Run.ForegroundProperty, "TerminalTimestampBrush");
                        runs.Add(timestampRun);
                    }
                }

                if (!string.IsNullOrEmpty(part))
                {
                    var run = CreateStyledRun(segment, part);
                    runs.Add(run);
                }

                // If there are more parts, this was a line break
                if (i < parts.Length - 1)
                {
                    runs.Add(new Run("\n"));
                    _paragraphCount++;
                }
            }
        }

        if (runs.Count > 0)
        {
            // Update plain text buffer for copy functionality
            var plainText = string.Join("", runs.Select(r => r.Text));
            AppendToTerminal(plainText);

            // Notify UI to append styled content
            ContentAppended?.Invoke(this, new TerminalContentEventArgs(runs));
        }

        // Limit paragraph count
        if (_paragraphCount > Settings.MaxDisplayLines)
        {
            // Signal that old content should be trimmed
            var trimCount = _paragraphCount - Settings.MaxDisplayLines;
            _paragraphCount = Settings.MaxDisplayLines;
        }
    }

    private Run CreateStyledRun(AnsiTextSegment segment, string text)
    {
        var run = new Run(text);

        if (segment.ForegroundColor.HasValue)
        {
            run.Foreground = new SolidColorBrush(segment.ForegroundColor.Value);
        }

        if (segment.BackgroundColor.HasValue)
        {
            run.Background = new SolidColorBrush(segment.BackgroundColor.Value);
        }

        if (segment.IsBold)
        {
            run.FontWeight = FontWeights.Bold;
        }

        if (segment.IsItalic)
        {
            run.FontStyle = FontStyles.Italic;
        }

        if (segment.IsUnderline)
        {
            run.TextDecorations = TextDecorations.Underline;
        }

        return run;
    }

    private void AppendToTerminal(string text, bool isEcho = false)
    {
        _terminalBuffer.Append(text);

        // Limit buffer size
        if (_terminalBuffer.Length > Settings.MaxDisplayLines * 100)
        {
            // Remove first 20% of buffer
            int removeCount = _terminalBuffer.Length / 5;
            _terminalBuffer.Remove(0, removeCount);
        }

        TerminalOutput = _terminalBuffer.ToString();

        // For echo, we need to emit styled runs
        if (isEcho)
        {
            var runs = new List<Run>();

            if (Settings.ShowTimestamps)
            {
                var timestampRun = new Run($"[{DateTime.Now:HH:mm:ss.fff}] ");
                timestampRun.SetResourceReference(Run.ForegroundProperty, "TerminalTimestampBrush");
                runs.Add(timestampRun);
            }

            // Echo text in a distinct color (using accent)
            var echoRun = new Run(text);
            echoRun.SetResourceReference(Run.ForegroundProperty, "AccentBrush");
            runs.Add(echoRun);

            if (text.EndsWith('\n'))
            {
                _paragraphCount++;
            }

            ContentAppended?.Invoke(this, new TerminalContentEventArgs(runs));
        }
    }

    private void UpdateControlLineStates()
    {
        if (IsConnected)
        {
            CtsState = _terminalService.GetCts();
            DsrState = _terminalService.GetDsr();
            CdState = _terminalService.GetCd();
        }
    }

    private void OnErrorOccurred(object? sender, string error)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            StatusMessage = error;
            _logger.Log(LogEntry.Error($"Terminal error: {error}"));
        });
    }

    private void OnConnected(object? sender, EventArgs e)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            IsConnected = true;
            UpdateControlLineStates();
        });
    }

    private void OnDisconnected(object? sender, EventArgs e)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            IsConnected = false;
            CtsState = false;
            DsrState = false;
            CdState = false;
        });
    }

    private void OnControlLineChanged(object? sender, EventArgs e)
    {
        Application.Current?.Dispatcher.Invoke(UpdateControlLineStates);
    }
}
