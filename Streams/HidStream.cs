using HidWin.Devices;
using HidWin.Enums;
using HidWin.Exceptions;
using HidWin.Natives;
using System.ComponentModel;

namespace HidWin.Streams;

public sealed class HidStream : DeviceStream
{
    private int _opened;
    private int _closed;
    private int _refCount;

    private byte[] _readBuffer;
    private byte[] _writeBuffer;

    public event Func<ushort> MaxInputReportLength;
    public event Func<ushort> MaxOutputReportLength;

    public HidStream(string path)
    {
        Handle = NativeMethods.CreateFileFromDevice(
            path,
            FileAccessMode.GENERIC_READ | FileAccessMode.GENERIC_WRITE,
            FileShareMode.FILE_SHARE_READ | FileShareMode.FILE_SHARE_WRITE
        );

        Throw.Handle.Invalid(Handle, "Unable to open HID class device (" + path + ").");

        CloseEventHandle = NativeMethods.CreateResetEventOrThrow(true);

        if (!NativeMethods.HidD_SetNumInputBuffers(Handle, 512))
        {
            NativeMethods.CloseHandle(Handle);
            throw new IOException("Failed to set input buffers");
        }

        _opened = 1;
        _refCount = 1;
    }

    public unsafe void GetFeature(byte[] buffer, int offset, int count)
    {
        Throw.OutOfRange(buffer, offset, count);

        HandleAcquireIfOpenOrFail();

        try
        {
            fixed (byte* ptr = buffer)
            {
                if (!NativeMethods.HidD_GetFeature(Handle, ptr + offset, count))
                    throw new IOException($"{nameof(GetFeature)} failed");
            }
        }
        finally
        {
            HandleRelease();
        }
    }

    public unsafe void SetFeature(byte[] buffer, int offset, int count)
    {
        Throw.OutOfRange(buffer, offset, count);

        HandleAcquireIfOpenOrFail();
        try
        {
            fixed (byte* ptr = buffer)
            {
                if (!NativeMethods.HidD_SetFeature(Handle, ptr + offset, count))
                    throw new IOException("SetFeature failed.", new Win32Exception());
            }
        }
        finally
        {
            HandleRelease();
        }
    }

    public override int Read(byte[] buffer, int offset, int count)
        => DeviceRead(buffer, offset, count);

    public override void Write(byte[] buffer, int offset, int count)
        => DeviceWrite(buffer, offset, count);

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => Task.Run(() => Read(buffer, offset, count), cancellationToken);

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => Task.Run(() => Write(buffer, offset, count), cancellationToken);

    protected override unsafe int DeviceRead(byte[] buffer, int offset, int count)
    {
        Throw.OutOfRange(buffer, offset, count);

        var @event = NativeMethods.CreateResetEventOrThrow(true);

        HandleAcquireIfOpenOrFail();

        try
        {
            var minIn = MaxInputReportLength();
            if (minIn <= 0)
                throw new IOException("Can't read from this device.");
            if (_readBuffer.Length < Math.Max(count, minIn))
                Array.Resize(ref _readBuffer, Math.Max(count, minIn));

            fixed (byte* ptr = _readBuffer)
            {
                var overlapped = stackalloc NativeOverlapped[1];
                overlapped[0].EventHandle = @event;

                NativeMethods.OverlappedOperation(Handle, @event, ReadTimeout, CloseEventHandle,
                    NativeMethods.ReadFile(Handle, ptr, Math.Max(count, minIn), IntPtr.Zero, overlapped),
                    overlapped, out var bytesTransferred);

                var newCount = 0;

                if (count > (int)bytesTransferred)
                    newCount = (int)bytesTransferred;

                Array.Copy(_readBuffer, 0, buffer, offset, newCount);

                return newCount;
            }
        }
        finally
        {
            HandleRelease();
            NativeMethods.CloseHandle(@event);
        }
    }

    protected override unsafe void DeviceWrite(byte[] buffer, int offset, int count)
    {
        Throw.OutOfRange(buffer, offset, count);
        var @event = NativeMethods.CreateResetEventOrThrow(true);

        HandleAcquireIfOpenOrFail();
        try
        {

            var minOut = MaxInputReportLength();
            if (minOut <= 0)
                throw new IOException("Can't write to this device.");
            
            if (_writeBuffer.Length < Math.Max(count, minOut)) 
                Array.Resize(ref _writeBuffer, Math.Max(count, minOut));
            Array.Copy(buffer, offset, _writeBuffer, 0, count);

            var newCount = count;

            if (count < minOut)
            {
                Array.Clear(_writeBuffer, count, minOut - count);
                newCount = minOut;
            }

            fixed (byte* ptr = _writeBuffer)
            {
                var offset0 = 0;
                while (newCount > 0)
                {
                    var overlapped = stackalloc NativeOverlapped[1];
                    overlapped[0].EventHandle = @event;

                    NativeMethods.OverlappedOperation(Handle, @event, WriteTimeout, CloseEventHandle,
                        NativeMethods.WriteFile(Handle, ptr + offset0, Math.Min(minOut, count), IntPtr.Zero, overlapped),
                        overlapped, out var bytesTransferred);
                    newCount -= (int)bytesTransferred;
                    offset0 += (int)bytesTransferred;
                }
            }
        }
        finally
        {
            HandleRelease();
            NativeMethods.CloseHandle(@event);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (!HandleClose()) return;

        NativeMethods.SetEvent(CloseEventHandle);
        HandleRelease();

        base.Dispose(disposing);
    }

    private bool HandleAcquire()
    {
        while (true)
        {
            var refCount = _refCount;
            if (refCount == 0)
            {
                return false;
            }

            if (refCount == Interlocked.CompareExchange
                    (ref _refCount, refCount + 1, refCount))
            {
                return true;
            }
        }
    }

    private void HandleAcquireIfOpenOrFail()
    {
        if (_closed != 0 || !HandleAcquire())
            throw ExceptionForClosed();
    }

    private void HandleRelease()
    {
        if (0 == Interlocked.Decrement(ref _refCount)) return;

        HandleFree();
    }


    private Exception ExceptionForClosed()
    {
        return new ObjectDisposedException("Closed.");
    }

    private void HandleFree()
    {
        NativeMethods.CloseHandle(Handle);
        NativeMethods.CloseHandle(CloseEventHandle);
    }

    private bool HandleClose()
    {
        return 0 == Interlocked.CompareExchange(ref _closed, 1, 0) && _opened != 0;
    }
}

