using HidWin.Enums;
using HidWin.Exceptions;
using HidWin.Natives;
using HidWin.Streams;

namespace HidWin.Devices;

public sealed class HidDevice : Device
{
    public string SerialNumber => GetProperty(NativeMethods.HidD_GetSerialNumberString);
    public string ProductName => GetProperty(NativeMethods.HidD_GetProductString);
    public string Manufacturer => GetProperty(NativeMethods.HidD_GetManufacturerString);
    public ushort MaxInputReportByteLength => GetInputReportByteLength();
    public ushort MaxOutputReportByteLength => GetMaxOutputReportByteLength();
    public int MaxFeature => GetProperty(caps => caps.FeatureReportByteLength);
    public int Version { get; init; }

    public string Id { get; }

    public HidDevice(string id) : base(DeviceKind.Hid)
    {
        Id = id;
    }

    public override DeviceStream Open()
    {
        if (base.Open() is not HidStream hid) 
            throw new IOException("Stream не является HidStream!");

        hid.MaxInputReportLength += GetInputReportByteLength;
        hid.MaxOutputReportLength += GetMaxOutputReportByteLength;
        return hid;

    }

    public override void Close()
    {
        if (Stream is HidStream hid)
        {
            hid.MaxInputReportLength -= GetInputReportByteLength;
            hid.MaxOutputReportLength += GetMaxOutputReportByteLength;
        }

        base.Close();
    }

    private string GetProperty(Func<IntPtr, char[], int, bool> callback)
    {
        if (Stream != null && Stream.TryGetString(callback, out var value))
        {
            return value;
        }
        else
        {
            var handle = IntPtr.Zero;

            try
            {
                handle = NativeMethods.CreateFileFromDevice(DevicePath, FileAccessMode.NONE,
                    FileShareMode.FILE_SHARE_READ | FileShareMode.FILE_SHARE_WRITE);

                Throw.Handle.Invalid(handle, "Unable to open device (" + DevicePath + ").");

                if (handle != IntPtr.Zero && handle.ToInt64() != -1)
                {
                    NativeMethods.TryGetDeviceString(handle, callback, out value);
                    return value;
                }
            }
            finally
            {
                if (handle != IntPtr.Zero)
                    NativeMethods.CloseHandle(handle);
            }
        }
        return string.Empty;
    }


    private ushort GetProperty(Func<NativeMethods.HIDP_CAPS, ushort> callback)
    {
        if (Stream != null && Stream.TryGetCaps(callback, out var result))
        {
            return result;
        }

        var handle = IntPtr.Zero;
        try
        {
            handle = NativeMethods.CreateFileFromDevice(DevicePath, FileAccessMode.NONE,
                FileShareMode.FILE_SHARE_READ | FileShareMode.FILE_SHARE_WRITE);

            Throw.Handle.Invalid(handle, "Unable to open device (" + DevicePath + ").");

            return NativeMethods.TryGetCaps(handle, callback);
        }
        finally
        {
            if (handle != IntPtr.Zero)
                NativeMethods.CloseHandle(handle);
        }
    }

    private ushort GetInputReportByteLength()
        => GetProperty(caps => caps.InputReportByteLength);

    private ushort GetMaxOutputReportByteLength()
        => GetProperty(caps => caps.OutputReportByteLength);
}

