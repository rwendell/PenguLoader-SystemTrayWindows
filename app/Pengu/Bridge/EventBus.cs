namespace Pengu.Bridge;

/// <summary>
/// Process-wide pub/sub for C#-originated events that fan out to every live
/// <see cref="JsBridge"/>. Decouples publishers (RCS daemon, activation
/// actions, future update poller) from subscribers (per-window bridges)
/// so the daemon doesn't need a direct reference to the bridge instance,
/// and so adding a second window in the future just adds another subscriber.
///
/// <para>Wire shape: publishers call <see cref="Emit"/> with the event name
/// and an optional JSON object literal as <see cref="string"/>. Each
/// subscriber receives both — a typical subscriber turns it into a
/// <c>chrome.webview.postMessage({event, ...payload})</c> envelope so the
/// JS shim can dispatch it as a <c>CustomEvent</c> on <c>window</c>.</para>
///
/// <para>Subscribers are invoked synchronously on the calling thread of
/// <see cref="Emit"/>. The RCS daemon publishes from its socket thread, so
/// subscribers (the bridge) need to marshal back to the UI thread before
/// touching WebView2 — <see cref="JsBridge"/> does that internally.</para>
/// </summary>
public sealed class EventBus
{
    private readonly object _lock = new();
    private readonly List<Action<string, string?>> _subscribers = new();

    /// <summary>
    /// Subscribe to all events. The returned <see cref="IDisposable"/>
    /// unsubscribes on Dispose; subscribers shouldn't capture state that
    /// outlives the bus or skip the dispose path.
    /// </summary>
    public IDisposable Subscribe(Action<string, string?> handler)
    {
        lock (_lock) _subscribers.Add(handler);
        return new Subscription(this, handler);
    }

    /// <summary>
    /// Publish an event. <paramref name="payloadJson"/> must be either null
    /// or a JSON object literal (e.g. <c>"{\"active\":true}"</c>); the
    /// bridge subscriber splices its properties into the postMessage
    /// envelope. Non-object payloads are rejected at the bridge boundary.
    /// </summary>
    public void Emit(string name, string? payloadJson = null)
    {
        Action<string, string?>[] snapshot;
        lock (_lock) snapshot = _subscribers.ToArray();

        foreach (var s in snapshot)
        {
            try { s(name, payloadJson); }
            catch (Exception ex) { Pengu.Logging.Log.Error(ex, "EventBus subscriber threw on '{0}'", name); }
        }
    }

    private sealed class Subscription : IDisposable
    {
        private EventBus? _bus;
        private readonly Action<string, string?> _handler;

        public Subscription(EventBus bus, Action<string, string?> handler)
        { _bus = bus; _handler = handler; }

        public void Dispose()
        {
            var bus = _bus;
            if (bus is null) return;
            lock (bus._lock) bus._subscribers.Remove(_handler);
            _bus = null;
        }
    }
}
