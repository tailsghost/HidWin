using System.Runtime.InteropServices;
using HidWin.Exceptions;
using HidWin.Natives;
using Microsoft.Win32.SafeHandles;

namespace HidWin.Streams;

public abstract unsafe class DeviceStream : Stream
{
    private bool _disposed;

    public override bool CanRead { get; } = false;
    public override bool CanWrite { get; } = false;
    public override bool CanSeek { get; } = false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() { }

    public override int Read(byte[] buffer, int offset, int count) =>
        OverlappedReadAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        OverlappedReadAsync(buffer, offset, count, cancellationToken);

    public override void Write(byte[] buffer, int offset, int count) =>
        OverlappedWriteAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        OverlappedWriteAsync(buffer, offset, count, cancellationToken);

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    protected SafeFileHandle Handle { get; init; }

    protected Task<int> OverlappedReadAsync(byte[] buffer, int offset, int count, CancellationToken token)
    {
        Throw.If.Null(buffer, $"{nameof(buffer)} null");
        if (offset < 0 || count < 0 || offset + count > buffer?.Length) throw new AggregateException();

        var handle = Handle;
        Throw.Handle.Invalid(handle, nameof(handle));

        var usedBuffer = (offset == 0) ? buffer : new byte[count];

        var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        ThreadPoolBoundHandle? bound = null;
        NativeOverlapped* overlapped = null;
        CancellationTokenRegistration registration = default;
        CancellationTokenSource cts = null;
        try
        {
            bound = ThreadPoolBoundHandle.BindHandle(handle);

            IOCompletionCallback callback = (errorCode, numBytes, pOv) =>
            {
                try
                {
                    if (errorCode != 0)
                    {
                        if (errorCode == NativeMethods.ERROR_OPERATION_ABORTED)
                            tcs.TrySetException(new IOException("Read cancelled / timeout"));
                        else
                            tcs.TrySetException(new IOException($"Read failed with error code {errorCode}"));
                    }
                    else
                        tcs.TrySetResult((int)numBytes);
                }
                finally
                {
                    try
                    {
                        bound.FreeNativeOverlapped(pOv);
                    }
                    catch
                    {
                        //ignored
                    }
                }
            };

            overlapped = bound.AllocateNativeOverlapped(callback, null, usedBuffer);

            if (!NativeMethods.ReadFile(handle, usedBuffer, (uint)count, out var bytesRead, overlapped))
            {
                var error = Marshal.GetLastWin32Error();
                if (error != NativeMethods.ERROR_IO_PENDING)
                {
                    bound.FreeNativeOverlapped(overlapped);
                    throw new IOException($"ReadFile failed with error code {error}");
                }
            }
            else
            {
                bound.FreeNativeOverlapped(overlapped);
                overlapped = null;
                if (offset != 0 && bytesRead > 0) Buffer.BlockCopy(usedBuffer, 0, buffer, offset, (int)bytesRead);
                return Task.FromResult((int)bytesRead);
            }


            cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            if (ReadTimeout > 0) cts.CancelAfter(ReadTimeout);

            registration = cts.Token.Register(() =>
            {
                try
                {
                    NativeMethods.CancelIoEx(handle, overlapped);
                }
                catch
                {
                    // ignored
                }
            });

            tcs.Task.ContinueWith(task =>
            {
                try
                {
                    registration.Dispose();
                }
                catch
                {
                    // ignored
                }

                try
                {
                    cts.Dispose();
                }
                catch
                {
                    // ignored
                }
                try
                {
                    bound?.Dispose();
                }
                catch
                {
                    // ignored
                }
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

            return tcs.Task;

        }
        catch (Exception ex)
        {
            try { if (overlapped != null) bound?.FreeNativeOverlapped(overlapped); }
            catch
            {
                //ignored
            }
            try { bound?.Dispose(); }
            catch
            {
                //ignored
            }
            try { cts?.Dispose(); }
            catch
            {
                //ignored
            }
            registration.Dispose();
            return Task.FromException<int>(ex);
        }
    }

    protected Task OverlappedWriteAsync(byte[] buffer, int offset, int count, CancellationToken ct)
    {
        Throw.If.Null(buffer, $"{nameof(buffer)} null");
        if (offset < 0 || count < 0 || offset + count > buffer.Length) throw new ArgumentOutOfRangeException();

        var handle = Handle;
        Throw.Handle.Invalid(handle, nameof(handle));

        var usedBuffer = (offset == 0) ? buffer : new byte[count];
        if (offset != 0) Buffer.BlockCopy(buffer, offset, usedBuffer, 0, count);

        var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        ThreadPoolBoundHandle bound = null;
        NativeOverlapped* nativeOv = null;
        CancellationTokenSource linkedCts = null;
        CancellationTokenRegistration registration = default;

        try
        {
            bound = ThreadPoolBoundHandle.BindHandle(handle);

            IOCompletionCallback callback = (errorCode, numBytes, pOv) =>
            {
                try
                {
                    if (errorCode != 0)
                    {
                        if (errorCode == NativeMethods.ERROR_OPERATION_ABORTED)
                            tcs.TrySetException(new IOException("Write cancelled / timeout"));
                        else
                            tcs.TrySetException(new IOException($"WriteFile failed: {errorCode}"));
                    }
                    else
                    {
                        tcs.TrySetResult((int)numBytes);
                    }
                }
                finally
                {
                    try { bound.FreeNativeOverlapped(pOv); }
                    catch
                    {
                        //ignored
                    }
                }
            };

            nativeOv = bound.AllocateNativeOverlapped(callback, null, usedBuffer);

            if (!NativeMethods.WriteFile(handle, usedBuffer, (uint)count, out var bytesWritten, nativeOv))
            {
                var err = Marshal.GetLastWin32Error();
                if (err != NativeMethods.ERROR_IO_PENDING)
                {
                    bound.FreeNativeOverlapped(nativeOv);
                    nativeOv = null;
                    try { bound.Dispose(); }
                    catch
                    {
                        //ignored
                    }
                    return Task.FromException(new IOException($"WriteFile failed immediate, error {err}"));
                }
            }
            else
            {
                bound.FreeNativeOverlapped(nativeOv);
                nativeOv = null;
                try { bound.Dispose(); }
                catch
                {
                    //ignored
                }
                return Task.CompletedTask;
            }


            linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            if (WriteTimeout > 0) linkedCts.CancelAfter(WriteTimeout);

            registration = linkedCts.Token.Register(() =>
            {
                try
                {
                    NativeMethods.CancelIoEx(handle, nativeOv);
                }
                catch
                {
                    // ignored
                }
            });

            tcs.Task.ContinueWith(t =>
            {
                try { registration.Dispose(); }
                catch
                {
                    //ignored
                }
                try { linkedCts.Dispose(); }
                catch
                {
                    //ignored
                }
                try { bound?.Dispose(); }
                catch
                {
                    //ignored
                }
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

            return tcs.Task;
        }
        catch (Exception ex)
        {
            try { if (nativeOv != null) {  bound?.FreeNativeOverlapped(nativeOv); } }
            catch
            {
                //ignored
            }
            try { bound?.Dispose(); }
            catch
            {
                //ignored
            }
            try { linkedCts?.Dispose(); }
            catch
            {
                //ignored
            }
            registration.Dispose();
            return Task.FromException(ex);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if(_disposed) return;
        if (disposing)
        {
            try
            {
                Handle.Dispose();
            }
            catch
            {
                // ignored
            }
        }
        base.Dispose(disposing);
        _disposed = true;
    }
}

