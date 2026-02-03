using System.IO.Ports;

namespace StickyTerm.Services;

/// <summary>
/// Abstraction layer for serial port operations.
/// Allows swapping System.IO.Ports with SerialPortStream if needed.
/// </summary>
public interface ISerialPortWrapper : IDisposable
{
    /// <summary>Gets the port name (e.g., "COM3").</summary>
    string PortName { get; }

    /// <summary>Gets whether the port is currently open.</summary>
    bool IsOpen { get; }

    /// <summary>Gets or sets the baud rate.</summary>
    int BaudRate { get; set; }

    /// <summary>Gets or sets the number of data bits.</summary>
    int DataBits { get; set; }

    /// <summary>Gets or sets the stop bits.</summary>
    StopBits StopBits { get; set; }

    /// <summary>Gets or sets the parity.</summary>
    Parity Parity { get; set; }

    /// <summary>Gets or sets the handshake protocol.</summary>
    Handshake Handshake { get; set; }

    /// <summary>Gets or sets DTR (Data Terminal Ready) state.</summary>
    bool DtrEnable { get; set; }

    /// <summary>Gets or sets RTS (Request to Send) state.</summary>
    bool RtsEnable { get; set; }

    /// <summary>Gets CTS (Clear to Send) holding state.</summary>
    bool CtsHolding { get; }

    /// <summary>Gets DSR (Data Set Ready) holding state.</summary>
    bool DsrHolding { get; }

    /// <summary>Gets CD (Carrier Detect) holding state.</summary>
    bool CDHolding { get; }

    /// <summary>Gets or sets the read timeout in milliseconds.</summary>
    int ReadTimeout { get; set; }

    /// <summary>Gets or sets the write timeout in milliseconds.</summary>
    int WriteTimeout { get; set; }

    /// <summary>Opens the serial port.</summary>
    void Open();

    /// <summary>Closes the serial port.</summary>
    void Close();

    /// <summary>Closes the serial port asynchronously.</summary>
    Task CloseAsync();

    /// <summary>Reads data from the port asynchronously.</summary>
    Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token);

    /// <summary>Writes data to the port asynchronously.</summary>
    Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken token);

    /// <summary>Writes text to the port asynchronously.</summary>
    Task WriteAsync(string text, CancellationToken token);

    /// <summary>Discards the input buffer.</summary>
    void DiscardInBuffer();

    /// <summary>Discards the output buffer.</summary>
    void DiscardOutBuffer();

    /// <summary>Raised when data is received.</summary>
    event EventHandler<byte[]>? DataReceived;

    /// <summary>Raised when an error occurs.</summary>
    event EventHandler<Exception>? ErrorOccurred;
}
