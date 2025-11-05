using Microsoft.Win32.SafeHandles;

namespace HidWin.Exceptions;

public static class Throw
{
    public static class If
    {
        public static void Null(object? obj, string paramName)
        {
            if (obj is null)
            {
                throw new ArgumentNullException(paramName);
            }
        }
    }

    public static void OutOfRange(int bufferSize, int offset, int count)
    {
        if (offset < 0 || offset > bufferSize) { throw new ArgumentOutOfRangeException("offset"); }
        if (count < 0 || count > bufferSize - offset) { throw new ArgumentOutOfRangeException("count"); }
    }

    public static void OutOfRange<T>(IList<T> buffer, int offset, int count)
    {
        If.Null(buffer, "buffer");
        OutOfRange(buffer.Count, offset, count);
    }

    public static class Handle
    {
        public static void Invalid(IntPtr handle, string paramName)
        {
            If.Null(handle, $"{paramName} null");
            if (handle == (IntPtr)(-1))
                throw new IOException(paramName);
        }
    }
}
