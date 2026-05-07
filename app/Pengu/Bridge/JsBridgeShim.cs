namespace Pengu.Bridge;

/// <summary>
/// JS-side shim injected via <c>AddScriptToExecuteOnDocumentCreated</c>. Sets
/// up <c>window.pengu</c> as a rolled-up facade over per-namespace proxies that
/// turn property access + call into a <c>{id, channel, args}</c> postMessage
/// and resolve the returned Promise on the matching reply. The
/// <c>/*__GLOBALS__*/</c> token is replaced with a JSON array of registered
/// global names at injection time.
///
/// <para>Push events from C# arrive as <c>{event: name, ...payload}</c> and
/// are dispatched as <c>CustomEvent(name, {detail: payload})</c> on
/// <c>window</c>; subscribe via <c>window.addEventListener(name, e =&gt; ...)</c>.</para>
/// </summary>
internal static class JsBridgeShim
{
    public const string Template = """
(function () {
  if (window.__penguBridge) return;
  const pending = new Map();
  let nextId = 1;

  function invoke(channel, args) {
    return new Promise((resolve, reject) => {
      const id = nextId++;
      pending.set(id, { resolve, reject });
      window.chrome.webview.postMessage({ id, channel, args });
    });
  }

  window.chrome.webview.addEventListener('message', (e) => {
    const msg = e.data;
    if (typeof msg !== 'object' || msg === null) return;
    // Bridge reply: { id, ok, result|error }
    if (typeof msg.id === 'number') {
      const p = pending.get(msg.id);
      if (!p) return;
      pending.delete(msg.id);
      if (msg.ok) p.resolve(msg.result);
      else p.reject(new Error(msg.error || 'bridge: invoke failed'));
      return;
    }
    // C#-pushed event: { event: "name", ...payload } -> CustomEvent on window.
    if (typeof msg.event === 'string') {
      const { event: name, ...detail } = msg;
      window.dispatchEvent(new CustomEvent(name, { detail }));
    }
  });

  function makeProxy(name) {
    return new Proxy({}, {
      get(_, method) {
        if (typeof method !== 'string') return undefined;
        if (method === 'then') return undefined;        // not thenable
        return (...args) => invoke(name + '.' + method, args);
      },
    });
  }

  const globals = [/*__GLOBALS__*/];
  const pengu = {};
  for (const g of globals) pengu[g] = makeProxy(g);
  window.pengu = pengu;
  window.__penguBridge = { invoke };
})();
""";
}
