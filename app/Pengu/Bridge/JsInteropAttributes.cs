namespace Pengu.Bridge;

/// <summary>
/// Marks a partial class as a bridge endpoint exposed to JavaScript. The class
/// is registered on a <see cref="JsBridge"/> via its <see cref="GlobalName"/>
/// and surfaced in the renderer as <c>window.pengu.&lt;globalName&gt;</c> by
/// the JS shim (see <see cref="JsBridgeShim"/>).
///
/// <para>The source generator in <c>Pengu.Gen</c> emits an
/// <see cref="IJsInteropDispatcher"/> implementation for every annotated class.
/// Methods on the class are exposed via <see cref="JsInvokableAttribute"/>.</para>
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class JsInteropAttribute : Attribute
{
    public string GlobalName { get; }
    public JsInteropAttribute(string globalName) => GlobalName = globalName;
}

/// <summary>
/// Marks a method as callable from JavaScript via the bridge. The JS-side name
/// defaults to a camelCase form of the C# method name unless
/// <paramref name="name"/> is set.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class JsInvokableAttribute : Attribute
{
    public string? Name { get; }
    public JsInvokableAttribute(string? name = null) => Name = name;
}
