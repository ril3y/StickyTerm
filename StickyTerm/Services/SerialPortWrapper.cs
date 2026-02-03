using System.IO;
using System.IO.Ports;
using System.Text;

namespace StickyTerm.Services;

/// <summary>
/// Wrapper around System.IO.Ports.SerialPort that implements ISerialPortWrapper.
/// Uses BaseStream.ReadAsync for reliable data reception instead of the DataReceived event.
/// </summary>
public class SerialPortWrapper : ISerialPortWrapper
{
    private readonly SerialPort _serialPort;
    private CancellationTokenSource? _readCts;
    private Task? _readTask;
    private bool _disposed;

    public string PortName => _serialPort.PortName;
    public bool IsOpen => _serialPort.IsOpen;

    public int BaudRate
    {
        get => _serialPort.BaudRate;
        set => _serialPort.BaudRate = value;
    }

    public int DataBits
    {
        get => _serialPort.DataBits;
        set => _serialPort.DataBits = value;
    }

    public StopBits StopBits
    {
        get => _serialPort.StopBits;
        set => _serialPort.StopBits = value;
    }

    public Parity Parity
    {
        get => _serialPort.Parity;
        set => _serialPort.Parity = value;
    }

    public Handshake Handshake
    {
        get => _serialPort.Handshake;
        set => _serialPort.Handshake = value;
    }

    public bool DtrEnable
    {
        get => _serialPort.DtrEnable;
        set => _serialPort.DtrEnable = value;
    }

    public bool RtsEnable
    {
        get => _serialPort.RtsEnable;
        set => _serialPort.RtsEnable = value;
    }

    public bool CtsHolding => _serialPort.CtsHolding;
    public bool DsrHolding => _serialPort.DsrHolding;
    public bool CDHolding => _serialPort.CDHolding;

    public int ReadTimeout
    {
        get => _serialPort.ReadTimeout;
        set => _serialPort.ReadTimeout = value;
    }

    public int WriteTimeout
    {
        get => _serialPort.WriteTimeout;
        set => _serialPort.WriteTimeout = value;
    }

    public event EventHandler<byte[]>? DataReceived;
    public event EventHandler<Exception>? ErrorOccurred;

    public SerialPortWrapper(string portName)
    {
        _serialPort = new SerialPort(portName)
        {
            ReadTimeout = 100,
            WriteTimeout = 1000
        };
    }

    public void Open()
    {
        if (_serialPort.IsOpen)
            return;

        _serialPort.Open();

        // Start continuous read loop
        _readCts = new CancellationTokenSource();
        _readTask = ReadLoopAsync(_readCts.Token);
    }

    public void Close()
    {
        CloseAsync().GetAwaiter().GetResult();
    }

    public async Task CloseAsync()
    {
        if (!_serialPort.IsOpen)
            return;

        // Stop read loop
        _readCts?.Cancel();
        if (_readTask != null)
        {
            try
            {
                await _readTask.WaitAsync(TimeSpan.FromMilliseconds(500));
            }
            catch (TimeoutException)
            {
                // Read task didn't finish in time, continue anyway
            }
            catch (OperationCanceledException)
            {
                // Expected when cancelling
            }
        }
        _readCts?.Dispose();
        _readCts = null;
        _readTask = null;

        _serialPort.Close();
    }

    public async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token)
    {
        return await _serialPort.BaseStream.ReadAsync(buffer, offset, count, token);
    }

    public async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken token)
    {
        await _serialPort.BaseStream.WriteAsync(buffer, offset, count, token);
    }

    public async Task WriteAsync(string text, CancellationToken token)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        await WriteAsync(bytes, 0, bytes.Length, token);
    }

    public void DiscardInBuffer()
    {
        _serialPort.DiscardInBuffer();
    }

    public void DiscardOutBuffer()
    {
        _serialPort.DiscardOutBuffer();
    }

    /// <summary>
    /// Continuous read loop using BaseStream.ReadAsync for reliable data reception.
    /// This is more reliable than the DataReceived event in WPF applications.
    /// </summary>
    private async Task ReadLoopAsync(CancellationToken token)
    {
        var buffer = new byte[4096];

        while (!token.IsCancellationRequested)
        {
            try
            {
                if (!_serialPort.IsOpen)
                    break;

                int bytesRead = await _serialPort.BaseStream.ReadAsync(buffer, 0, buffer.Length, token);

                if (bytesRead > 0)
                {
                    var data = new byte[bytesRead];
                    Array.Copy(buffer, data, bytesRead);
                    DataReceived?.Invoke(this, data);
                }
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation
                break;
            }
            catch (IOException)
            {
                // Port disconnected
                break;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex);
                // Small delay to avoid tight loop on persistent errors
                await Task.Delay(100, token);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Close();
        _serialPort.Dispose();
    }
}
