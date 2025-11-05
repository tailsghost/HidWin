using HidWin.Enums;
using HidWin.Exceptions;
using HidWin.Natives;

namespace HidWin.Streams;

public sealed class UsbStream : DeviceStream
{
    public UsbStream(string path)
    {
        Handle = NativeMethods.CreateFileFromDevice(
            path,
            FileAccessMode.GENERIC_READ | FileAccessMode.GENERIC_WRITE,
            FileShareMode.FILE_SHARE_READ | FileShareMode.FILE_SHARE_WRITE
        );

        Throw.Handle.Invalid(Handle, "Unable to open USB class device (" + path + ").");

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

