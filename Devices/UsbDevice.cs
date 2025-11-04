using HidWin.Enums;

namespace HidWin.Devices;

public sealed class UsbDevice : Device
{
    public UsbDevice() : base(DeviceKind.Usb)
    {

    }
}

