using StickyTerm.Models;

namespace StickyTerm.Services;

/// <summary>
/// Service for managing serial terminal connections and data flow.
/// </summary>
public interface ISerialTerminalService : IDisposable
{
    /// <summary>Gets whether the terminal is connected to a port.</summary>
    bool IsConnected { get; }

    /// <summary>Gets the currently connected port name, or null if not connected.</summary>
    string? CurrentPort { get; }

    /// <summary>Gets the current terminal settings.</summary>
    SerialTerminalSettings Settings { get; }

    /// <summary>
    /// Connects to a serial port with the specified settings.
    /// </summary>
    Task<bool> ConnectAsync(string portName, SerialTerminalSettings settings);

    /// <summary>
    /// Disconnects from the current port.
    /// </summary>
    Task DisconnectAsync();

    /// <summary>
    /// Sends text to the connected port.
    /// </summary>
    Task SendTextAsync(string text);

    /// <summary>
    /// Sends raw bytes to the connected port.
    /// </summary>
    Task SendBytesAsync(byte[] data);

    /// <summary>
    /// Sends hex-encoded bytes to the connected port.
    /// Example input: "1B 5B 32 4A" or "0x1B 0x5B"
    /// </summary>
    Task SendHexStringAsync(string hexString);

    /// <summary>
    /// Sets the DTR (Data Terminal Ready) signal state.
    /// </summary>
    void SetDtr(bool enabled);

    /// <summary>
    /// Sets the RTS (Request to Send) signal state.
    /// </summary>
    void SetRts(bool enabled);

    /// <summary>
    /// Toggles the DTR signal state.
    /// </summary>
    void ToggleDtr();

    /// <summary>
    /// Toggles the RTS signal state.
    /// </summary>
    void ToggleRts();

    /// <summary>
    /// Gets the current CTS (Clear to Send) signal state.
    /// </summary>
    bool GetCts();

    /// <summary>
    /// Gets the current DSR (Data Set Ready) signal state.
    /// </summary>
    bool GetDsr();

    /// <summary>
    /// Gets the current CD (Carrier Detect) signal state.
    /// </summary>
    bool GetCd();

    /// <summary>
    /// Clears the input and output buffers.
    /// </summary>
    void ClearBuffers();

    /// <summary>
    /// Gets a list of available COM port names.
    /// </summary>
    string[] GetAvailablePorts();

    /// <summary>Raised when data is received from the port.</summary>
    event EventHandler<byte[]>? DataReceived;

    /// <summary>Raised when an error occurs.</summary>
    event EventHandler<string>? ErrorOccurred;

    /// <summary>Raised when connected to a port.</summary>
    event EventHandler? Connected;

    /// <summary>Raised when disconnected from a port.</summary>
    event EventHandler? Disconnected;

    /// <summary>Raised when a control line state changes.</summary>
    event EventHandler? ControlLineChanged;
}
