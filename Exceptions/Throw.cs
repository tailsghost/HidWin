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

    public static class Handle
    {
        public static void Invalid(SafeFileHandle handle, string paramName)
        {
            If.Null(handle, $"{paramName} null");
            if (handle.IsInvalid)
                throw new ObjectDisposedException($"{paramName} invalid");
        }
    }
}
