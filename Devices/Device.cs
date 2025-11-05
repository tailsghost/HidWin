using HidWin.Enums;
using HidWin.Streams;

namespace HidWin.Devices;

public abstract class Device
{
    protected DeviceStream? Stream;

    public DeviceKind Kind { get; }
    public string DevicePath { get; init; }
    public string? Name { get; init; }
    public ushort VendorId { get; init; }
    public ushort ProductId { get; init; }

    protected Device(DeviceKind kind)
    {
        Kind = kind;
    }

    public virtual DeviceStream Open()
    {
        if(Stream != null) return Stream;
        Stream = Kind switch
        {
            DeviceKind.Hid => new HidStream(DevicePath),
            DeviceKind.Usb => new UsbStream(DevicePath),
            DeviceKind.Com => new ComStream(DevicePath),
            _ => throw new NotSupportedException($"Device kind {Kind} is not supported.")
        };
        return Stream;
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

    public virtual void Close()
    {
        Stream?.Close();
        Stream?.Dispose();
        Stream = null;
    }

    public override string ToString() => $"{Kind} {VendorId:X4}:{ProductId:X4}";
}

