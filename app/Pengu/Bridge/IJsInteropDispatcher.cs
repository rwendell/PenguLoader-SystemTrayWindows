using System.Text.Json;

namespace Pengu.Bridge;

/// <summary>
/// Implemented by the source generator for every <c>[JsInterop]</c> partial class.
/// The implementation deserializes args into the typed parameters, invokes the
/// target method, and returns the result serialized as JSON (or null for void).
/// </summary>
public interface IJsInteropDispatcher
{
    /// <summary>The global name this dispatcher is registered under.</summary>
    string GlobalName { get; }

    /// <summary>
    /// Dispatch a bridge call. <paramref name="method"/> is the JS-side method
    /// name; <paramref name="args"/> are the deserialized JSON elements from
    /// the envelope's <c>args</c> array.
    /// </summary>
    /// <returns>JSON-serialized result, or null for void/Task.</returns>
    Task<string?> __DispatchAsync(string method, JsonElement[] args);
}
