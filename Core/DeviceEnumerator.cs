﻿using HidWin.Devices;
using HidWin.Enums;
using HidWin.Natives;
using System.ComponentModel;
using System.IO.Ports;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
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
                _ => []
            };

        var result = new List<Device>();
        result.AddRange(GetHidDevices());
        result.AddRange(GetComDevices());
        result.AddRange(GetHidDevices());
        return result;
    }

    private static (ushort vid, ushort pid)? ParseVidPid(string devicePath)
    {
        if (string.IsNullOrEmpty(devicePath)) return null;

        var rx = new Regex(@"VID_([0-9A-F]{4})\s*&\s*PID_([0-9A-F]{4})",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        var m = rx.Match(devicePath);
        if (!m.Success) return null;

        var vid = Convert.ToUInt16(m.Groups[1].Value, 16);
        var pid = Convert.ToUInt16(m.Groups[2].Value, 16);
        return (vid, pid);
    }

    private static List<HidDevice> GetHidDevices()
    {
        var list = new List<HidDevice>();
        NativeMethods.HidD_GetHidGuid(out var hidGuid);

        var devInfo = NativeMethods.SetupDiGetClassDevs(
            ref hidGuid,
            null,
            IntPtr.Zero,
            NativeMethods.DIGCF_PRESENT | NativeMethods.DIGCF_DEVICEINTERFACE);

        if(devInfo == IntPtr.Zero || devInfo.ToInt64() == -1) return list;

        try
        {
            uint index = 0;
            while (true)
            {
                var deviceInterfaceData = new NativeMethods.SP_DEVICE_INTERFACE_DATA();
                if (!NativeMethods.SetupDiEnumDeviceInterfaces(devInfo, IntPtr.Zero, ref hidGuid, index,
                        out deviceInterfaceData))
                {
                    var err = Marshal.GetLastWin32Error();
                    if(err == 259) break;
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

                var detailBuffer = Marshal.AllocHGlobal(requiredSize);
                try
                {
                    var cbSize = IntPtr.Size == 8 ? 8 : 4;
                    Marshal.WriteInt32(detailBuffer, cbSize);
                    if (!NativeMethods.SetupDiGetDeviceInterfaceDetail(devInfo, ref deviceInterfaceData, detailBuffer,
                            requiredSize, out _, IntPtr.Zero))
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

                    if (devicePath.StartsWith("?\\"))
                        devicePath = "\\\\?\\" + devicePath.Substring(2);

                    var resultParse = ParseVidPid(devicePath);

                    if (resultParse != null)
                    {
                        list.Add(new HidDevice(DeviceKind.Hid)
                        {
                            DevicePath = devicePath,
                            VendorId = resultParse.Value.vid,
                            ProductId = resultParse.Value.pid
                        });
                        index++;
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

    public static List<ComDevice> GetComDevices()
    {
        var list = new List<ComDevice>();
        var guid = new Guid("86E0D1E0-8089-11D0-9CE4-08003E301F73");

        var devInfo = NativeMethods.SetupDiGetClassDevs(ref guid, null, IntPtr.Zero,
            NativeMethods.DIGCF_PRESENT | NativeMethods.DIGCF_DEVICEINTERFACE);
        if (devInfo == IntPtr.Zero || devInfo.ToInt64() == -1) return list;

        try
        {
            uint index = 0;
            while (true)
            {
                var ifaceData = new NativeMethods.SP_DEVICE_INTERFACE_DATA();

                if (!NativeMethods.SetupDiEnumDeviceInterfaces(devInfo, IntPtr.Zero, ref guid, index, out ifaceData))
                {
                    var err = Marshal.GetLastWin32Error();
                    if (err == 259) break; 
                    throw new Win32Exception(err);
                }

                if (!NativeMethods.SetupDiGetDeviceInterfaceDetail(devInfo, ref ifaceData, IntPtr.Zero, 0, out var requiredSize, IntPtr.Zero))
                {
                    var err = Marshal.GetLastWin32Error();
                    if (err != 122) 
                        throw new Win32Exception(err);
                    if (requiredSize == 0) { index++; continue; }
                }

                var detailBuffer = Marshal.AllocHGlobal(requiredSize);
                try
                {
                    var cbSize = IntPtr.Size == 8 ? 8 : 4;
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

                    var devInfoDataSize = Marshal.SizeOf<NativeMethods.SP_DEVINFO_DATA>();
                    var devInfoDataPtr = Marshal.AllocHGlobal(devInfoDataSize);
                    try
                    {
                        var devInfoDataInit = new NativeMethods.SP_DEVINFO_DATA();
                        Marshal.StructureToPtr(devInfoDataInit, devInfoDataPtr, false);
                        if (!NativeMethods.SetupDiGetDeviceInterfaceDetail(devInfo, ref ifaceData, detailBuffer, requiredSize, out _, devInfoDataPtr))
                        {
                            var m = Regex.Match(devicePath, @"\bCOM\d+\b", RegexOptions.IgnoreCase);
                            if (m.Success)
                            {
                                list.Add(new ComDevice { PortName = m.Value.ToUpperInvariant(), DevicePath = devicePath });
                            }
                            else
                            {
                                list.Add(new ComDevice { PortName = devicePath, DevicePath = devicePath });
                            }

                            index++;
                            continue;
                        }

                        var devInfoData = Marshal.PtrToStructure<NativeMethods.SP_DEVINFO_DATA>(devInfoDataPtr);

                        var buf = new byte[512];
                        if (NativeMethods.SetupDiGetDeviceRegistryProperty(devInfo, ref devInfoData, NativeMethods.SPDRP_FRIENDLYNAME, out _, buf, (uint)buf.Length, out var req))
                        {
                            string friendly;
                            try
                            {
                                friendly = Encoding.Unicode.GetString(buf, 0, (int)req).TrimEnd('\0');
                            }
                            catch
                            {
                                friendly = Encoding.Default.GetString(buf, 0, (int)req).TrimEnd('\0');
                            }

                            var m = Regex.Match(friendly, @"\bCOM\d+\b", RegexOptions.IgnoreCase);
                            if (m.Success)
                            {
                                var port = m.Value.ToUpperInvariant();
                                list.Add(new ComDevice { PortName = port, DevicePath = devicePath });
                            }
                            else
                            {
                                var m2 = Regex.Match(devicePath, @"COM\d+", RegexOptions.IgnoreCase);
                                if (m2.Success)
                                    list.Add(new ComDevice { PortName = m2.Value.ToUpperInvariant(), DevicePath = devicePath });
                            }
                        }
                        else
                        {
                            var m2 = Regex.Match(devicePath, @"COM\d+", RegexOptions.IgnoreCase);
                            if (m2.Success)
                                list.Add(new ComDevice { PortName = m2.Value.ToUpperInvariant(), DevicePath = devicePath });
                        }
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(devInfoDataPtr);
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

    public static List<UsbDevice> GetUsbDevices()
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
                var deviceInterfaceData = new NativeMethods.SP_DEVICE_INTERFACE_DATA();
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

                var detailDataBuffer = Marshal.AllocHGlobal(requiredSize);
                try
                {
                    var cbSize = IntPtr.Size == 8 ? 8 : 4;
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
}

