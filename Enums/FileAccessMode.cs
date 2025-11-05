namespace HidWin.Enums;

[Flags]
public enum FileAccessMode : uint
{
    NONE = 0,
    GENERIC_READ = 0x80000000,
    GENERIC_WRITE = 0x40000000,
    GENERIC_EXECUTE = 0x20000000,
    ALL = 0x10000000
}

