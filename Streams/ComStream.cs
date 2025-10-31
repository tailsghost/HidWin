using HidWin.Exceptions;
using HidWin.Natives;

namespace HidWin.Streams;

public sealed class ComStream :DeviceStream
{
    public ComStream(string port)
    {
        Handle = NativeMethods.CreateFile(
            port,
            NativeMethods.GENERIC_READ | NativeMethods.GENERIC_WRITE,
            NativeMethods.FILE_SHARE_READ | NativeMethods.FILE_SHARE_WRITE,
            IntPtr.Zero,
            NativeMethods.OPEN_EXISTING,
            NativeMethods.FILE_FLAG_OVERLAPPED,
            IntPtr.Zero
        );
        Throw.Handle.Invalid(Handle, nameof(Handle));
    }
}
