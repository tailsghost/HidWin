using HidWin.Enums;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace HidWin.Natives;

public static class NativeMethods
{

    public static readonly Guid GuidForPortsClass = new Guid("{4D36E978-E325-11CE-BFC1-08002BE10318}");

    public delegate void EnumerateDeviceInterfacesCallback(IntPtr deviceInfoSet,
        SP_DEVINFO_DATA deviceInfoData,
        SP_DEVICE_INTERFACE_DATA deviceInterfaceData,
        string deviceID, string devicePath);

    [StructLayout(LayoutKind.Sequential)]
    internal struct HIDD_ATTRIBUTES
    {
        internal int Size;
        internal ushort VendorID;
        internal ushort ProductID;
        internal ushort VersionNumber;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct HIDP_CAPS
    {
        public ushort Usage;
        public ushort UsagePage;
        public ushort InputReportByteLength;
        public ushort OutputReportByteLength;
        public ushort FeatureReportByteLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
        public byte[] Reserved;
        public ushort NumberLinkCollectionNodes;
        public ushort NumberInputButtonCaps;
        public ushort NumberInputValueCaps;
        public ushort NumberInputDataIncludes;
        public ushort NumberOutputButtonCaps;
        public ushort NumberOutputValueCaps;
        public ushort NumberOutputDataIncludes;
        public ushort NumberFeatureButtonCaps;
        public ushort NumberFeatureValueCaps;
        public ushort NumberFeatureDataIncludes;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SP_DEVICE_INTERFACE_DATA
    {
        public int cbSize;
        public Guid InterfaceClassGuid;
        public int Flags;
        public IntPtr Reserved;

        public SP_DEVICE_INTERFACE_DATA SetCbSize()
        {
            cbSize = Marshal.SizeOf(this);
            return this;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SP_DEVINFO_DATA
    {
        public int cbSize;
        public Guid ClassGuid;
        public int DevInst;
        public IntPtr Reserved;

        public SP_DEVINFO_DATA SetCbSize()
        {
            cbSize = Marshal.SizeOf(this);
            return this;
        }
    }

    [Obfuscation(Exclude = true)]
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct SP_DEVICE_INTERFACE_DETAIL_DATA
    {
        public int Size;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 1024)] public string DevicePath;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DCB
    {
        public int DCBlength;
        public uint BaudRate;
        public uint fFlags;

        public bool fBinary
        {
            get => GetBool(0);
            set => SetBool(0, value);
        }

        public bool fParity
        {
            get => GetBool(1);
            set => SetBool(1, value);
        }

        public bool fOutxCtsFlow
        {
            get => GetBool(2);
            set => SetBool(2, value);
        }

        public bool fOutxDsrFlow
        {
            get => GetBool(3);
            set => SetBool(3, value);
        }

        public uint fDtrControl
        {
            get => GetBits(4, 2);
            set => SetBits(4, 2, value);
        }

        public bool fDsrSensitivity
        {
            get => GetBool(6);
            set => SetBool(6, value);
        }

        public bool fTXContinueOnXoff
        {
            get => GetBool(7);
            set => SetBool(7, value);
        }

        public bool fOutX
        {
            get => GetBool(8);
            set => SetBool(8, value);
        }

        public bool fInX
        {
            get => GetBool(9);
            set => SetBool(9, value);
        }

        public bool fErrorChar
        {
            get => GetBool(10);
            set => SetBool(10, value);
        }
        public bool fNull
        {
            get => GetBool(11);
            set => SetBool(11, value);
        }
        public uint fRtsControl
        {
            get => GetBits(12, 2);
            set => SetBits(12, 2, value);
        }
        public bool fAbortOnError
        {
            get => GetBool(14);
            set => SetBool(14, value);
        }

        ushort Reserved1;
        public ushort XonLim;
        public ushort XoffLim;
        public byte ByteSize;
        public byte Parity;
        public byte StopBits;
        public byte XonChar;
        public byte XoffChar;
        public byte ErrorChar;
        public byte EofChar;
        public byte EvtChar;
        ushort Reserved2;

        private uint GetBitMask(int bitCount)
        {
            return (1u << bitCount) - 1;
        }

        private uint GetBits(int bitOffset, int bitCount)
        {
            return (fFlags >> bitOffset) & GetBitMask(bitCount);
        }

        private void SetBits(int bitOffset, int bitCount, uint value)
        {
            var mask = GetBitMask(bitCount);
            fFlags &= ~(mask << bitOffset);
            fFlags |= (value & mask) << bitOffset;
        }

        private bool GetBool(int bitOffset)
        {
            return GetBits(bitOffset, 1) != 0;
        }

        private void SetBool(int bitOffset, bool value)
        {
            SetBits(bitOffset, 1, value ? 1u : 0);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct COMMTIMEOUTS
    {
        public uint ReadIntervalTimeout;
        public uint ReadTotalTimeoutMultiplier;
        public uint ReadTotalTimeoutConstant;
        public uint WriteTotalTimeoutMultiplier;
        public uint WriteTotalTimeoutConstant;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetCommState(IntPtr handle, ref DCB dcb);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetCommState(IntPtr handle, ref DCB dcb);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool PurgeComm(IntPtr handle, uint flags);



    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    internal static extern IntPtr CreateFile(
        string fileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ReadFile(
        IntPtr hFile,
        IntPtr lpBuffer,
        int nNumberOfBytesToRead,
        IntPtr lpNumberOfBytesRead,
        IntPtr lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool WriteFile(
        IntPtr hFile,
        IntPtr lpBuffer,
        int nNumberOfBytesToWrite,
        IntPtr lpNumberOfBytesWritten,
        IntPtr lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CancelIo(IntPtr handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetCommTimeouts(IntPtr handle, out COMMTIMEOUTS timeouts);

    [DllImport("hid.dll", SetLastError = true)]
    internal static extern void HidD_GetHidGuid(out Guid guid);

    [DllImport("hid.dll", SetLastError = true)]
    internal static extern bool HidD_GetAttributes(IntPtr hidObject, ref HIDD_ATTRIBUTES attributes);

    [DllImport("hid.dll", SetLastError = true, CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool HidD_GetSerialNumberString(IntPtr handle, char[] buffer, int bufferLengthInBytes);

    [DllImport("hid.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool HidD_GetPreparsedData(IntPtr handle, out IntPtr preparsed);

    [DllImport("hid.dll", SetLastError = true)]
    internal static extern int HidP_GetCaps(IntPtr preparsedData, out HIDP_CAPS caps);

    [DllImport("hid.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool HidD_GetFeature(IntPtr handle, IntPtr reportBuffer, int reportBufferLength);

    [DllImport("hid.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool HidD_SetFeature(IntPtr handle, IntPtr reportBuffer, int reportBufferLength);

    [DllImport("hid.dll", SetLastError = true, CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool HidD_GetManufacturerString(IntPtr handle, char[] buffer, int bufferLengthInBytes);

    [DllImport("hid.dll", SetLastError = true, CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool HidD_GetProductString(IntPtr handle, char[] buffer, int bufferLengthInBytes);

    [DllImport("hid.dll", SetLastError = true, CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool HidD_SetNumInputBuffers(IntPtr handle, int count);

    [DllImport("hid.dll", SetLastError = true)]
    internal static extern bool HidD_FreePreparsedData(IntPtr preparsedData);

    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
    internal static extern IntPtr SetupDiGetClassDevs(ref Guid ClassGuid, string Enumerator, IntPtr hwndParent,
        int flags);

    [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
    internal static extern bool SetupDiGetDeviceRegistryProperty(
        IntPtr DeviceInfoSet, ref SP_DEVINFO_DATA DeviceInfoData, uint Property,
        out uint PropertyRegDataType, byte[] PropertyBuffer, uint PropertyBufferSize, out uint RequiredSize);

    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetupDiGetDeviceRegistryProperty(IntPtr deviceInfoSet,
        ref SP_DEVINFO_DATA deviceInfoData,
        uint property, out uint propertyDataType,
        char[] buffer, int lengthInBytes, IntPtr lengthInBytesRequired);

    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
    internal static extern bool SetupDiEnumDeviceInterfaces(IntPtr hDevInfo, IntPtr devInfo, ref Guid interfaceGuid,
        uint memberIndex, out SP_DEVICE_INTERFACE_DATA deviceInterfaceData);

    [DllImport("setupapi.dll", SetLastError = true)]
    public static extern IntPtr SetupDiOpenDevRegKey(IntPtr deviceInfoSet,
        ref SP_DEVINFO_DATA deviceInfoData,
        int scope = (int)DeviceInstanceFlags.DICS_FLAG_GLOBAL,
        int profile = 0,
        int keyType = (int)DeviceRegistryScope.DIREG_DEV,
        uint desiredAccess = (uint)RegistryAccess.KEY_READ);

    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetupDiEnumDeviceInfo(IntPtr deviceInfoSet, int memberIndex,
        ref SP_DEVINFO_DATA deviceInfoData);


    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
    internal static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr hDevInfo,
        ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData, IntPtr deviceInterfaceDetailData,
        int deviceInterfaceDetailSize, out int requiredSize, IntPtr deviceInfoData);

    [DllImport("setupapi.dll", SetLastError = true)]
    internal static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern uint WaitForMultipleObjects(uint count, IntPtr[] handles,
        [MarshalAs(UnmanagedType.Bool)] bool waitAll, uint milliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetOverlappedResult(IntPtr handle,
        IntPtr overlapped, out uint bytesTransferred,
        [MarshalAs(UnmanagedType.Bool)] bool wait);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr CreateEvent(IntPtr eventAttributes,
        [MarshalAs(UnmanagedType.Bool)] bool manualReset,
        [MarshalAs(UnmanagedType.Bool)] bool initialState,
        IntPtr name);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetEvent(IntPtr handle);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern bool SetupDiGetDeviceInstanceId(
        IntPtr DeviceInfoSet,
        ref SP_DEVINFO_DATA DeviceInfoData,
        StringBuilder DeviceInstanceId,
        int DeviceInstanceIdSize,
        out int RequiredSize
    );

    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetupDiEnumDeviceInterfaces(IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfoData,
        [MarshalAs(UnmanagedType.LPStruct)] Guid interfaceClassGuid, int memberIndex,
        ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern bool GetVolumeInformation(
        string rootPathName,
        StringBuilder volumeNameBuffer,
        uint volumeNameSize,
        out uint volumeSerialNumber,
        out uint maximumComponentLength,
        out uint fileSystemFlags,
        StringBuilder fileSystemNameBuffer,
        uint fileSystemNameSize
    );

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    public static extern int CM_Get_Device_ID(uint devInst, char[] buffer, int length, int flags = 0);

    [DllImport("cfgmgr32.dll")]
    public static extern int CM_Get_Device_ID_Size(out int length, uint devInst, int flags = 0);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern int RegQueryValueEx(IntPtr handle, string valueName, uint reserved, IntPtr type, char[] buffer, ref int lengthInBytes);

    [DllImport("advapi32.dll", SetLastError = true)]
    public static extern int RegCloseKey(IntPtr handle);

    public static uint WaitForMultipleObjectsGetTimeout(int eventTimeout)
    {
        return eventTimeout < 0 ? ~(uint)0 : (uint)eventTimeout;
    }

    public static IntPtr CreateFileFromDevice(string filename, FileAccessMode desiredAccess, FileShareMode shareMode)
    {
        return CreateFile(filename, (uint)desiredAccess, (uint)shareMode, IntPtr.Zero,
            (uint)FileCreationDisposition.OPEN_EXISTING,
            (uint)(FileFlags.FILE_FLAG_DEVICE | FileFlags.FILE_FLAG_OVERLAPPED),
            IntPtr.Zero);
    }

    public static void OverlappedOperation(
        IntPtr ioHandle,
        IntPtr eventHandle,
        int eventTimeout,
        IntPtr closeEventHandle,
        bool overlapResult,
        IntPtr overlappedPtr,
        out uint bytesTransferred)
    {
        var closed = false;
        bytesTransferred = 0;

        if (!overlapResult)
        {
            var err = Marshal.GetLastWin32Error();
            if (err != (int)WinError.ERROR_IO_PENDING)
            {
                throw new IOException($"Operation failed early: {new Win32Exception(err).Message} - {err}");
            }

            var handles = closeEventHandle != IntPtr.Zero ? new[] { eventHandle, closeEventHandle } : new[] { eventHandle };

            var waitMillis = WaitForMultipleObjectsGetTimeout(eventTimeout);

            var waitResult = WaitForMultipleObjects((uint)handles.Length, handles, false, waitMillis);

            switch (waitResult)
            {
                case 0:
                    break;
                case 1 when handles.Length >= 2:
                    closed = true;
                    break;
                default:
                    CancelIo(ioHandle);
                    break;
            }
        }

        if (GetOverlappedResult(ioHandle, overlappedPtr, out bytesTransferred, true))
            return;


        var finalErr = Marshal.GetLastWin32Error();
        if (finalErr != (int)WinError.ERROR_HANDLE_EOF)
        {
            if (closed)
            {
                throw new ObjectDisposedException("Closed.");
            }

            if (finalErr == (int)WinError.ERROR_OPERATION_ABORTED)
            {
                throw new TimeoutException("Operation timed out.");
            }

            throw new IOException("Operation failed after some time.", new Win32Exception(finalErr));
        }

        bytesTransferred = 0;
    }

    public static void EnumerateDeviceInterfaces(Guid guid, EnumerateDeviceInterfacesCallback callback)
    {
        EnumerateDeviceInterfaces(guid, null, callback);
    }

    public static void EnumerateDeviceInterfaces(Guid guid, string deviceIDToFilterTo, EnumerateDeviceInterfacesCallback callback)
    {
        EnumerateDevicesCore(SetupDiGetClassDevs(ref guid, deviceIDToFilterTo, IntPtr.Zero, (int)(DIGCF.DeviceInterface | DIGCF.Present)),
            (devInfo, dvi, deviceID) =>
            {
                var did = new SP_DEVICE_INTERFACE_DATA();
                did.cbSize = Marshal.SizeOf(did);

                for (var i = 0; SetupDiEnumDeviceInterfaces(devInfo, ref dvi, guid, i, ref did); i++)
                {
                    if (SetupDiGetDeviceInterfaceDevicePath(devInfo, ref did, out var devicePath))
                    {
                        callback(devInfo, dvi, did, deviceID, devicePath);
                    }
                }
            });
    }

    public static bool SetupDiGetDeviceInterfaceDevicePath(IntPtr deviceInfoSet,
        ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData,
        out string devicePath)
    {

        devicePath = GetDeviceInterfaceDetail(deviceInfoSet, ref deviceInterfaceData);

        return !string.IsNullOrEmpty(devicePath);
    }

    public static string GetDeviceInterfaceDetail(
        IntPtr deviceInfoSet,
        ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData)
    {
        SetupDiGetDeviceInterfaceDetail(
            deviceInfoSet, ref deviceInterfaceData, IntPtr.Zero, 0, out var requiredSize, IntPtr.Zero);

        if (requiredSize == 0)
            return null;

        var detailDataBuffer = Marshal.AllocHGlobal(requiredSize);

        try
        {
            Marshal.WriteInt32(detailDataBuffer, IntPtr.Size == 8 ? 8 : 4 + Marshal.SystemDefaultCharSize);

            if (!SetupDiGetDeviceInterfaceDetail(
                    deviceInfoSet, ref deviceInterfaceData,
                    detailDataBuffer, requiredSize, out _, IntPtr.Zero))
            {
                return null;
            }

            var pDevicePath = IntPtr.Add(detailDataBuffer, 4); 
            var devicePath = Marshal.PtrToStringAuto(pDevicePath);

            return devicePath;
        }
        finally
        {
            Marshal.FreeHGlobal(detailDataBuffer);
        }
    }

    public static void EnumerateDevices(Guid guid, Action<IntPtr, SP_DEVINFO_DATA, string> callback)
    {
        EnumerateDevicesCore(NativeMethods.SetupDiGetClassDevs(ref guid, null, IntPtr.Zero, (int)DIGCF.Present), callback);
    }

    private static void EnumerateDevicesCore(IntPtr devInfo, Action<IntPtr, SP_DEVINFO_DATA, string> callback)
    {
        try
        {
            var dvi = new SP_DEVINFO_DATA();
            dvi.cbSize = Marshal.SizeOf(dvi);

            for (var j = 0; SetupDiEnumDeviceInfo(devInfo, j, ref dvi); j++)
            {
                var deviceID = string.Empty;
                if (0 != CM_Get_Device_ID((uint)dvi.DevInst, out deviceID)) { continue; }

                callback(devInfo, dvi, deviceID);
            }
        }
        finally
        {
            SetupDiDestroyDeviceInfoList(devInfo);
        }
    }

    public static int CM_Get_Device_ID(uint devInst, out string deviceID)
    {
        int ret; 
        deviceID = string.Empty;

        ret = CM_Get_Device_ID_Size(out var length, devInst);
        if (ret != 0) { return ret; }

        var chars = new char[length + 1];
        ret = CM_Get_Device_ID(devInst, chars, chars.Length);
        if (ret != 0) { return ret; }

        deviceID = new string(chars, 0, length);
        return 0;
    }

    public static bool TryGetSerialPortFriendlyName(IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfoData, out string friendlyName)
    {
        if (TryGetDeviceRegistryProperty(deviceInfoSet, ref deviceInfoData, (uint)SPDRP.SPDRP_FRIENDLYNAME, out friendlyName))
        {

        }
        else if (TryGetDeviceRegistryProperty(deviceInfoSet, ref deviceInfoData, (uint)SPDRP.SPDRP_DEVICEDESC, out friendlyName))
        {

        }
        else
        {
            friendlyName = string.Empty;
        }

        return !string.IsNullOrEmpty(friendlyName);
    }

    public static bool TryGetSerialPortName(IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfoData, out string portName)
    {
        portName = string.Empty;

        var hkey = SetupDiOpenDevRegKey(deviceInfoSet, ref deviceInfoData);
        if (hkey == (IntPtr)(-1)) return !string.IsNullOrEmpty(portName);

        try
        {
            var portNameChars = new char[64];
            var portNameLength = 63 * 2;
            if (0 == RegQueryValueEx(hkey, "PortName", 0, IntPtr.Zero, portNameChars, ref portNameLength))
            {
                Array.Resize(ref portNameChars, portNameLength / 2);

                var newPortName = NTString(portNameChars);
                if (newPortName.Length >= 4 && newPortName.StartsWith("COM"))
                {
                    if (!int.TryParse(newPortName[3..], NumberStyles.Integer, CultureInfo.InvariantCulture,
                            out var newPortNumber) ||
                        newPortName != "COM" + newPortNumber.ToString(CultureInfo.InvariantCulture))
                    {
                    }
                    else
                    {
                        portName = newPortName;
                    }
                }
            }
        }
        finally
        {
            RegCloseKey(hkey);
        }

        return !string.IsNullOrEmpty(portName);
    }


    public static bool TryGetDeviceRegistryProperty(IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfoData, uint property, out string value)
    {
        value = string.Empty;
        var propertyValueChars = new char[64];
        var propertyValueLength = 63 * 2;
        if (!SetupDiGetDeviceRegistryProperty(deviceInfoSet, ref deviceInfoData, property, out var propertyDataType,
                propertyValueChars, propertyValueLength, IntPtr.Zero) || propertyDataType != (uint)RegValueType.REG_SZ) return !string.IsNullOrEmpty(value);
        value = NTString(propertyValueChars);

        return !string.IsNullOrEmpty(value);
    }


    public static IntPtr CreateResetEventOrThrow(bool manualReset)
    {
        var @event = CreateEvent(IntPtr.Zero, manualReset, false, IntPtr.Zero);
        if (@event == IntPtr.Zero) { throw new IOException("Event creation failed."); }
        return @event;
    }

    public static string NTString(char[] buffer)
    {
        var index = Array.IndexOf(buffer, '\0');
        return new string(buffer, 0, index >= 0 ? index : buffer.Length);
    }

    public static bool TryGetDeviceString(IntPtr handle, Func<IntPtr, char[], int, bool> callback, out string s)
    {
        var buffer = new char[128];
        if (!callback(handle, buffer, Marshal.SystemDefaultCharSize * buffer.Length))
        {
            s = null;
            return Marshal.GetLastWin32Error() == (int)WinError.ERROR_GEN_FAILURE;
        }
        s = NTString(buffer);
        return true;
    }

    public static ushort TryGetCaps(IntPtr handle, Func<HIDP_CAPS, ushort> callback)
    {
        if (!HidD_GetPreparsedData(handle, out var preparsed)) { return 0; }

        try
        {
            var statusCaps = HidP_GetCaps(preparsed, out var caps);
            return statusCaps == HIDP_STATUS_SUCCESS ? callback(caps) : (ushort)0;
        }
        finally
        {
            HidD_FreePreparsedData(preparsed);
        }
    }

    public static int HIDP_ERROR_CODES(int sev, ushort code)
    {
        return sev << 28 | 0x11 << 16 | code;
    }

    public static Guid HidD_GetHidGuid()
    {
        HidD_GetHidGuid(out var guid);
        return guid;
    }

    public static readonly int HIDP_STATUS_SUCCESS = HIDP_ERROR_CODES(0, 0);
}

