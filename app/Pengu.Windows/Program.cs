using Pengu;
using Pengu.Logging;
using Pengu.Windows.Native;

namespace Pengu.Windows;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        AppEnv.ParseCommandLine(args);
        Log.Initialize(AppContext.BaseDirectory, AppEnv.Verbose);

        try
        {
            // Single-instance: if another Pengu is running, broadcast the
            // show-me message to it (handled by BorderlessWindow.WndProc) and
            // exit silently. Same UUID + RegisterWindowMessage("Pengu Loader")
            // pattern as v1.1.6, so cross-version handoff works.
            if (!SingleInstance.TryAcquire())
            {
                Log.Info("Another Pengu instance is running; signalled it and exiting");
                return 0;
            }

            try
            {
                return Dispatcher.Run(async () =>
                {
                    var host = new WindowsHost();
                    await AppHost.RunAsync(host);
                });
            }
            finally
            {
                SingleInstance.Release();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unhandled exception in Program.Main");
            return 1;
        }
        finally
        {
            Log.Info("Pengu exiting");
            Log.Shutdown();
        }
    }
}
