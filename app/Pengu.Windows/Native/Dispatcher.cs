using System.Collections.Concurrent;
using Vanara.PInvoke;
using static Vanara.PInvoke.Kernel32;
using static Vanara.PInvoke.User32;

namespace Pengu.Windows.Native;

/// <summary>
/// Single-threaded UI dispatcher modeled on Avalonia's Win32DispatcherImpl.
/// Drains posted continuations, then PeekMessage drains win32 messages, then
/// MsgWaitForMultipleObjectsEx blocks until either a new message arrives or
/// the wakeup event is signalled by <see cref="Post"/>.
///
/// <para>Installed as the <see cref="SynchronizationContext"/> via
/// <see cref="DispatcherSynchronizationContext"/> so <c>await</c> continuations
/// resume on the UI thread by default — same shape as WPF's Dispatcher,
/// without WPF's dependency tree.</para>
/// </summary>
public sealed class Dispatcher
{
    private static Dispatcher? s_instance;

    public static Dispatcher UIThread =>
        s_instance ?? throw new InvalidOperationException("Dispatcher.Run has not been called.");

    private readonly int _threadId = Environment.CurrentManagedThreadId;
    private readonly ConcurrentQueue<Action> _queue = new();
    private readonly SafeEventHandle _wake;
    private bool _running;
    private int _exitCode;

    private Dispatcher()
    {
        _wake = CreateEvent(null, bManualReset: false, bInitialState: false, lpName: null);
        if (_wake.IsInvalid)
            throw new InvalidOperationException("CreateEvent failed");
    }

    public bool CheckAccess() => Environment.CurrentManagedThreadId == _threadId;

    public void VerifyAccess()
    {
        if (!CheckAccess())
            throw new InvalidOperationException("Call must be on the UI thread.");
    }

    public void Post(Action work)
    {
        _queue.Enqueue(work);
        SetEvent(_wake);
    }

    public Task InvokeAsync(Action work)
    {
        if (CheckAccess())
        {
            try { work(); return Task.CompletedTask; }
            catch (Exception ex) { return Task.FromException(ex); }
        }
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Post(() =>
        {
            try { work(); tcs.SetResult(); }
            catch (Exception ex) { tcs.SetException(ex); }
        });
        return tcs.Task;
    }

    public void Exit(int code = 0)
    {
        _exitCode = code;
        _running = false;
        PostQuitMessage(code);
        SetEvent(_wake);
    }

    /// <summary>Run the dispatcher loop on the current thread.
    /// <paramref name="onStart"/> is invoked after the SynchronizationContext
    /// is installed but before the first message pump.</summary>
    public static int Run(Func<Task> onStart)
    {
        if (s_instance is not null)
            throw new InvalidOperationException("Dispatcher already running.");

        var d = new Dispatcher();
        s_instance = d;

        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(d));
        d._running = true;

        try
        {
            var startTask = onStart();
            // Surface async-init faults to the log; don't kill the loop —
            // the host may still want to keep windows alive.
            _ = startTask.ContinueWith(t =>
            {
                if (t.IsFaulted)
                    Pengu.Logging.Log.Error(t.Exception!, "onStart faulted");
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }
        catch (Exception ex)
        {
            Pengu.Logging.Log.Error(ex, "Dispatcher.Run onStart threw synchronously");
            return 1;
        }

        return d.Loop();
    }

    private int Loop()
    {
        var handles = new HANDLE[] { _wake.DangerousGetHandle() };

        while (_running)
        {
            DrainQueue();

            while (PeekMessage(out var msg, default, 0, 0, PM.PM_REMOVE))
            {
                if (msg.message == (uint)WindowMessage.WM_QUIT)
                {
                    _running = false;
                    _exitCode = (int)(long)msg.wParam;
                    DrainQueue();
                    return _exitCode;
                }
                TranslateMessage(msg);
                DispatchMessage(msg);
            }

            if (!_running) break;

            MsgWaitForMultipleObjectsEx(
                1u,
                handles,
                INFINITE,
                QS.QS_ALLINPUT,
                MWMO.MWMO_INPUTAVAILABLE);
        }

        DrainQueue();
        return _exitCode;
    }

    private void DrainQueue()
    {
        while (_queue.TryDequeue(out var work))
        {
            try { work(); }
            catch (Exception ex) { Pengu.Logging.Log.Error(ex, "Posted action threw"); }
        }
    }
}
