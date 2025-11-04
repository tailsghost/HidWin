using HidWin.Enums;
using HidWin.Exceptions;
using HidWin.Natives;

namespace HidWin.Streams;

public sealed class UsbStream : DeviceStream
{
    public UsbStream(string path)
    {
        Handle = NativeMethods.CreateFile(path,
            (uint)(FileAccessMode.GENERIC_READ | FileAccessMode.GENERIC_WRITE),
            (uint)(FileShareMode.FILE_SHARE_READ | FileShareMode.FILE_SHARE_WRITE),
            IntPtr.Zero,
            (uint)FileCreationDisposition.OPEN_EXISTING,
            (uint)FileFlags.FILE_FLAG_OVERLAPPED,
            IntPtr.Zero);

        CloseEventHandle = NativeMethods.CreateResetEventOrThrow(true);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        throw new NotImplementedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotImplementedException();
    }

    protected override unsafe int DeviceRead(byte[] buffer, int offset, int count)
    {
        throw new NotImplementedException();
    }

    protected override unsafe void DeviceWrite(byte[] buffer, int offset, int count)
    {
        throw new NotImplementedException();
    }
}

