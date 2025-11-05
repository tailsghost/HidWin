using HidWin.Core;
using HidWin.Natives;

namespace HidWin.Streams;

public abstract unsafe class DeviceStream : Stream
{
    private bool _disposed;

    public override int ReadTimeout { get; set; }
    public override int WriteTimeout { get; set; }
    public override bool CanRead { get; } = true;
    public override bool CanWrite { get; } = true;
    public override bool CanSeek { get; } = false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() { }

    public DeviceStream()
    {
        WriteTimeout = 2000;
        ReadTimeout = 2000;
        AppDomain.CurrentDomain.ProcessExit += CurrentDomainOnProcessExit;
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();

    protected IntPtr Handle;
    protected IntPtr CloseEventHandle;

    public bool IsValidHandle => Handle != IntPtr.Zero && Handle.ToInt64() !=-1;

    public bool TryGetString(Func<IntPtr, char[], int, bool> callback, out string value)
    {
        value = string.Empty;
        return IsValidHandle && NativeMethods.TryGetDeviceString(Handle, callback, out value);
    }

    public bool TryGetCaps(Func<NativeMethods.HIDP_CAPS, ushort> callback, out ushort value)
    {
        value = 0;
        if (!IsValidHandle) return false;
        value = NativeMethods.TryGetCaps(Handle, callback);
        return true;
    }


    private void CurrentDomainOnProcessExit(object? sender, EventArgs e)
    {
        Dispose();
    }

    protected override void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            AppDomain.CurrentDomain.ProcessExit -= CurrentDomainOnProcessExit;
        }

        base.Dispose(disposing);

        _disposed = true;
    }

    protected abstract unsafe int DeviceRead(byte[] buffer, int offset, int count);
    protected abstract unsafe void DeviceWrite(byte[] buffer, int offset, int count);


    protected bool SetValue<T>(ref T property, T value)
    {
        if (property.Equals(value)) return false;
        property = value;
        return true;
    }
}

