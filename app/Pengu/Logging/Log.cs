using System.Text;

namespace Pengu.Logging;

/// <summary>
/// Hand-rolled tiny logger. No <c>Microsoft.Extensions.Logging</c> dependency —
/// the bridge surface is small enough that <see cref="string.Format(string, object?[])"/>
/// over a per-launch file is sufficient.
///
/// <para>File is created at <c>&lt;exe_dir&gt;/logs/&lt;yyyy-MM-dd_HHmmss&gt;_&lt;pid&gt;.log</c>.
/// Console mirroring is automatic in Debug builds (which use <c>Exe</c> output),
/// silent in Release (<c>WinExe</c>, no allocated console).</para>
///
/// <para>Files older than 7 days are pruned on <see cref="Initialize"/>.
/// No rotation within a session — each launch gets its own file.</para>
/// </summary>
public static class Log
{
    private static readonly object s_lock = new();
    private static StreamWriter? s_writer;
    private static LogLevel s_minLevel = LogLevel.Information;

    public static string? LogFilePath { get; private set; }

    public static void Initialize(string baseDirectory, bool verbose = false)
    {
        s_minLevel = verbose ? LogLevel.Debug : LogLevel.Information;

        var logsDir = Path.Combine(baseDirectory, "logs");
        try
        {
            Directory.CreateDirectory(logsDir);
            PruneOldLogs(logsDir);
        }
        catch
        {
            // Logging failures must never crash the host. If we can't create the
            // dir, fall back to console-only.
            return;
        }

        var fileName = $"{DateTime.Now:yyyy-MM-dd_HHmmss}_{Environment.ProcessId}.log";
        LogFilePath = Path.Combine(logsDir, fileName);

        try
        {
            s_writer = new StreamWriter(LogFilePath, append: false, Encoding.UTF8) { AutoFlush = true };
        }
        catch
        {
            s_writer = null;
        }
    }

    public static void Shutdown()
    {
        lock (s_lock)
        {
            s_writer?.Dispose();
            s_writer = null;
        }
    }

    public static void Debug(string message, params object?[] args)   => Write(LogLevel.Debug,       null, message, args);
    public static void Info(string message, params object?[] args)    => Write(LogLevel.Information, null, message, args);
    public static void Warn(string message, params object?[] args)    => Write(LogLevel.Warning,     null, message, args);
    public static void Error(string message, params object?[] args)   => Write(LogLevel.Error,       null, message, args);
    public static void Error(Exception ex, string message, params object?[] args) => Write(LogLevel.Error, ex, message, args);

    private static void Write(LogLevel level, Exception? ex, string message, object?[] args)
    {
        if (level < s_minLevel) return;

        string formatted;
        try
        {
            formatted = args.Length == 0 ? message : string.Format(message, args);
        }
        catch (FormatException)
        {
            // Bad format string in caller — keep going with the raw template.
            formatted = message;
        }

        var line = $"{DateTime.Now:HH:mm:ss.fff} [{LevelTag(level)}] {formatted}";
        if (ex is not null)
            line += Environment.NewLine + ex;

        lock (s_lock)
        {
            try { s_writer?.WriteLine(line); } catch { /* swallow logger I/O */ }
        }

        // Console mirror — only when a console is allocated (Debug subsystem).
        if (HasConsole)
        {
            try { Console.WriteLine(line); } catch { /* swallow */ }
        }
    }

    private static string LevelTag(LogLevel l) => l switch
    {
        LogLevel.Debug       => "DBG",
        LogLevel.Information => "INF",
        LogLevel.Warning     => "WRN",
        LogLevel.Error       => "ERR",
        _                    => "???",
    };

    private static void PruneOldLogs(string logsDir)
    {
        var cutoff = DateTime.Now - TimeSpan.FromDays(7);
        foreach (var path in Directory.EnumerateFiles(logsDir, "*.log"))
        {
            try
            {
                if (File.GetLastWriteTime(path) < cutoff)
                    File.Delete(path);
            }
            catch { /* best effort */ }
        }
    }

    private static bool HasConsole
    {
        get
        {
            // IsInputRedirected is the cheapest "is there a stdin/stdout we can
            // write to" probe that doesn't allocate. Console.OpenStandardOutput
            // would work too but is heavier.
            try { _ = Console.IsInputRedirected; return true; }
            catch { return false; }
        }
    }
}

public enum LogLevel { Debug, Information, Warning, Error }
