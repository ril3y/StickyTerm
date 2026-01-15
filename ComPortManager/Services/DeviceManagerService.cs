using System.Runtime.InteropServices;
using ComPortManager.Models;

namespace ComPortManager.Services;

/// <summary>
/// Device manager service using SetupAPI for device enable/disable.
/// </summary>
public class DeviceManagerService : IDeviceManagerService
{
    private readonly ILoggingService _logger;

    public DeviceManagerService(ILoggingService logger)
    {
        _logger = logger;
    }

    public async Task<bool> DisableDeviceAsync(string pnpDeviceId)
    {
        return await Task.Run(() => SetDeviceState(pnpDeviceId, false));
    }

    public async Task<bool> EnableDeviceAsync(string pnpDeviceId)
    {
        return await Task.Run(() => SetDeviceState(pnpDeviceId, true));
    }

    public async Task<bool> RestartDeviceAsync(string pnpDeviceId)
    {
        _logger.Log(LogEntry.Info($"Restarting device: {pnpDeviceId}"));

        if (!await DisableDeviceAsync(pnpDeviceId))
        {
            _logger.Log(LogEntry.Error($"Failed to disable device for restart: {pnpDeviceId}"));
            return false;
        }

        // Wait a moment for device to fully disable
        await Task.Delay(500);

        if (!await EnableDeviceAsync(pnpDeviceId))
        {
            _logger.Log(LogEntry.Error($"Failed to re-enable device after restart: {pnpDeviceId}"));
            return false;
        }

        _logger.Log(LogEntry.Info($"Device restarted successfully: {pnpDeviceId}"));
        return true;
    }

    public async Task<bool> IsDeviceEnabledAsync(string pnpDeviceId)
    {
        return await Task.Run(() =>
        {
            var deviceInfoSet = IntPtr.Zero;
            try
            {
                deviceInfoSet = NativeMethods.SetupDiGetClassDevs(
                    IntPtr.Zero,
                    pnpDeviceId,
                    IntPtr.Zero,
                    NativeMethods.DIGCF_ALLCLASSES | NativeMethods.DIGCF_DEVICEINTERFACE);

                if (deviceInfoSet == NativeMethods.INVALID_HANDLE_VALUE)
                    return false;

                var deviceInfoData = new NativeMethods.SP_DEVINFO_DATA
                {
                    cbSize = (uint)Marshal.SizeOf<NativeMethods.SP_DEVINFO_DATA>()
                };

                if (!NativeMethods.SetupDiEnumDeviceInfo(deviceInfoSet, 0, ref deviceInfoData))
                    return false;

                uint status = 0, problem = 0;
                if (!NativeMethods.CM_Get_DevNode_Status(ref status, ref problem, deviceInfoData.DevInst, 0))
                    return false;

                // DN_STARTED flag indicates device is running
                return (status & NativeMethods.DN_STARTED) != 0;
            }
            catch (Exception ex)
            {
                _logger.Log(LogEntry.Warning($"Failed to check device status: {ex.Message}"));
                return false;
            }
            finally
            {
                if (deviceInfoSet != IntPtr.Zero && deviceInfoSet != NativeMethods.INVALID_HANDLE_VALUE)
                {
                    NativeMethods.SetupDiDestroyDeviceInfoList(deviceInfoSet);
                }
            }
        });
    }

    private bool SetDeviceState(string pnpDeviceId, bool enable)
    {
        var deviceInfoSet = IntPtr.Zero;
        try
        {
            // Get device info set for the specific device
            deviceInfoSet = NativeMethods.SetupDiGetClassDevs(
                IntPtr.Zero,
                pnpDeviceId,
                IntPtr.Zero,
                NativeMethods.DIGCF_ALLCLASSES | NativeMethods.DIGCF_DEVICEINTERFACE);

            if (deviceInfoSet == NativeMethods.INVALID_HANDLE_VALUE)
            {
                _logger.Log(LogEntry.Error($"Failed to get device info set for {pnpDeviceId}"));
                return false;
            }

            var deviceInfoData = new NativeMethods.SP_DEVINFO_DATA
            {
                cbSize = (uint)Marshal.SizeOf<NativeMethods.SP_DEVINFO_DATA>()
            };

            if (!NativeMethods.SetupDiEnumDeviceInfo(deviceInfoSet, 0, ref deviceInfoData))
            {
                _logger.Log(LogEntry.Error($"Failed to enumerate device info for {pnpDeviceId}"));
                return false;
            }

            var propChangeParams = new NativeMethods.SP_PROPCHANGE_PARAMS
            {
                ClassInstallHeader = new NativeMethods.SP_CLASSINSTALL_HEADER
                {
                    cbSize = (uint)Marshal.SizeOf<NativeMethods.SP_CLASSINSTALL_HEADER>(),
                    InstallFunction = NativeMethods.DIF_PROPERTYCHANGE
                },
                StateChange = enable ? NativeMethods.DICS_ENABLE : NativeMethods.DICS_DISABLE,
                Scope = NativeMethods.DICS_FLAG_CONFIGSPECIFIC,
                HwProfile = 0
            };

            if (!NativeMethods.SetupDiSetClassInstallParams(
                deviceInfoSet,
                ref deviceInfoData,
                ref propChangeParams,
                (uint)Marshal.SizeOf<NativeMethods.SP_PROPCHANGE_PARAMS>()))
            {
                var error = Marshal.GetLastWin32Error();
                _logger.Log(LogEntry.Error($"Failed to set class install params (error {error}) for {pnpDeviceId}"));
                return false;
            }

            if (!NativeMethods.SetupDiCallClassInstaller(
                NativeMethods.DIF_PROPERTYCHANGE,
                deviceInfoSet,
                ref deviceInfoData))
            {
                var error = Marshal.GetLastWin32Error();
                _logger.Log(LogEntry.Error($"Failed to call class installer (error {error}) for {pnpDeviceId}"));
                return false;
            }

            _logger.Log(LogEntry.Info($"Device {(enable ? "enabled" : "disabled")}: {pnpDeviceId}"));
            return true;
        }
        catch (Exception ex)
        {
            _logger.Log(LogEntry.Error($"Error {(enable ? "enabling" : "disabling")} device: {ex.Message}"));
            return false;
        }
        finally
        {
            if (deviceInfoSet != IntPtr.Zero && deviceInfoSet != NativeMethods.INVALID_HANDLE_VALUE)
            {
                NativeMethods.SetupDiDestroyDeviceInfoList(deviceInfoSet);
            }
        }
    }
}

/// <summary>
/// Native methods for SetupAPI device management.
/// </summary>
internal static partial class NativeMethods
{
    public static readonly IntPtr INVALID_HANDLE_VALUE = new(-1);

    public const uint DIGCF_ALLCLASSES = 0x00000004;
    public const uint DIGCF_DEVICEINTERFACE = 0x00000010;

    public const uint DIF_PROPERTYCHANGE = 0x00000012;

    public const uint DICS_ENABLE = 0x00000001;
    public const uint DICS_DISABLE = 0x00000002;

    public const uint DICS_FLAG_CONFIGSPECIFIC = 0x00000002;

    public const uint DN_STARTED = 0x00000008;

    [StructLayout(LayoutKind.Sequential)]
    public struct SP_DEVINFO_DATA
    {
        public uint cbSize;
        public Guid ClassGuid;
        public uint DevInst;
        public IntPtr Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SP_CLASSINSTALL_HEADER
    {
        public uint cbSize;
        public uint InstallFunction;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SP_PROPCHANGE_PARAMS
    {
        public SP_CLASSINSTALL_HEADER ClassInstallHeader;
        public uint StateChange;
        public uint Scope;
        public uint HwProfile;
    }

    [LibraryImport("setupapi.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    public static partial IntPtr SetupDiGetClassDevs(
        IntPtr classGuid,
        [MarshalAs(UnmanagedType.LPWStr)] string? enumerator,
        IntPtr hwndParent,
        uint flags);

    [LibraryImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetupDiEnumDeviceInfo(
        IntPtr deviceInfoSet,
        uint memberIndex,
        ref SP_DEVINFO_DATA deviceInfoData);

    [LibraryImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetupDiSetClassInstallParams(
        IntPtr deviceInfoSet,
        ref SP_DEVINFO_DATA deviceInfoData,
        ref SP_PROPCHANGE_PARAMS classInstallParams,
        uint classInstallParamsSize);

    [LibraryImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetupDiCallClassInstaller(
        uint installFunction,
        IntPtr deviceInfoSet,
        ref SP_DEVINFO_DATA deviceInfoData);

    [LibraryImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

    [LibraryImport("cfgmgr32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool CM_Get_DevNode_Status(
        ref uint status,
        ref uint problemNumber,
        uint devInst,
        uint flags);
}
