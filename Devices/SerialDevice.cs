using HidWin.Enums;

namespace HidWin.Devices;

public sealed class SerialDevice : Device
{
    public string DeviceId { get; init; }
    public string FileSystemName { get; init; }
    public string FriendlyName { get; init; }

    public string PortName { get; init; }

    public SerialDevice() : base(DeviceKind.Serial)
    {
    }

}

