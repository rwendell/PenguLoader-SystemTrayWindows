namespace Pengu.Windows.Native;

/// <summary>
/// SynchronizationContext that posts callbacks back to the UI thread via
/// <see cref="Dispatcher.Post"/>. Installed by <see cref="Dispatcher.Run"/>
/// so <c>await</c> resumes on the UI thread without explicit
/// <c>ConfigureAwait(true)</c> at every call site.
/// </summary>
internal sealed class DispatcherSynchronizationContext : SynchronizationContext
{
    private readonly Dispatcher _dispatcher;

    public DispatcherSynchronizationContext(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public override void Post(SendOrPostCallback d, object? state)
    {
        _dispatcher.Post(() => d(state));
    }

    public override void Send(SendOrPostCallback d, object? state)
    {
        if (_dispatcher.CheckAccess())
        {
            d(state);
            return;
        }
        // Synchronous cross-thread send — block the caller until the UI
        // thread runs the callback. Used rarely; .NET prefers Post.
        using var done = new ManualResetEventSlim(false);
        Exception? capturedEx = null;
        _dispatcher.Post(() =>
        {
            try { d(state); }
            catch (Exception ex) { capturedEx = ex; }
            finally { done.Set(); }
        });
        done.Wait();
        if (capturedEx is not null) throw capturedEx;
    }

    public override SynchronizationContext CreateCopy() => this;
}
