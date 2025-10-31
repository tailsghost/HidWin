using HidWin.Enums;

namespace HidWin.Devices;

public sealed class HidDevice() : Device(DeviceKind.Hid)
{
    public string SerialNumber { get; init; } = string.Empty;
    public ushort Usage { get; init; }
    public ushort UsagePage { get; init; }
    public ushort InputReportByteLength { get; init; }
    public ushort OutputReportByteLength { get; init; }
}

