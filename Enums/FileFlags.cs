namespace HidWin.Enums;

[Flags]
internal enum FileFlags : uint
{
    NONE = 0x00000000,
    FILE_FLAG_DEVICE = 0x00000040,
    FILE_FLAG_OVERLAPPED = 0x40000000
}

