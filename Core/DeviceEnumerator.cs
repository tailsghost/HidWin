using HidWin.Devices;
using HidWin.Enums;
using HidWin.Natives;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace HidWin.Core;

public static class DeviceEnumerator
{
    public static List<Device> GetDevices(DeviceKind? kind = null)
    {
        if (kind != null)
            return kind switch
            {
                DeviceKind.Hid => GetHidDevices(),
                DeviceKind.Serial => GetSerialDevices(),
                _ => new List<Device>()
            };

        var result = new List<Device>();
        result.AddRange(GetHidDevices());
        result.AddRange(GetHidDevices());
        return result;
    }

    private static List<Device> GetHidDevices()
    {
        var hidGuid = NativeMethods.HidD_GetHidGuid();

        var list = new List<Device>();

        NativeMethods.EnumerateDeviceInterfaces(hidGuid, (_, __, ___, deviceID, devicePath) =>
        {
            TryExtractVidPid(deviceID, out var vid, out var pid);
            list.Add(new HidDevice(deviceID)
            {
                DevicePath = devicePath,
                VendorId = vid,
                ProductId = pid
            });
        });

        return list;
    }

    public static List<Device> GetSerialDevices()
    {
        var list = new List<Device>();
        NativeMethods.EnumerateDevices(NativeMethods.GuidForPortsClass, (deviceInfoSet, deviceInfoData, deviceID) =>
        {
            if (!NativeMethods.TryGetSerialPortFriendlyName(deviceInfoSet, ref deviceInfoData, out var friendlyName) ||
                !NativeMethods.TryGetSerialPortName(deviceInfoSet, ref deviceInfoData, out var portName)) return;
            TryExtractVidPid(deviceID, out var vid, out var pid);
            list.Add(new SerialDevice()
            {
                DeviceId = deviceID,
                DevicePath = $"\\\\?\\" + portName,
                FileSystemName = portName,
                FriendlyName = friendlyName,
                VendorId = vid,
                ProductId = pid
            });
        });

        return list;
    }

    private static bool TryExtractVidPid(string deviceId, out ushort vid, out ushort pid)
    {
        vid = 0;
        pid = 0;

        if (string.IsNullOrEmpty(deviceId))
            return false;

        deviceId = deviceId.ToUpperInvariant();

        var vidIndex = deviceId.IndexOf("VID_", StringComparison.Ordinal);
        var pidIndex = deviceId.IndexOf("PID_", StringComparison.Ordinal);

        if (vidIndex < 0 || pidIndex < 0)
            return false;

        var vidStr = GetHexPart(deviceId, vidIndex + 4);
        var pidStr = GetHexPart(deviceId, pidIndex + 4);

        if (vidStr == null || pidStr == null)
            return false;

        try
        {
            vid = Convert.ToUInt16(vidStr, 16);
            pid = Convert.ToUInt16(pidStr, 16);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string? GetHexPart(string text, int start)
    {
        var sb = new StringBuilder(4);
        for (var i = start; i < text.Length; i++)
        {
            var c = text[i];
            if (Uri.IsHexDigit(c))
                sb.Append(c);
            else
                break;
        }

        return sb.Length >= 4 ? sb.ToString() : null;
    }
}

