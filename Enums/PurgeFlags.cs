namespace HidWin.Enums;

[Flags]
internal enum PurgeFlags : uint
{
    PURGE_TXABORT = 1,
    PURGE_RXABORT = 2,
    PURGE_TXCLEAR = 4,
    PURGE_RXCLEAR = 8
}

