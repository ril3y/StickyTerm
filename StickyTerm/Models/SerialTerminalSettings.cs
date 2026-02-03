using System.IO.Ports;
using CommunityToolkit.Mvvm.ComponentModel;

namespace StickyTerm.Models;

/// <summary>
/// Settings for the serial terminal connection and display.
/// </summary>
public partial class SerialTerminalSettings : ObservableObject
{
    // Connection settings
    [ObservableProperty]
    private int _baudRate = 115200;

    [ObservableProperty]
    private int _dataBits = 8;

    [ObservableProperty]
    private StopBits _stopBits = StopBits.One;

    [ObservableProperty]
    private Parity _parity = Parity.None;

    [ObservableProperty]
    private Handshake _handshake = Handshake.None;

    // Line control
    [ObservableProperty]
    private bool _dtrEnabled = true;

    [ObservableProperty]
    private bool _rtsEnabled = true;

    // Display settings
    [ObservableProperty]
    private bool _showTimestamps;

    [ObservableProperty]
    private bool _hexMode;

    [ObservableProperty]
    private bool _localEcho;

    [ObservableProperty]
    private bool _autoScroll = true;

    [ObservableProperty]
    private bool _parseAnsiCodes = true;

    // Line ending settings
    [ObservableProperty]
    private LineEnding _sendLineEnding = LineEnding.CRLF;

    [ObservableProperty]
    private LineEnding _receiveLineEnding = LineEnding.LF;

    // Buffer settings
    [ObservableProperty]
    private int _maxDisplayLines = 10000;

    // Logging
    [ObservableProperty]
    private bool _logToFile;

    [ObservableProperty]
    private string _logFilePath = string.Empty;

    /// <summary>
    /// Standard baud rates available for selection.
    /// </summary>
    public static int[] AvailableBaudRates => [300, 1200, 2400, 4800, 9600, 19200, 38400, 57600, 115200, 230400, 460800, 921600];

    /// <summary>
    /// Available data bit options.
    /// </summary>
    public static int[] AvailableDataBits => [5, 6, 7, 8];

    /// <summary>
    /// Creates a copy of these settings.
    /// </summary>
    public SerialTerminalSettings Clone()
    {
        return new SerialTerminalSettings
        {
            BaudRate = BaudRate,
            DataBits = DataBits,
            StopBits = StopBits,
            Parity = Parity,
            Handshake = Handshake,
            DtrEnabled = DtrEnabled,
            RtsEnabled = RtsEnabled,
            ShowTimestamps = ShowTimestamps,
            HexMode = HexMode,
            LocalEcho = LocalEcho,
            AutoScroll = AutoScroll,
            ParseAnsiCodes = ParseAnsiCodes,
            SendLineEnding = SendLineEnding,
            ReceiveLineEnding = ReceiveLineEnding,
            MaxDisplayLines = MaxDisplayLines,
            LogToFile = LogToFile,
            LogFilePath = LogFilePath
        };
    }
}

/// <summary>
/// Line ending options for serial communication.
/// </summary>
public enum LineEnding
{
    /// <summary>No line ending added.</summary>
    None,
    /// <summary>Carriage return only (\r).</summary>
    CR,
    /// <summary>Line feed only (\n).</summary>
    LF,
    /// <summary>Carriage return + line feed (\r\n).</summary>
    CRLF
}

/// <summary>
/// Extension methods for LineEnding enum.
/// </summary>
public static class LineEndingExtensions
{
    /// <summary>
    /// Gets the string representation of the line ending.
    /// </summary>
    public static string ToLineEndingString(this LineEnding lineEnding)
    {
        return lineEnding switch
        {
            LineEnding.CR => "\r",
            LineEnding.LF => "\n",
            LineEnding.CRLF => "\r\n",
            _ => string.Empty
        };
    }
}
