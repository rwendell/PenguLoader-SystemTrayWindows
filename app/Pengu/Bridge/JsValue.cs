using System.Text.Json;

namespace Pengu.Bridge;

/// <summary>
/// Pass-through wrapper around a <see cref="JsonElement"/>. Bridge methods that
/// accept or return <see cref="JsValue"/> skip the AOT JsonSerializerContext
/// path entirely — the source generator hands the raw element through and
/// reads <see cref="GetRawText"/> for return values.
///
/// <para>Use this for methods whose argument or return shape isn't worth
/// pinning down with a typed record (debug helpers, generic config bags, etc.).
/// Prefer typed records for everything else.</para>
/// </summary>
public readonly struct JsValue
{
    public JsonElement Element { get; }

    public JsValue(JsonElement element) => Element = element;

    public string GetRawText() => Element.GetRawText();

    public JsonValueKind ValueKind => Element.ValueKind;
}
