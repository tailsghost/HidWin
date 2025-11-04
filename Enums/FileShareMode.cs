namespace HidWin.Enums;

[Flags]
internal enum FileShareMode
{
    NONE = 0x00000000,
    FILE_SHARE_READ = 0x00000001,
    FILE_SHARE_WRITE = 0x00000002
}

