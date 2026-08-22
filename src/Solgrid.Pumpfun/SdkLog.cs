namespace Solgrid.Pumpfun;

// the sdk stays dependency-free; hosts wire this to their own logger
public static class SdkLog
{
    public static Action<string>? OnTrace;

    public static void Trace(string msg) => OnTrace?.Invoke(msg);
}
