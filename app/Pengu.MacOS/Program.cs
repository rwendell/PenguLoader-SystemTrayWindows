using AppKit;
using Foundation;
using Pengu;
using Pengu.Logging;

namespace Pengu.MacOS;

internal static class Program
{
    private static int Main(string[] args)
    {
        AppEnv.ParseCommandLine(args);
        Log.Initialize(AppContext.BaseDirectory, AppEnv.Verbose);

        try
        {
            // NSApplication.Init wires up the AppKit runtime and is required
            // before any AppKit symbol is touched. Once initialised, we set
            // a Regular activation policy (Dock icon, can become foreground)
            // and hand control to the NSApp run loop. The AppDelegate routes
            // applicationDidFinishLaunching into AppHost.RunAsync.
            NSApplication.Init();

            var app = NSApplication.SharedApplication;
            app.Delegate = new AppDelegate();
            app.ActivationPolicy = NSApplicationActivationPolicy.Regular;
            app.Run();

            return 0;
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
