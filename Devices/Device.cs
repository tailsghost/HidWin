using HidWin.Enums;
using HidWin.Streams;

namespace HidWin.Devices;

public abstract class Device
{
    private DeviceStream? _stream;

    public DeviceKind Kind { get; }
    public string DevicePath { get; init; }
    public string? Name { get; init; }
    public ushort VendorId { get; init; }
    public ushort ProductId { get; init; }

    protected Device(DeviceKind kind)
    {
        Kind = kind;
    }

    public DeviceStream Open()
    {
        if(_stream != null) return _stream;
        _stream = Kind switch
        {
            DeviceKind.Hid => new HidStream(DevicePath),
            DeviceKind.Usb => new UsbStream(DevicePath),
            DeviceKind.Com => new ComStream(DevicePath),
            _ => throw new NotSupportedException($"Device kind {Kind} is not supported.")
        };
        return _stream;
    }

    public bool TryOpen(out DeviceStream? stream)
    {
        stream = null;
        try
        {
            stream = Open();
        }
        catch
        {
            stream = null;
            return false;
        }

        return true;
    }

    public void Close()
    {
        _stream.Close();
        _stream?.Dispose();
        _stream = null;
    }

    public override string ToString() => $"{Kind} {VendorId:X4}:{ProductId:X4}";
}

