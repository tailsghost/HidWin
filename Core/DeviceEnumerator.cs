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
        var list = new List<Device>();
        NativeMethods.HidD_GetHidGuid(out var hidGuid);

        var devInfo = NativeMethods.SetupDiGetClassDevs(
            ref hidGuid,
            null,
            IntPtr.Zero,
            (int)(DeviceInfoFlags.DIGCF_PRESENT | DeviceInfoFlags.DIGCF_DEVICEINTERFACE));

        if (devInfo == IntPtr.Zero || devInfo.ToInt64() == -1) return list;

        try
        {
            uint index = 0;
            while (true)
            {
                var deviceInterfaceData = new NativeMethods.SP_DEVICE_INTERFACE_DATA().SetCbSize();
                if (!NativeMethods.SetupDiEnumDeviceInterfaces(devInfo, IntPtr.Zero, ref hidGuid, index,
                        out deviceInterfaceData))
                {
                    var err = Marshal.GetLastWin32Error();
                    if (err == 259) break;
                    throw new Win32Exception(err);
                }

                if (!NativeMethods.SetupDiGetDeviceInterfaceDetail(devInfo, ref deviceInterfaceData, IntPtr.Zero, 0,
                        out var requiredSize, IntPtr.Zero))
                {
                    var err = Marshal.GetLastWin32Error();
                    if (err != (int)WinError.ERROR_NO_MORE_ITEMS) throw new Win32Exception(err);
                    if (requiredSize == 0)
                    {
                        index++;
                        continue;
                    }
                }


                var devInfoData = new NativeMethods.SP_DEVINFO_DATA().SetCbSize();
                var devInfoPtr = Marshal.AllocHGlobal(devInfoData.cbSize);
                Marshal.StructureToPtr(devInfoData, devInfoPtr, false);
                var detailBuffer = Marshal.AllocHGlobal(requiredSize);

                var cbSize = (IntPtr.Size == 8) ? 8 : 6;
                try
                {
                    Marshal.WriteInt32(detailBuffer, cbSize);
                    if (!NativeMethods.SetupDiGetDeviceInterfaceDetail(devInfo, ref deviceInterfaceData, detailBuffer,
                            requiredSize, out _, devInfoPtr))
                    {
                        index++;
                        continue;
                    }

                    var devicePathPtr = IntPtr.Add(detailBuffer, cbSize);
                    var devicePath = NormalizeString(Marshal.PtrToStringUni(devicePathPtr) ?? string.Empty);
                    if (string.IsNullOrEmpty(devicePath))
                    {
                        index++;
                        continue;
                    }

                    var handle = IntPtr.Zero;

                    try
                    {
                        handle = NativeMethods.CreateFileFromDevice(devicePath, FileAccessMode.GENERIC_READ,
                            FileShareMode.FILE_SHARE_READ | FileShareMode.FILE_SHARE_WRITE);

                        var attrs = new NativeMethods.HIDD_ATTRIBUTES
                        {
                            Size = Marshal.SizeOf<NativeMethods.HIDD_ATTRIBUTES>()
                        };

                        if (!NativeMethods.HidD_GetAttributes(handle, ref attrs))
                        {
                            index++;
                            continue;
                        }

                        list.Add(new HidDevice(GetDeviceInstanceId(devInfo, ref devInfoData))
                        {
                            DevicePath = devicePath,
                            VendorId = attrs.VendorID,
                            ProductId = attrs.ProductID,
                            Version = attrs.VersionNumber
                        });

                        index++;
                    }
                    finally
                    {
                        if (handle != IntPtr.Zero)
                            NativeMethods.CloseHandle(handle);
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(detailBuffer);
                }
            }
        }
        finally
        {
            NativeMethods.SetupDiDestroyDeviceInfoList(devInfo);
        }

        return list;
    }

    public static List<Device> GetSerialDevices()
    {
        var list = new List<Device>();

        var guids = new[]
        {
                new Guid("86E0D1E0-8089-11D0-9CE4-08003E301F73"),
                new Guid("A5DCBF10-6530-11D2-901F-00C04FB951ED")
            };

        foreach (var guid in guids)
        {
            list.AddRange(GetDevice(guid));
        }

        return list;
    }

    private static List<Device> GetDevice(Guid guid)
    {
        var list = new List<Device>();
        var flags = (int)(DeviceInfoFlags.DIGCF_PRESENT | DeviceInfoFlags.DIGCF_DEVICEINTERFACE);
        var devInfo = NativeMethods.SetupDiGetClassDevs(ref guid, null, IntPtr.Zero, flags);
        if (devInfo == IntPtr.Zero || devInfo.ToInt64() == -1) return list;

        try
        {
            uint index = 0;
            while (true)
            {
                var ifaceData = new NativeMethods.SP_DEVICE_INTERFACE_DATA().SetCbSize();
                if (!NativeMethods.SetupDiEnumDeviceInterfaces(devInfo, IntPtr.Zero, ref guid, index, out ifaceData))
                {
                    var err = Marshal.GetLastWin32Error();
                    if (err != (int)WinError.ERROR_NO_MORE_ITEMS) break;
                    throw new Win32Exception(err);
                }

                if (!NativeMethods.SetupDiGetDeviceInterfaceDetail(devInfo, ref ifaceData, IntPtr.Zero, 0, out var requiredSize, IntPtr.Zero))
                {
                    var err = Marshal.GetLastWin32Error();
                    if (err != (int)WinError.ERROR_NO_MORE_ITEMS) throw new Win32Exception(err);
                    if (requiredSize == 0) { index++; continue; }
                }

                var cbSize = (IntPtr.Size == 8) ? 8 : 6;
                var detailBuffer = Marshal.AllocHGlobal(requiredSize);
                var devInfoDataPtr = IntPtr.Zero;

                try
                {
                    Marshal.WriteInt32(detailBuffer, cbSize);

                    if (!NativeMethods.SetupDiGetDeviceInterfaceDetail(devInfo, ref ifaceData, detailBuffer, requiredSize, out _, IntPtr.Zero))
                    {
                        index++;
                        continue;
                    }

                    var pDevicePath = IntPtr.Add(detailBuffer, cbSize);
                    var devicePath = NormalizeString(Marshal.PtrToStringUni(pDevicePath) ?? string.Empty);

                    var devInfoDataSize = Marshal.SizeOf<NativeMethods.SP_DEVINFO_DATA>();
                    devInfoDataPtr = Marshal.AllocHGlobal(devInfoDataSize);
                    var devInfoDataInit = new NativeMethods.SP_DEVINFO_DATA().SetCbSize();
                    Marshal.StructureToPtr(devInfoDataInit, devInfoDataPtr, false);

                    var gotDevInfoData = NativeMethods.SetupDiGetDeviceInterfaceDetail(devInfo, ref ifaceData, detailBuffer, requiredSize, out _, devInfoDataPtr);

                    var devInfoData = new NativeMethods.SP_DEVINFO_DATA();
                    var deviceInstanceId = string.Empty;
                    if (gotDevInfoData)
                    {
                        devInfoData = Marshal.PtrToStructure<NativeMethods.SP_DEVINFO_DATA>(devInfoDataPtr);
                        try
                        {
                            deviceInstanceId = GetDeviceInstanceId(devInfo, ref devInfoData);
                        }
                        catch
                        {
                            deviceInstanceId = string.Empty;
                        }
                    }

                    ushort vendor = 0, product = 0;
                    var haveVidPid = false;

                    if (gotDevInfoData)
                    {
                        var buf = new byte[4096];
                        if (NativeMethods.SetupDiGetDeviceRegistryProperty(devInfo, ref devInfoData, (uint)SetupDiProperty.SPDRP_HARDWAREID, out _, buf, (uint)buf.Length, out var required))
                        {
                            string hwIds;
                            try { hwIds = Encoding.Unicode.GetString(buf, 0, (int)required); }
                            catch { hwIds = Encoding.Default.GetString(buf, 0, (int)required); }

                            if (TryExtractVidPidFromHwIdString(hwIds, out vendor, out product))
                                haveVidPid = true;
                        }
                    }

                    if (!haveVidPid)
                    {
                        var h = NativeMethods.CreateFile(devicePath, (uint)FileAccessMode.GENERIC_READ, (uint)(FileShareMode.FILE_SHARE_READ | FileShareMode.FILE_SHARE_WRITE), IntPtr.Zero, (uint)FileCreationDisposition.OPEN_EXISTING, 0, IntPtr.Zero);
                        if (h != IntPtr.Zero && h.ToInt64() != -1)
                        {
                            try
                            {
                                var attrs = new NativeMethods.HIDD_ATTRIBUTES { Size = Marshal.SizeOf<NativeMethods.HIDD_ATTRIBUTES>() };
                                if (NativeMethods.HidD_GetAttributes(h, ref attrs))
                                {
                                    vendor = attrs.VendorID;
                                    product = attrs.ProductID;
                                    haveVidPid = true;
                                }
                            }
                            finally
                            {
                                NativeMethods.CloseHandle(h);
                            }
                        }
                    }

                    var friendly = string.Empty;
                    if (gotDevInfoData)
                    {
                        var friendlyBuf = new byte[512];
                        if (NativeMethods.SetupDiGetDeviceRegistryProperty(devInfo, ref devInfoData, (uint)SetupDiProperty.SPDRP_FRIENDLYNAME, out _, friendlyBuf, (uint)friendlyBuf.Length, out var reqF))
                        {
                            try { friendly = Encoding.Unicode.GetString(friendlyBuf, 0, (int)reqF).TrimEnd('\0'); }
                            catch { friendly = Encoding.Default.GetString(friendlyBuf, 0, (int)reqF).TrimEnd('\0'); }
                        }
                    }

                    var portName = ExtractComPortName(friendly);
                    if (string.IsNullOrEmpty(portName)) portName = ExtractComPortName(devicePath);
                    if (string.IsNullOrEmpty(portName)) portName = devicePath;

                    var comDevice = new SerialDevice
                    {
                        PortName = portName,
                        DevicePath = devicePath,
                        VendorId = haveVidPid ? vendor : (ushort)0,
                        ProductId = haveVidPid ? product : (ushort)0,
                        FriendlyName = friendly,
                        DeviceId = deviceInstanceId,
                        FileSystemName = TryGetFileSystemNameFromPath(devicePath)
                    };

                    list.Add(comDevice);
                }
                finally
                {
                    if (devInfoDataPtr != IntPtr.Zero)
                    {
                        Marshal.FreeHGlobal(devInfoDataPtr);
                    }
                    Marshal.FreeHGlobal(detailBuffer);
                }

                index++;
            }
        }
        finally
        {
            NativeMethods.SetupDiDestroyDeviceInfoList(devInfo);
        }

        return list;
    }


    private static string NormalizeString(string value)
    {
        var result = string.Empty;
        if (value.StartsWith("\\?\\")) result = "\\\\?\\" + value[3..];
        else if (value.StartsWith("?\\")) result = "\\\\?\\" + value[2..];

        return result;
    }


    private static bool TryExtractVidPidFromHwIdString(string hwIds, out ushort vid, out ushort pid)
    {
        vid = 0; pid = 0;
        if (string.IsNullOrEmpty(hwIds)) return false;

        var s = hwIds.ToUpperInvariant();

        var vidIndex = s.IndexOf("VID_", StringComparison.Ordinal);

        var vidStr = string.Empty;
        var pidStr = string.Empty;

        if (vidIndex >= 0 && vidIndex + 4 + 4 <= s.Length)
        {
            var start = vidIndex + 4;
            vidStr = ReadHexDigits(s, start, 4);
            if (vidStr.Length == 4 && ushort.TryParse(vidStr, System.Globalization.NumberStyles.HexNumber, null, out var v))
            {
                var pidIndex = s.IndexOf("PID_", start + 4, StringComparison.Ordinal);
                if (pidIndex >= 0 && pidIndex + 4 + 4 <= s.Length)
                {
                    var pstart = pidIndex + 4;
                    pidStr = ReadHexDigits(s, pstart, 4);
                    if (pidStr.Length == 4 && ushort.TryParse(pidStr, System.Globalization.NumberStyles.HexNumber, null, out var p))
                    {
                        vid = v; pid = p;
                        return true;
                    }
                }
                else
                {
                    var anyPid = s.IndexOf("PID_", StringComparison.Ordinal);
                    if (anyPid >= 0 && anyPid + 4 + 4 <= s.Length)
                    {
                        pidStr = ReadHexDigits(s, anyPid + 4, 4);
                        if (pidStr.Length == 4 && ushort.TryParse(pidStr, System.Globalization.NumberStyles.HexNumber, null, out var p))
                        {
                            vid = v; pid = p;
                            return true;
                        }
                    }
                }
            }
        }

        var pidIndex2 = s.IndexOf("PID_", StringComparison.Ordinal);
        if (pidIndex2 < 0 || pidIndex2 + 4 + 4 > s.Length) return false;

        pidStr = ReadHexDigits(s, pidIndex2 + 4, 4);
        if (pidStr.Length != 4 ||
            !ushort.TryParse(pidStr, System.Globalization.NumberStyles.HexNumber, null, out var p1)) return false;
        var vidIndex2 = s.IndexOf("VID_", StringComparison.Ordinal);
        if (vidIndex2 < 0 || vidIndex2 + 4 + 4 > s.Length) return false;
        vidStr = ReadHexDigits(s, vidIndex2 + 4, 4);
        if (vidStr.Length != 4 ||
            !ushort.TryParse(vidStr, System.Globalization.NumberStyles.HexNumber, null, out var v2)) return false;
        vid = v2; pid = p1; return true;

    }

    private static string ReadHexDigits(string s, int pos, int maxDigits)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < maxDigits && pos + i < s.Length; i++)
        {
            var c = s[pos + i];
            var isHex =
                c is >= '0' and <= '9' or >= 'A' and <= 'F' or >= 'a' and <= 'f';
            if (!isHex) break;
            sb.Append(c);
        }
        return sb.ToString();
    }

    private static string ExtractComPortName(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        var s = input.ToUpperInvariant();
        for (var i = 0; i < s.Length - 2; i++)
        {
            if (s[i] != 'C') continue;
            if (i + 2 >= s.Length) continue;
            if (s[i + 1] != 'O' || s[i + 2] != 'M') continue;

            var j = i + 3;
            var digits = new StringBuilder();
            while (j < s.Length && char.IsDigit(s[j]) && digits.Length < 5) 
            {
                digits.Append(s[j]);
                j++;
            }

            if (digits.Length > 0)
            {
                return "COM" + digits.ToString();
            }
        }

        return string.Empty;
    }

    private static string TryGetFileSystemNameFromPath(string path)
    {
        try
        {
            if (string.IsNullOrEmpty(path)) return string.Empty;

            if (path.Length >= 3 && path[1] == ':' && (path[2] == '\\' || path[2] == '/')
                && ((path[0] >= 'A' && path[0] <= 'Z') || (path[0] >= 'a' && path[0] <= 'z')))
            {
                var root = path[..3].Replace('/', '\\');
                try
                {
                    var di = new DriveInfo(root);
                    return di.IsReady ? di.DriveFormat : string.Empty;
                }
                catch
                {
                    return string.Empty;
                }
            }

            const string volumePrefix = @"\\?\Volume{";
            if (path.Length < volumePrefix.Length || !path.StartsWith(volumePrefix, StringComparison.OrdinalIgnoreCase))
                return string.Empty;
            var closeBrace = path.IndexOf('}', volumePrefix.Length - 1);
            if (closeBrace <= volumePrefix.Length - 2) return string.Empty;
            var volRoot = string.Empty;
            if (closeBrace + 1 < path.Length && (path[closeBrace + 1] == '\\' || path[closeBrace + 1] == '/'))
                volRoot = path[..(closeBrace + 2)]; 
            else
                volRoot = path[..(closeBrace + 1)] + "\\";

            var fsNameBuf = new StringBuilder(261);
            try
            {
                if (NativeMethods.GetVolumeInformation(volRoot, null, 0, out _, out _, out _, fsNameBuf, (uint)fsNameBuf.Capacity))
                {
                    return fsNameBuf.ToString().TrimEnd('\0');
                }
            }
            catch
            {
                return string.Empty;
            }

            return string.Empty;

        }
        catch
        {
            return string.Empty;
        }
    }

    private static string GetDeviceInstanceId(IntPtr devInfoSet, ref NativeMethods.SP_DEVINFO_DATA devInfoData)
    {
        var buf = new StringBuilder(512);
        if (NativeMethods.SetupDiGetDeviceInstanceId(devInfoSet, ref devInfoData, buf, buf.Capacity, out var requiredLen))
        {
            return buf.ToString();
        }

        var err = Marshal.GetLastWin32Error();
        if (err != 122 || requiredLen <= 0) return string.Empty;
        var sb = new StringBuilder(requiredLen);
        return NativeMethods.SetupDiGetDeviceInstanceId(devInfoSet, ref devInfoData, sb, sb.Capacity, out _) ? sb.ToString() : string.Empty;
    }
}

