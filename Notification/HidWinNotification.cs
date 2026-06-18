using DeviceListenerChanged;

namespace HidWin.Notification;

public class HidWinNotification
{

    public event Action Connect;
    public event Action Disconnect;

    public void AutoConnect(int vid, int pid, DevineInterface deviceInfo)
    {
        var listener = new DeviceNotificationListener(new TargetVidPid(vid, pid), deviceInfo);
        listener.DeviceMatchedConnected += Start;
        listener.DeviceMatchedDisconnected += Stop;
    }

    private void Start()
    {
        Connect?.Invoke();
    }

    private void Stop()
    {
        Disconnect?.Invoke();
    }
}

