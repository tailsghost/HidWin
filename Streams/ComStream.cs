using HidWin.Exceptions;
using HidWin.Natives;
using System.Runtime.InteropServices;
using HidWin.Enums;

namespace HidWin.Streams;

public sealed class ComStream : DeviceStream
{
    private int _opened;
    private int _closed;
    private int _refCount;

    private int _baudRate;
    private int _dataBits;
    private SerialParity _parity;
    private int _stopBits;
    private bool _settingsChanged;

    internal void HandleInitAndOpen()
    {
        _opened = 1; _refCount = 1;
    }

    public int BaudRate
    {
        get => _baudRate;
        set
        {
            if (value < 0)
            {
                throw new NotSupportedException();
            }

            if (SetValue(ref _baudRate, value))
                _settingsChanged = true;
        }
    }

    public int DataBits
    {
        get => _dataBits;
        set
        {
            if (value is < 7 or > 8)
            {
                throw new NotSupportedException();
            }

            if (SetValue(ref _dataBits, value))
                _settingsChanged = true;
        }
    }

    public SerialParity Parity
    {
        get => _parity;
        set
        {
            if (SetValue(ref _parity, value))
                _settingsChanged = true;
        }
    }

    public int StopBits
    {
        get => _stopBits;
        set
        {
            if (value is < 1 or > 2)
            {
                throw new NotSupportedException();
            }
            if (SetValue(ref _stopBits, value))
                _settingsChanged = true;
        }
    }

    public ComStream(string port)
    {
        Handle = NativeMethods.CreateFile(
            port,
            (uint)(FileAccessMode.GENERIC_READ | FileAccessMode.GENERIC_WRITE),
            (uint)FileShareMode.NONE,
            IntPtr.Zero,
            (uint)FileCreationDisposition.OPEN_EXISTING,
            (uint)(FileFlags.FILE_FLAG_DEVICE | FileFlags.FILE_FLAG_OVERLAPPED),
            IntPtr.Zero
        );
        CloseEventHandle = NativeMethods.CreateResetEventOrThrow(true);

        BaudRate = 9600;
        DataBits = 8;
        Parity = SerialParity.None;
        StopBits = 1;
        HandleInitAndOpen();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        return DeviceRead(buffer, offset, count);
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return Task.Run(() => DeviceRead(buffer, offset, count), cancellationToken);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        DeviceWrite(buffer, offset, count);
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return Task.Run(() => DeviceWrite(buffer, offset, count), cancellationToken);
    }


    protected override unsafe int DeviceRead(byte[] buffer, int offset, int count)
    {
        Throw.If.OutOfRange(buffer, offset, count);

        var @event = NativeMethods.CreateResetEventOrThrow(true);
        HandleAcquireIfOpenOrFail();
        UpdateSettings();
        try
        {
            fixed (byte* ptr = buffer)
            {
                var overlapped = stackalloc NativeOverlapped[1];
                overlapped[0].EventHandle = @event;

                NativeMethods.OverlappedOperation(Handle, @event, ReadTimeout, CloseEventHandle,
                    NativeMethods.ReadFile(Handle, ptr + offset, count, IntPtr.Zero, overlapped),
                    overlapped, out var bytesTransferred);
                return (int)bytesTransferred;
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
        Throw.If.OutOfRange(buffer, offset, count);

        var @event = NativeMethods.CreateResetEventOrThrow(true);

        HandleAcquireIfOpenOrFail();
        UpdateSettings();
        try
        {
            fixed (byte* ptr = buffer)
            {
                var overlapped = stackalloc NativeOverlapped[1];
                overlapped[0].EventHandle = @event;

                NativeMethods.OverlappedOperation(Handle, @event, WriteTimeout, CloseEventHandle,
                    NativeMethods.WriteFile(Handle, ptr + offset, count, IntPtr.Zero, overlapped),
                    overlapped, out var bytesTransferred);
                if (bytesTransferred != count) { throw new IOException("Write failed."); }
            }
        }
        finally
        {
            HandleRelease();
            NativeMethods.CloseHandle(@event);
        }
    }

    void UpdateSettings()
    {

        if (!_settingsChanged) { return; }
        _settingsChanged = false;

        var dcb = new NativeMethods.DCB
        {
            DCBlength = Marshal.SizeOf(typeof(NativeMethods.DCB))
        };

        if (!NativeMethods.GetCommState(Handle, ref dcb))
        {
            throw new IOException("Failed to get serial state.", Marshal.GetHRForLastWin32Error());
        }

        SetDcb(ref dcb);
        dcb.BaudRate = checked((uint)BaudRate);
        dcb.ByteSize = checked((byte)DataBits);
        dcb.Parity = Parity == SerialParity.Even ? (byte)Enums.Parity.EVENPARITY : Parity == SerialParity.Odd ? (byte)Enums.Parity.ODDPARITY : (byte)Enums.Parity.NOPARITY;
        dcb.StopBits = StopBits == 2 ? (byte)Enums.StopBits.TWOSTOPBITS : (byte)Enums.StopBits.ONESTOPBIT;
        if (!NativeMethods.SetCommState(Handle, ref dcb))
        {
            throw new IOException("Failed to get serial state.", Marshal.GetHRForLastWin32Error());
        }

        var purgeFlags = PurgeFlags.PURGE_RXABORT | PurgeFlags.PURGE_RXCLEAR | PurgeFlags.PURGE_TXABORT | PurgeFlags.PURGE_TXCLEAR;
        if (NativeMethods.PurgeComm(Handle, (uint)purgeFlags)) return;

        throw new IOException("Failed to purge serial port.", Marshal.GetHRForLastWin32Error());


    }

    private void SetDcb(ref NativeMethods.DCB dcb)
    {
        dcb.fFlags = 0;
        dcb.fBinary = true;
    }

    internal bool HandleClose()
    {
        return 0 == Interlocked.CompareExchange(ref _closed, 1, 0) && _opened != 0;
    }


    protected override void Dispose(bool disposing)
    {
        if (!HandleClose()) { return; }

        NativeMethods.SetEvent(CloseEventHandle);
        HandleRelease();

        base.Dispose(disposing);
    }

    internal void HandleAcquireIfOpenOrFail()
    {
        if (_closed != 0 || !HandleAcquire()) { throw new ObjectDisposedException("Closed."); }
    }

    private bool HandleAcquire()
    {
        while (true)
        {
            var refCount = _refCount;
            if (refCount == 0) { return false; }

            if (refCount == Interlocked.CompareExchange
                    (ref _refCount, refCount + 1, refCount))
            {
                return true;
            }
        }
    }

    private void HandleRelease()
    {
        if (0 != Interlocked.Decrement(ref _refCount)) return;
        if (_opened == 0) return;
        NativeMethods.CloseHandle(Handle);
        NativeMethods.CloseHandle(CloseEventHandle);
    }
}

