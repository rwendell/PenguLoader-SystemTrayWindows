using System.Text;
using System.Text.Json;

namespace Pengu.Bridge;

/// <summary>
/// Per-window dispatcher that bridges <c>chrome.webview.postMessage</c> envelopes
/// to source-generated <see cref="IJsInteropDispatcher"/> implementations.
///
/// <para>Envelope from web side:  <c>{ id: number, channel: "global.method", args: [...] }</c><br/>
/// Reply on success:        <c>{ id, ok: true,  result: ... }</c><br/>
/// Reply on failure:        <c>{ id, ok: false, error: "..." }</c></para>
/// </summary>
public sealed class JsBridge
{
    private readonly IBrowserHost _browser;
    private readonly Dictionary<string, IJsInteropDispatcher> _handlers = new(StringComparer.Ordinal);
    private bool _scriptInjected;

    public JsBridge(IBrowserHost browser)
    {
        _browser = browser;
        _browser.WebMessageReceivedAsJson += OnWebMessage;
    }

    /// <summary>Register a dispatcher under its <see cref="IJsInteropDispatcher.GlobalName"/>.
    /// If the JS shim has already been injected, re-inject it so the new global
    /// is exposed on the next navigation.</summary>
    public JsBridge Register(IJsInteropDispatcher dispatcher)
    {
        _handlers[dispatcher.GlobalName] = dispatcher;
        if (_scriptInjected)
            InjectScript();
        return this;
    }

    /// <summary>Inject the JS shim. Call once after the underlying browser is
    /// ready (so <c>AddScriptToExecuteOnDocumentCreated</c> is callable). Runs
    /// on every subsequent navigation.</summary>
    public void InjectScript()
    {
        var script = BuildScript();
        _browser.AddScriptToExecuteOnDocumentCreated(script);
        _scriptInjected = true;
    }

    private string BuildScript()
    {
        var globals = string.Join(",", _handlers.Keys.Select(k => "\"" + k.Replace("\"", "\\\"") + "\""));
        return JsBridgeShim.Template.Replace("/*__GLOBALS__*/", globals);
    }

    private async void OnWebMessage(string json)
    {
        int id = -1;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("id", out var idEl) || !idEl.TryGetInt32(out id))
                return; // not a bridge call envelope

            if (!root.TryGetProperty("channel", out var channelEl))
                throw new InvalidOperationException("missing 'channel'");
            if (!root.TryGetProperty("args", out var argsEl))
                throw new InvalidOperationException("missing 'args'");

            var channel = channelEl.GetString() ?? throw new InvalidOperationException("'channel' is null");
            int dot = channel.IndexOf('.');
            if (dot < 0) throw new InvalidOperationException($"bad channel '{channel}', expected 'global.method'");

            var globalName = channel.Substring(0, dot);
            var method     = channel.Substring(dot + 1);

            if (!_handlers.TryGetValue(globalName, out var handler))
                throw new InvalidOperationException($"no bridge handler for '{globalName}'");

            int n = argsEl.GetArrayLength();
            var args = new JsonElement[n];
            int i = 0;
            // Clone each element so they survive the JsonDocument disposal.
            foreach (var a in argsEl.EnumerateArray()) args[i++] = a.Clone();

            string? resultJson;
            try
            {
                resultJson = await handler.__DispatchAsync(method, args);
            }
            catch (Exception inner)
            {
                ReplyError(id, inner.Message);
                return;
            }

            ReplyOk(id, resultJson);
        }
        catch (Exception ex)
        {
            if (id >= 0) ReplyError(id, ex.Message);
        }
    }

    private void ReplyOk(int id, string? resultJson)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartObject();
            w.WriteNumber("id", id);
            w.WriteBoolean("ok", true);
            w.WritePropertyName("result");
            if (resultJson is null) w.WriteNullValue();
            else w.WriteRawValue(resultJson);
            w.WriteEndObject();
        }
        _browser.PostWebMessageAsJson(Encoding.UTF8.GetString(ms.ToArray()));
    }

    private void ReplyError(int id, string error)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartObject();
            w.WriteNumber("id", id);
            w.WriteBoolean("ok", false);
            w.WriteString("error", error);
            w.WriteEndObject();
        }
        _browser.PostWebMessageAsJson(Encoding.UTF8.GetString(ms.ToArray()));
    }

    /// <summary>
    /// Push a C#-originated event to the renderer. The shim translates this
    /// into a <c>CustomEvent(name, {detail: payload})</c> dispatched on
    /// <c>window</c>. Use for activation state changes, update notifications,
    /// anything the renderer should react to without having issued a request.
    /// </summary>
    /// <param name="name">Event name (matches the JS-side listener).</param>
    /// <param name="payloadJson">JSON object literal whose properties become
    /// <c>e.detail.*</c> on the JS side. Pass null for an event with no payload.</param>
    public void EmitEvent(string name, string? payloadJson = null)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartObject();
            w.WriteString("event", name);
            if (payloadJson is not null)
            {
                using var doc = JsonDocument.Parse(payloadJson);
                if (doc.RootElement.ValueKind != JsonValueKind.Object)
                    throw new ArgumentException("payloadJson must be a JSON object", nameof(payloadJson));
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    w.WritePropertyName(prop.Name);
                    prop.Value.WriteTo(w);
                }
            }
            w.WriteEndObject();
        }
        _browser.PostWebMessageAsJson(Encoding.UTF8.GetString(ms.ToArray()));
    }
}
