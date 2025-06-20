namespace TujenMem;

public enum LogLevel
{
    None,
    Error,
    Warning,
    Debug,
};

public class Log
{
    private static LogLevel LogLevel
    {
        get
        {
            switch (TujenMem.Instance.Settings.LogLevel)
            {
                case "Debug":
                    return LogLevel.Debug;
                case "Warning":
                    return LogLevel.Warning;
                case "Error":
                    return LogLevel.Error;
                default:
                    return LogLevel.None;
            }
        }
    }
    public static void Debug(string message)
    {
        if (LogLevel < LogLevel.Debug)
            return;
        TujenMem.Instance.LogMsg($"TujenMem: {message}");
    }

    public static void Warning(string message)
    {
        if (LogLevel < LogLevel.Warning)
            return;
        TujenMem.Instance.LogMsg($"TujenMem: {message}");
    }

    public static void Error(string message)
    {
        if (LogLevel < LogLevel.Error)
            return;
        TujenMem.Instance.LogError($"TujenMem: {message}");
    }

}