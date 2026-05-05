using HidWin.Enums;
using HidWin.Natives;
using System;
using System.Collections.Generic;
using System.Text;

namespace HidWin.Devices;

public class WinUsbDevice : Device
{
    public string SerialNumber { get; set; }
    public string ProductName { get; set; }
    public WinUsbDevice() : base(DeviceKind.WinUsb)
    {
    }
}

