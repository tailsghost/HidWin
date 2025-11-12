using HidWin.Enums;
using HidWin.Exceptions;
using HidWin.Natives;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace HidWin.Streams;

public sealed class HidStream : DeviceStream
{
    private object _readSync = new();
    private object _writeSync = new();
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

    public void GetFeature(byte[] buffer, int offset, int count)
    {
        Throw.OutOfRange(buffer, offset, count);
        HandleAcquireIfOpenOrFail();
        try
        {
            var gch = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                var basePtr = gch.AddrOfPinnedObject();
                var ptr = IntPtr.Add(basePtr, offset);

                if (NativeMethods.HidD_GetFeature(Handle, ptr, count)) return;
                var err = Marshal.GetLastWin32Error();
                throw new IOException($"{nameof(GetFeature)} failed. Err={err}");
            }
            finally
            {
                gch.Free();
            }
        }
        finally
        {
            HandleRelease();
        }
    }

    public void SetFeature(byte[] buffer, int offset, int count)
    {
        Throw.OutOfRange(buffer, offset, count);

        HandleAcquireIfOpenOrFail();
        try
        {
            var gch = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                var basePtr = gch.AddrOfPinnedObject();
                var ptr = IntPtr.Add(basePtr, offset);

                if (NativeMethods.HidD_SetFeature(Handle, ptr, count)) return;

                var err = Marshal.GetLastWin32Error();
                throw new IOException($"{nameof(SetFeature)} failed. Err={err}", new Win32Exception(err));
            }
            finally
            {
                gch.Free();
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

    protected override int DeviceRead(byte[] buffer, int offset, int count)
    {
        Throw.OutOfRange(buffer, offset, count);

        var evt = NativeMethods.CreateResetEventOrThrow(true);
        var pOverlapped = IntPtr.Zero;
        GCHandle? pinned = null;

        HandleAcquireIfOpenOrFail();

        try
        {
            lock (_readSync)
            {
                var minIn = MaxInputReportLength();
                if (minIn <= 0)
                    throw new IOException("Can't read from this device.");

                if (minIn > count)
                    throw new IOException($"Buffer too small for device minimum report length ({minIn}).");

                pinned = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                var bufPtr = IntPtr.Add(pinned.Value.AddrOfPinnedObject(), offset);

                pOverlapped = AllocAndInitOverlapped(evt);

                var initialResult = NativeMethods.ReadFile(Handle, bufPtr, count, IntPtr.Zero, pOverlapped);

                NativeMethods.OverlappedOperation(Handle, evt, ReadTimeout, CloseEventHandle,
                    initialResult, pOverlapped, out var transferred);

                return (int)Math.Min(transferred, (uint)count);
            }
        }
        finally
        {
            HandleRelease();

            if (pOverlapped != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(pOverlapped);
            }

            if (pinned.HasValue && pinned.Value.IsAllocated)
            {
                pinned.Value.Free();
            }

            NativeMethods.CloseHandle(evt);
        }
    }

    protected override void DeviceWrite(byte[] buffer, int offset, int count)
    {
        Throw.OutOfRange(buffer, offset, count);

        var evt = NativeMethods.CreateResetEventOrThrow(true);
        var pOverlapped = IntPtr.Zero;
        GCHandle? pinned = null;

        HandleAcquireIfOpenOrFail();

        try
        {
            lock (_writeSync)
            {
                var minOut = MaxInputReportLength();
                if (minOut <= 0)
                    throw new IOException("Can't write to this device.");

                var want = Math.Max(count, minOut);
                if (buffer.Length - offset < want)
                    throw new ArgumentException("Provided buffer is too small for the requested write.");

                pinned = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                var basePtr = pinned.Value.AddrOfPinnedObject();

                var remaining = (count < minOut) ? minOut : count;
                var writeOffset = 0;

                while (remaining > 0)
                {
                    var chunk = Math.Min(minOut, remaining);
                    var bufPtr = IntPtr.Add(basePtr, offset + writeOffset);
                    pOverlapped = AllocAndInitOverlapped(evt);

                    try
                    {
                        var initialResult = NativeMethods.WriteFile(Handle, bufPtr, chunk, IntPtr.Zero, pOverlapped);

                        NativeMethods.OverlappedOperation(Handle, evt, WriteTimeout, CloseEventHandle,
                            initialResult, pOverlapped, out var transferred);

                        remaining -= (int)transferred;
                        writeOffset += (int)transferred;
                    }
                    finally
                    {
                        if (pOverlapped != IntPtr.Zero)
                        {
                            Marshal.FreeHGlobal(pOverlapped);
                            pOverlapped = IntPtr.Zero;
                        }
                    }
                }

                if (writeOffset < count)
                    throw new IOException("Write failed: not all bytes were transferred.");
            }
        }
        finally
        {
            HandleRelease();

            if (pOverlapped != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(pOverlapped);
            }

            if (pinned.HasValue && pinned.Value.IsAllocated)
            {
                pinned.Value.Free();
            }

            NativeMethods.CloseHandle(evt);
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

