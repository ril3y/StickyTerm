using System.Globalization;
using System.IO.Ports;
using System.Text;
using StickyTerm.Models;

namespace StickyTerm.Services;

/// <summary>
/// Implementation of ISerialTerminalService using SerialPortWrapper.
/// </summary>
public class SerialTerminalService : ISerialTerminalService
{
    private ISerialPortWrapper? _port;
    private bool _disposed;

    public bool IsConnected => _port?.IsOpen ?? false;
    public string? CurrentPort => _port?.PortName;
    public SerialTerminalSettings Settings { get; private set; } = new();

    public event EventHandler<byte[]>? DataReceived;
    public event EventHandler<string>? ErrorOccurred;
    public event EventHandler? Connected;
    public event EventHandler? Disconnected;
    public event EventHandler? ControlLineChanged;

    public async Task<bool> ConnectAsync(string portName, SerialTerminalSettings settings)
    {
        if (_port?.IsOpen == true)
        {
            await DisconnectAsync();
        }

        try
        {
            Settings = settings.Clone();
            _port = new SerialPortWrapper(portName)
            {
                BaudRate = settings.BaudRate,
                DataBits = settings.DataBits,
                StopBits = settings.StopBits,
                Parity = settings.Parity,
                Handshake = settings.Handshake
            };

            _port.DataReceived += OnDataReceived;
            _port.ErrorOccurred += OnErrorOccurred;

            _port.Open();

            // Set initial DTR/RTS states
            _port.DtrEnable = settings.DtrEnabled;
            _port.RtsEnable = settings.RtsEnabled;

            Connected?.Invoke(this, EventArgs.Empty);
            return true;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Failed to connect: {ex.Message}");
            _port?.Dispose();
            _port = null;
            return false;
        }
    }

    public async Task DisconnectAsync()
    {
        if (_port != null)
        {
            _port.DataReceived -= OnDataReceived;
            _port.ErrorOccurred -= OnErrorOccurred;
            await _port.CloseAsync();
            _port.Dispose();
            _port = null;
            Disconnected?.Invoke(this, EventArgs.Empty);
        }
    }

    public async Task SendTextAsync(string text)
    {
        if (_port?.IsOpen != true)
        {
            ErrorOccurred?.Invoke(this, "Not connected");
            return;
        }

        try
        {
            var textWithEnding = text + Settings.SendLineEnding.ToLineEndingString();
            await _port.WriteAsync(textWithEnding, CancellationToken.None);
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Send failed: {ex.Message}");
        }
    }

    public async Task SendBytesAsync(byte[] data)
    {
        if (_port?.IsOpen != true)
        {
            ErrorOccurred?.Invoke(this, "Not connected");
            return;
        }

        try
        {
            await _port.WriteAsync(data, 0, data.Length, CancellationToken.None);
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Send failed: {ex.Message}");
        }
    }

    public async Task SendHexStringAsync(string hexString)
    {
        if (string.IsNullOrWhiteSpace(hexString))
            return;

        // Parse hex - let exceptions propagate so caller can display error
        var bytes = ParseHexString(hexString);
        await SendBytesAsync(bytes);
    }

    /// <summary>
    /// Parses a hex string like "1B 5B 32 4A" or "0x1B 0x5B" into bytes.
    /// </summary>
    private static byte[] ParseHexString(string hexString)
    {
        // Remove common prefixes and separators
        var cleaned = hexString
            .Replace("0x", " ", StringComparison.OrdinalIgnoreCase)
            .Replace(",", " ")
            .Replace("-", " ")
            .Trim();

        var parts = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var bytes = new byte[parts.Length];

        for (int i = 0; i < parts.Length; i++)
        {
            bytes[i] = byte.Parse(parts[i], NumberStyles.HexNumber);
        }

        return bytes;
    }

    public void SetDtr(bool enabled)
    {
        if (_port?.IsOpen == true)
        {
            _port.DtrEnable = enabled;
            Settings.DtrEnabled = enabled;
            ControlLineChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void SetRts(bool enabled)
    {
        if (_port?.IsOpen == true)
        {
            _port.RtsEnable = enabled;
            Settings.RtsEnabled = enabled;
            ControlLineChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void ToggleDtr()
    {
        if (_port?.IsOpen == true)
        {
            SetDtr(!_port.DtrEnable);
        }
    }

    public void ToggleRts()
    {
        if (_port?.IsOpen == true)
        {
            SetRts(!_port.RtsEnable);
        }
    }

    public bool GetCts() => _port?.CtsHolding ?? false;
    public bool GetDsr() => _port?.DsrHolding ?? false;
    public bool GetCd() => _port?.CDHolding ?? false;

    public void ClearBuffers()
    {
        if (_port?.IsOpen == true)
        {
            _port.DiscardInBuffer();
            _port.DiscardOutBuffer();
        }
    }

    public string[] GetAvailablePorts()
    {
        return SerialPort.GetPortNames()
            .Distinct(StringComparer.OrdinalIgnoreCase) // Remove duplicates (known .NET issue)
            .OrderBy(p =>
            {
                // Sort by port number
                if (p.StartsWith("COM", StringComparison.OrdinalIgnoreCase) &&
                    int.TryParse(p.AsSpan(3), out var num))
                {
                    return num;
                }
                return int.MaxValue;
            }).ToArray();
    }

    private void OnDataReceived(object? sender, byte[] data)
    {
        DataReceived?.Invoke(this, data);
    }

    private void OnErrorOccurred(object? sender, Exception ex)
    {
        ErrorOccurred?.Invoke(this, ex.Message);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Disconnect synchronously but without blocking the UI thread
        if (_port != null)
        {
            _port.DataReceived -= OnDataReceived;
            _port.ErrorOccurred -= OnErrorOccurred;
            _port.Close(); // Use sync Close() for Dispose since we're shutting down
            _port.Dispose();
            _port = null;
        }
    }
}
