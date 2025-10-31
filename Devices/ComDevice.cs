using HidWin.Enums;

namespace HidWin.Devices;

public sealed class ComDevice() : Device(DeviceKind.Com)
{
    public string PortName { get; init; } = string.Empty;
    public int BaudRate { get; init; } = 115200;
}

