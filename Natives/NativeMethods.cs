using HidWin.Enums;
using Microsoft.VisualBasic;
using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace HidWin.Natives;

public static class NativeMethods
{

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
    internal struct SP_DEVICE_INTERFACE_DATA
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

    [StructLayout(LayoutKind.Sequential)]
    internal struct SP_DEVICE_INTERFACE_DETAIL_DATA
    {
        public int cbSize;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string DevicePath;
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
    internal static extern unsafe bool ReadFile(
        SafeFileHandle hFile,
        byte[] lpBuffer,
        uint nNumberOfBytesToRead,
        out uint lpNumberOfBytesRead,
        NativeOverlapped* lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern unsafe bool WriteFile(
        SafeFileHandle hFile,
        byte[] lpBuffer,
        uint nNumberOfBytesToWrite,
        out uint lpNumberOfBytesWritten,
        NativeOverlapped* lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern unsafe bool CancelIoEx(
        SafeFileHandle hFile,
        NativeOverlapped* lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public unsafe static extern bool ReadFile(IntPtr handle, byte* buffer, int bytesToRead,
        IntPtr bytesRead, NativeOverlapped* overlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public unsafe static extern bool WriteFile(IntPtr handle, byte* buffer, int bytesToWrite,
        IntPtr bytesWritten, NativeOverlapped* overlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CancelIo(IntPtr handle);

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

    [DllImport("hid.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern unsafe bool HidD_GetFeature(IntPtr handle, byte* buffer, int bufferLength);

    [DllImport("hid.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern unsafe bool HidD_SetFeature(IntPtr handle, byte* buffer, int bufferLength);

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
    internal static extern bool SetupDiEnumDeviceInterfaces(IntPtr hDevInfo, IntPtr devInfo, ref Guid interfaceGuid,
        uint memberIndex, out SP_DEVICE_INTERFACE_DATA deviceInterfaceData);


    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
    internal static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr hDevInfo,
        ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData, IntPtr deviceInterfaceDetailData,
        int deviceInterfaceDetailSize, out int requiredSize, IntPtr deviceInfoData);

    [DllImport("setupapi.dll", SetLastError = true)]
    internal static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern unsafe uint WaitForMultipleObjects(uint count, IntPtr* handles,
        [MarshalAs(UnmanagedType.Bool)] bool waitAll, uint milliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern unsafe bool GetOverlappedResult(IntPtr handle,
        NativeOverlapped* overlapped, out uint bytesTransferred,
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


    public static unsafe void OverlappedOperation(IntPtr ioHandle,
        IntPtr eventHandle, int eventTimeout, IntPtr closeEventHandle,
        bool overlapResult,
        NativeOverlapped* overlapped, out uint bytesTransferred)
    {
        var closed = false;

        WinError win32Error = 0;

        if (!overlapResult)
        {
            win32Error = (WinError)Marshal.GetLastWin32Error();
            if (win32Error != WinError.ERROR_IO_PENDING)
            {
                var ex = new Win32Exception();
                throw new IOException($"Operation failed early: {ex.Message}", ex);
            }

            var handles = stackalloc IntPtr[2];
            handles[0] = eventHandle; handles[1] = closeEventHandle;
            var waitResult = WaitForMultipleObjects(2, handles, false, WaitForMultipleObjectsGetTimeout(eventTimeout));
            switch ((WaitObject)waitResult)
            {
                case WaitObject.WAIT_OBJECT_0: break;
                case WaitObject.WAIT_OBJECT_1: closed = true; goto default;
                default: CancelIo(ioHandle); break;
            }
        }

        if (GetOverlappedResult(ioHandle, overlapped, out bytesTransferred, true)) return;

        win32Error = (WinError)Marshal.GetLastWin32Error();
        if (win32Error != WinError.ERROR_HANDLE_EOF)
        {
            if (closed)
            {
                throw new ObjectDisposedException("Closed.");
            }

            if (win32Error == WinError.ERROR_OPERATION_ABORTED)
            {
                throw new TimeoutException("Operation timed out.");
            }

            throw new IOException("Operation failed after some time.", new Win32Exception());
        }

        bytesTransferred = 0;
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

    public static ushort TryGetCaps(IntPtr handle, Func<NativeMethods.HIDP_CAPS, ushort> callback)
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

    public static readonly int HIDP_STATUS_SUCCESS = HIDP_ERROR_CODES(0, 0);
}

