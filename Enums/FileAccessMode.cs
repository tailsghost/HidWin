namespace HidWin.Enums;

[Flags]
internal enum FileAccessMode : uint
{
    GENERIC_READ = 0x80000000,
    GENERIC_WRITE = 0x40000000
}

