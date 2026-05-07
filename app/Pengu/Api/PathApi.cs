using Pengu.Bridge;

namespace Pengu.Api;

/// <summary>
/// Path manipulation escape hatches exposed as <c>window.pengu.path</c>.
/// Wire-shape note: the bridge envelope's <c>args</c> array carries each
/// JS-level argument as a separate element, so to support variable-arity
/// joins we accept a single <c>string[]</c> rather than <c>params</c>.
/// JS callers spread or pre-build the array: <c>pengu.path.join(['a','b'])</c>.
/// </summary>
[JsInterop("path")]
public partial class PathApi
{
    [JsInvokable]
    public Task<string> Join(string[] parts)
    {
        if (parts is null || parts.Length == 0)
            return Task.FromResult(string.Empty);
        return Task.FromResult(System.IO.Path.Combine(parts));
    }
}
