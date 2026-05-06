using HidWin.Enums;
using HidWin.Exceptions;
using HidWin.Natives;
using System.Runtime.InteropServices;
using static HidWin.Natives.NativeMethods;

namespace HidWin.Streams;

public class WinUsbStream : DeviceStream
{
    private const uint PIPE_TRANSFER_TIMEOUT = 0x03;
    public override int ReadTimeout
    {
        get => field;
        set
        {
            if(field == value)
                return;
            if (WinUsbHandle == IntPtr.Zero)
                return;
            field = value;
            SetReadTimeout(WinUsbHandle, InPipeId, (uint)field);
        }
    }

    public override int WriteTimeout
    {
        get => field;
        set
        {
            if (field == value)
                return;
            if (WinUsbHandle == IntPtr.Zero)
                return;
            field = value;
            SetReadTimeout(WinUsbHandle, OutPipeId, (uint)field);
        }
    }
    private byte InPipeId { get; set; }
    private byte OutPipeId { get; set; }

    public WINUSB_PIPE_INFORMATION InPipe { get; private set; }
    public WINUSB_PIPE_INFORMATION OutPipe { get; private set; }

    public IntPtr WinUsbHandle { get; private set; }

    public WinUsbStream(string port)
    {
        Handle = NativeMethods.CreateFileFromDevice(
            port,
            FileAccessMode.GENERIC_WRITE | FileAccessMode.GENERIC_READ,
            FileShareMode.FILE_SHARE_READ | FileShareMode.FILE_SHARE_WRITE);

        CloseEventHandle = NativeMethods.CreateResetEventOrThrow(true);

        Throw.Handle.Invalid(Handle, "Unable to open COM class device (" + port + ").");
        if (!NativeMethods.WinUsb_Initialize(Handle, out IntPtr handle))
            throw new Exception("WinUsb_Initialize failed");
        WinUsbHandle = handle;
        QueryPipes();
        ReadTimeout = 3000;
        WriteTimeout = 3000;
    }

    private void SetReadTimeout(IntPtr handle, byte pipeId, uint timeoutMs)
    {
        uint value = timeoutMs;

        if (!WinUsb_SetPipePolicy(
                handle,
                pipeId,
                PIPE_TRANSFER_TIMEOUT,
                sizeof(uint),
                ref value))
        {
            throw new System.ComponentModel.Win32Exception();
        }
    }

    private void QueryPipes()
    {
        USB_INTERFACE_DESCRIPTOR desc;

        if (!NativeMethods.WinUsb_QueryInterfaceSettings(
                WinUsbHandle,
                0,
                out desc))
        {
            throw new Exception("WinUsb_QueryInterfaceSettings failed");
        }

        Console.WriteLine($"Endpoints count: {desc.bNumEndpoints}");

        InPipeId = 0;
        OutPipeId = 0;

        for (byte i = 0; i < desc.bNumEndpoints; i++)
        {
            WINUSB_PIPE_INFORMATION pipe;

            if (!WinUsb_QueryPipe(
                    WinUsbHandle,
                    0,
                    i,
                    out pipe))
            {
                throw new Exception("WinUsb_QueryPipe failed");
            }

            var isIn = (pipe.PipeId & 0x80) != 0;
            var isOut = !isIn;

            Console.WriteLine($"Pipe: 0x{pipe.PipeId:X2}  IN:{isIn} OUT:{isOut}");

            if (isIn && InPipeId == 0)
            {
                InPipeId = pipe.PipeId;
                InPipe = pipe;
            }

            if (!isOut || OutPipeId != 0) continue;
            OutPipeId = pipe.PipeId;
            OutPipe = pipe;
        }

        if (InPipeId == 0 || OutPipeId == 0)
            throw new Exception("Не найдены IN/OUT endpoints");
    }

    public override bool IsValidHandle => WinUsbHandle != IntPtr.Zero && WinUsbHandle.ToInt64() != -1;

    public override int Read(byte[] buffer, int offset, int count)
    {
        return !WinUsb_ReadPipe(
            WinUsbHandle,
            InPipeId,
            buffer,
            buffer.Length,
            out var read,
            IntPtr.Zero) ? throw new Exception("Read failed: " + Marshal.GetLastWin32Error()) : read;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        if (!WinUsb_WritePipe(
                WinUsbHandle,
                OutPipeId,
                buffer,
                buffer.Length,
                out var written,
                IntPtr.Zero))
        {
            throw new Exception("Write failed: " + Marshal.GetLastWin32Error());
        }
    }

    protected override int DeviceRead(byte[] buffer, int offset, int count)
    {
        throw new NotImplementedException();
    }

    protected override void DeviceWrite(byte[] buffer, int offset, int count)
    {
        throw new NotImplementedException();
    }

    protected override void Dispose(bool disposing)
    {
        SetEvent(CloseEventHandle);
        if (WinUsbHandle != IntPtr.Zero)
            WinUsb_Free(WinUsbHandle);
        base.Dispose(disposing);
    }
}

