using HidWin.Devices;
using HidWin.Enums;
using HidWin.Natives;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace HidWin.Core;

public static class DeviceEnumerator
{
    public static List<Device> GetDevices(DeviceKind? kind = null)
    {
        if (kind != null)
            return kind switch
            {
                DeviceKind.Hid => GetHidDevices().Cast<Device>().ToList(),
                DeviceKind.Com => GetComDevices().Cast<Device>().ToList(),
                DeviceKind.Usb => GetUsbDevices().Cast<Device>().ToList(),
                _ => new List<Device>()
            };

        var result = new List<Device>();
        result.AddRange(GetHidDevices());
        result.AddRange(GetComDevices());
        result.AddRange(GetHidDevices());
        return result;
    }

    private static List<HidDevice> GetHidDevices()
    {
        var list = new List<HidDevice>();
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
                    if (err != 122) throw new Win32Exception(err);
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
                    var devicePath = Marshal.PtrToStringUni(devicePathPtr) ?? string.Empty;
                    if (string.IsNullOrEmpty(devicePath))
                    {
                        index++;
                        continue;
                    }

                    if (devicePath.StartsWith("\\?\\"))
                        devicePath = "\\\\?\\" + devicePath.Substring(3);
                    else if(devicePath.StartsWith("?\\"))
                        devicePath = "\\\\?\\" + devicePath.Substring(2);

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

    private static List<ComDevice> GetComDevices()
    {
        var list = new List<ComDevice>();
        var guid = new Guid("86E0D1E0-8089-11D0-9CE4-08003E301F73");

        var devInfo = NativeMethods.SetupDiGetClassDevs(ref guid, null, IntPtr.Zero,
            (int)(DeviceInfoFlags.DIGCF_PRESENT | DeviceInfoFlags.DIGCF_DEVICEINTERFACE));
        if (devInfo == IntPtr.Zero || devInfo.ToInt64() == -1) return list;

        try
        {
            uint index = 0;
            const int BUF_FRIENDLY = 512;
            var friendlyBuf = new byte[BUF_FRIENDLY];

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
                    if (err == (int)WinError.ERROR_INSUFFICIENT_BUFFER)
                        throw new Win32Exception(err);
                    if (requiredSize == 0)
                    {
                        index++;
                        continue;
                    }
                }

                var detailBuffer = Marshal.AllocHGlobal(requiredSize);
                var devInfoDataPtr = IntPtr.Zero;

                var cbSize = (IntPtr.Size == 8) ? 8 : 6;

                try
                {
                    Marshal.WriteInt32(detailBuffer, cbSize);

                    if (!NativeMethods.SetupDiGetDeviceInterfaceDetail(devInfo, ref ifaceData, detailBuffer, requiredSize, out _, IntPtr.Zero))
                    {
                        index++;
                        continue;
                    }

                    var pDevicePath = IntPtr.Add(detailBuffer, cbSize);
                    var devicePath = Marshal.PtrToStringUni(pDevicePath) ?? string.Empty;
                    if (devicePath.StartsWith("?\\"))
                        devicePath = "\\\\?\\" + devicePath.Substring(2);
                    else if (devicePath.StartsWith("\\?\\"))
                        devicePath = "\\\\?\\" + devicePath.Substring(3);

                    var devInfoDataSize = Marshal.SizeOf<NativeMethods.SP_DEVINFO_DATA>();
                    devInfoDataPtr = Marshal.AllocHGlobal(devInfoDataSize);
                    try
                    {
                        var devInfoDataInit = new NativeMethods.SP_DEVINFO_DATA().SetCbSize();
                        Marshal.StructureToPtr(devInfoDataInit, devInfoDataPtr, false);

                        var gotDevInfoData = NativeMethods.SetupDiGetDeviceInterfaceDetail(devInfo, ref ifaceData, detailBuffer, requiredSize, out _, devInfoDataPtr);

                        var deviceInstanceId = string.Empty;
                        var devInfoData = new NativeMethods.SP_DEVINFO_DATA();
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
                            var buf = new byte[2048];
                            if (NativeMethods.SetupDiGetDeviceRegistryProperty(devInfo, ref devInfoData, (uint)SetupDiProperty.SPDRP_HARDWAREID, out _, buf, (uint)buf.Length, out var required))
                            {
                                string hwIds;
                                try
                                {
                                    hwIds = Encoding.Unicode.GetString(buf, 0, (int)required);
                                }
                                catch
                                {
                                    hwIds = Encoding.Default.GetString(buf, 0, (int)required);
                                }

                                var parts = hwIds.Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries);
                                var vidpidRegex = new Regex(@"VID_([0-9A-Fa-f]{4}).*PID_([0-9A-Fa-f]{4})",
                                    RegexOptions.IgnoreCase);
                                foreach (var part in parts)
                                {
                                    var m = vidpidRegex.Match(part);
                                    if (!m.Success) continue;
                                    if (ushort.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.HexNumber, null, out var v) &&
                                        ushort.TryParse(m.Groups[2].Value, System.Globalization.NumberStyles.HexNumber, null, out var p))
                                    {
                                        vendor = v;
                                        product = p;
                                        haveVidPid = true;
                                        break;
                                    }
                                }
                            }
                        }

                        if (!haveVidPid)
                        {
                            var h = NativeMethods.CreateFile(
                                devicePath,
                                (uint)FileAccessMode.GENERIC_READ,
                                (uint)(FileShareMode.FILE_SHARE_READ | FileShareMode.FILE_SHARE_WRITE),
                                IntPtr.Zero,
                                (uint)FileCreationDisposition.OPEN_EXISTING,
                                0,
                                IntPtr.Zero);

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
                        try
                        {
                            if (gotDevInfoData)
                            {
                                if (!NativeMethods.SetupDiGetDeviceRegistryProperty(devInfo, ref devInfoData,
                                        (uint)SetupDiProperty.SPDRP_FRIENDLYNAME, out _, friendlyBuf,
                                        (uint)friendlyBuf.Length, out var reqF))
                                {
                                }
                                else
                                {
                                    try
                                    {
                                        friendly = Encoding.Unicode.GetString(friendlyBuf, 0, (int)reqF).TrimEnd('\0');
                                    }
                                    catch
                                    {
                                        friendly = Encoding.Default.GetString(friendlyBuf, 0, (int)reqF).TrimEnd('\0');
                                    }
                                }
                            }
                        }
                        catch { friendly = string.Empty; }

                        var portName = string.Empty;
                        var mFriendly = Regex.Match(friendly ?? string.Empty, @"\bCOM\d+\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        if (mFriendly.Success) portName = mFriendly.Value.ToUpperInvariant();
                        else
                        {
                            var m2 = Regex.Match(devicePath ?? string.Empty, @"\bCOM\d+\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                            if (m2.Success) portName = m2.Value.ToUpperInvariant();
                        }

                        if (string.IsNullOrEmpty(portName)) portName = devicePath;

                        var comDevice = new ComDevice
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
                            devInfoDataPtr = IntPtr.Zero;
                        }
                    }

                    index++;
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


    private static List<UsbDevice> GetUsbDevices()
    {
        var list = new List<UsbDevice>();
        var guid = new Guid("A5DCBF10-6530-11D2-901F-00C04FB951ED");
        var devInfo = NativeMethods.SetupDiGetClassDevs(ref guid, null, IntPtr.Zero, 0x00000002 | 0x00000010);
        if (devInfo == IntPtr.Zero || devInfo.ToInt64() == -1) return list;

        try
        {
            uint index = 0;
            while (true)
            {
                var deviceInterfaceData = new NativeMethods.SP_DEVICE_INTERFACE_DATA().SetCbSize();
                if (!NativeMethods.SetupDiEnumDeviceInterfaces(devInfo, IntPtr.Zero, ref guid, index, out deviceInterfaceData))
                {
                    var err = Marshal.GetLastWin32Error();
                    if (err == 259) break;
                    throw new Win32Exception(err);
                }

                if (!NativeMethods.SetupDiGetDeviceInterfaceDetail(devInfo, ref deviceInterfaceData, IntPtr.Zero, 0, out var requiredSize, IntPtr.Zero))
                {
                    var err = Marshal.GetLastWin32Error();
                    if (err != 122) throw new Win32Exception(err);
                    if (requiredSize == 0)
                    {
                        index++;
                        continue;
                    }
                }

                var cbSize = (IntPtr.Size == 8) ? 8 : 6;

                var detailDataBuffer = Marshal.AllocHGlobal(requiredSize);
                try
                {
                    Marshal.WriteInt32(detailDataBuffer, cbSize);
                    if (!NativeMethods.SetupDiGetDeviceInterfaceDetail(devInfo, ref deviceInterfaceData, detailDataBuffer, requiredSize, out _, IntPtr.Zero))
                    {
                        index++;
                        continue;
                    }

                    var devicePathPtr = IntPtr.Add(detailDataBuffer, cbSize);
                    var devicePath = Marshal.PtrToStringUni(devicePathPtr) ?? string.Empty;
                    if (string.IsNullOrEmpty(devicePath))
                    {
                        index++;
                        continue;
                    }
                    if (devicePath.StartsWith("?\\"))
                        devicePath = "\\\\?\\" + devicePath.Substring(2);

                    list.Add(new UsbDevice { DevicePath = devicePath });
                    index++;
                }
                finally
                {
                    Marshal.FreeHGlobal(detailDataBuffer);
                }
            }
        }
        finally
        {
            NativeMethods.SetupDiDestroyDeviceInfoList(devInfo);
        }

        return list;
    }


    private static string TryGetDeviceInstanceId(IntPtr devInfoSet, IntPtr devInfoElement, IntPtr devInfoDataPtr)
    {
        try
        {
            if (devInfoDataPtr == IntPtr.Zero) return string.Empty;
            var devInfoData = Marshal.PtrToStructure<NativeMethods.SP_DEVINFO_DATA>(devInfoDataPtr);
            return GetDeviceInstanceId(devInfoSet, ref devInfoData);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string TryGetFileSystemNameFromPath(string path)
    {
        try
        {
            var m = Regex.Match(path, @"^[A-Za-z]:\\");
            if (m.Success)
            {
                var root = path[..3];
                try
                {
                    var di = new DriveInfo(root);
                    return di.IsReady ? di.DriveFormat : string.Empty;
                }
                catch
                {
                    return null;
                }
            }

            var mVol = Regex.Match(path, @"^\\\\\?\\Volume\{[0-9A-Fa-f\-]+\}\\?", RegexOptions.IgnoreCase);
            if (mVol.Success)
            {
                var volPath = "\\".EndsWith(path) ? path : $"{path}\\";

                var fsNameBuf = new StringBuilder(261);
                if (NativeMethods.GetVolumeInformation(volPath, null, 0, out _, out _, out _, fsNameBuf, (uint)fsNameBuf.Capacity))
                {
                    return fsNameBuf.ToString().TrimEnd('\0');
                }
            }
        }
        catch
        {
            // ignored
        }

        return string.Empty;
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

