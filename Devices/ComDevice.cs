using HidWin.Enums;
using HidWin.Natives;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace HidWin.Devices;

public sealed class ComDevice : Device
{
    public string DeviceId { get; init; }
    public string FileSystemName { get; init; }
    public string FriendlyName { get; init; }

    public string PortName { get; init; }

    public ComDevice() : base(DeviceKind.Com)
    {
    }

}

