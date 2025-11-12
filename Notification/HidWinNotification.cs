using DeviceListenerChanged;

namespace HidWin.Notification;

public static class HidWinNotification
{

    public static event Action Connect;
    public static event Action Disconnect;

    public static void AutoConnect(int vid, int pid, DevineInterface deviceInfo)
    {
        var listener = new DeviceNotificationListener(new TargetVidPid(vid, pid), deviceInfo);
        listener.DeviceMatchedConnected += Start;
        listener.DeviceMatchedDisconnected += Stop;
    }

    private static void Start()
    {
        Connect?.Invoke();
    }

    private static void Stop()
    {
        Disconnect?.Invoke();
    }
}

