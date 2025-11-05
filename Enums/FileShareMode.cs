using System.ComponentModel;

namespace HidWin.Enums;

[Flags]
public enum FileShareMode
{
    NONE = 0x00000000,
    FILE_SHARE_READ = 0x00000001,
    FILE_SHARE_WRITE = 0x00000002,
    FILE_SHARE_DELETE = 0x00000004,
    ALL = FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE
}

