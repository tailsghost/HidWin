namespace HidWin.Helpers;

internal class HidException : Exception
{
    public HidException(){}
    public HidException(string message) : base(message){}
    public HidException(string? message, Exception? innerException) : base(message, innerException){}
}

