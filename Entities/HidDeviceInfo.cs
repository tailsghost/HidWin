namespace HidWin.Entities;

internal sealed record HidDeviceInfo
{
    public string? DevicePath { get; init; } = string.Empty;
    public ushort VendorId { get; init; }
    public ushort ProductId { get; init; }
    public string SerialNumber { get; init; } = string.Empty;
    public ushort UsagePage { get; init; }
    public ushort Usage { get; init; }
    public ushort InputReportLength { get; init; }
    public ushort OutputReportLength { get; init; }

    public override string? ToString()
    {
        if (VendorId != 0 || ProductId != 0)
            return $"{VendorId:X4}:{ProductId:X4} - {DevicePath}";

        return DevicePath ?? base.ToString();
    }
}

