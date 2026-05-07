using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Pengu.Gen;

/// <summary>
/// Incremental source generator that emits an <c>IJsInteropDispatcher</c>
/// partial for every <c>[JsInterop("name")]</c>-annotated class. The emitted
/// dispatch table reads JsonElement args, deserializes via PenguJsonContext,
/// invokes the target method, and returns JSON.
///
/// Wire format: <c>{id, channel: "global.method", args: [...]}</c>.
/// Each <c>[JsInvokable]</c> method becomes a <c>case "method":</c> in the
/// switch. Method names default to camelCase of the C# name.
///
/// Pass-through types (<c>JsValue</c>, <c>JsonElement</c>) skip serialization;
/// otherwise the generator emits <c>args[i].Deserialize&lt;T&gt;(PenguJsonContext.Default.T)</c>.
/// </summary>
[Generator(LanguageNames.CSharp)]
public class JsInteropGenerator : IIncrementalGenerator
{
    private const string ClassAttr  = "Pengu.Bridge.JsInteropAttribute";
    private const string MethodAttr = "Pengu.Bridge.JsInvokableAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var classes = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                ClassAttr,
                predicate: static (n, _) => n is ClassDeclarationSyntax,
                transform: static (ctx, _) => Extract(ctx))
            .Where(static c => c != null)
            .Collect();

        context.RegisterSourceOutput(classes, Emit);
    }

    private static ClassInfo Extract(GeneratorAttributeSyntaxContext ctx)
    {
        if (!(ctx.TargetSymbol is INamedTypeSymbol classSym)) return null;
        if (ctx.Attributes.Length == 0) return null;
        var attr = ctx.Attributes[0];
        if (attr.ConstructorArguments.Length < 1) return null;
        if (!(attr.ConstructorArguments[0].Value is string globalName)) return null;

        var ns = classSym.ContainingNamespace.IsGlobalNamespace
            ? "" : classSym.ContainingNamespace.ToDisplayString();
        var className = classSym.Name;
        var accessibility = classSym.DeclaredAccessibility == Accessibility.Public
            ? "public" : "internal";

        var methods = new List<MethodInfo>();
        foreach (var m in classSym.GetMembers().OfType<IMethodSymbol>())
        {
            var mAttr = m.GetAttributes().FirstOrDefault(a =>
                a.AttributeClass != null &&
                a.AttributeClass.ToDisplayString() == MethodAttr);
            if (mAttr == null) continue;

            var jsName = mAttr.ConstructorArguments.Length > 0
                ? mAttr.ConstructorArguments[0].Value as string
                : null;
            if (string.IsNullOrEmpty(jsName)) jsName = CamelCase(m.Name);

            var args = m.Parameters
                .Select(p => MakeTypeInfo(p.Type))
                .ToImmutableArray();

            // Unwrap Task / Task<T>.
            ITypeSymbol returnInner = null;
            bool isAsync = false;
            if (m.ReturnType is INamedTypeSymbol nt)
            {
                var ctor = nt.ConstructedFrom.ToDisplayString();
                if (ctor == "System.Threading.Tasks.Task")
                {
                    isAsync = true;
                }
                else if (ctor == "System.Threading.Tasks.Task<TResult>")
                {
                    isAsync = true;
                    returnInner = nt.TypeArguments[0];
                }
                else if (m.ReturnType.SpecialType != SpecialType.System_Void)
                {
                    returnInner = m.ReturnType;
                }
            }
            else if (m.ReturnType.SpecialType != SpecialType.System_Void)
            {
                returnInner = m.ReturnType;
            }

            var ret = returnInner == null ? null : MakeTypeInfo(returnInner);
            methods.Add(new MethodInfo(m.Name, jsName, args, ret, isAsync));
        }

        return new ClassInfo(ns, className, accessibility, globalName, methods.ToImmutableArray());
    }

    private static TypeInfo MakeTypeInfo(ITypeSymbol type)
    {
        var fullName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        // Pass-through: hand the JsonElement (or JsValue wrapper) through
        // unchanged. Lets callers opt out of typed (de)serialization for
        // generic config bags / debug helpers.
        var passthrough =
            fullName == "global::Pengu.Bridge.JsValue" ||
            fullName == "global::System.Text.Json.JsonElement";

        if (passthrough)
            return new TypeInfo(fullName, "_passthrough_", true);

        // System.Text.Json source-gen names the property for `T[]` as
        // `<TypeName>Array` (e.g. `PluginInfo[]` -> `PluginInfoArray`,
        // `string[]` -> `StringArray`). Match that convention so the
        // generated dispatcher reaches the right Default.X property.
        if (type is IArrayTypeSymbol arr)
        {
            var elem = arr.ElementType;
            string elemProp = elem.SpecialType switch
            {
                SpecialType.System_String  => "String",
                SpecialType.System_Int32   => "Int32",
                SpecialType.System_Int64   => "Int64",
                SpecialType.System_Boolean => "Boolean",
                SpecialType.System_Double  => "Double",
                SpecialType.System_Single  => "Single",
                SpecialType.System_Byte    => "Byte",
                _ => SanitizeIdentifier(elem.Name),
            };
            return new TypeInfo(fullName, elemProp + "Array", false);
        }

        string prop;
        switch (type.SpecialType)
        {
            case SpecialType.System_String:  prop = "String";  break;
            case SpecialType.System_Int32:   prop = "Int32";   break;
            case SpecialType.System_Int64:   prop = "Int64";   break;
            case SpecialType.System_Boolean: prop = "Boolean"; break;
            case SpecialType.System_Double:  prop = "Double";  break;
            case SpecialType.System_Single:  prop = "Single";  break;
            case SpecialType.System_Byte:    prop = "Byte";    break;
            default: prop = SanitizeIdentifier(type.Name); break;
        }
        return new TypeInfo(fullName, prop, false);
    }

    private static string SanitizeIdentifier(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
            sb.Append(char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_');
        return sb.ToString();
    }

    private static string CamelCase(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToLowerInvariant(s[0]) + s.Substring(1);

    // -------- emission --------

    private static void Emit(SourceProductionContext spc, ImmutableArray<ClassInfo> classes)
    {
        var concrete = classes.Where(c => c != null).ToList();
        if (concrete.Count == 0) return;
        foreach (var c in concrete)
            spc.AddSource($"{c.ClassName}.JsInterop.g.cs", EmitDispatch(c));
    }

    private static SourceText EmitDispatch(ClassInfo c)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Text.Json;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using Pengu.Bridge;");
        sb.AppendLine();
        if (!string.IsNullOrEmpty(c.Namespace))
        {
            sb.Append("namespace ").Append(c.Namespace).AppendLine(";");
            sb.AppendLine();
        }
        sb.Append(c.Accessibility).Append(" partial class ").Append(c.ClassName).AppendLine(" : IJsInteropDispatcher");
        sb.AppendLine("{");
        sb.Append("    public string GlobalName => \"").Append(c.GlobalName).AppendLine("\";");
        sb.AppendLine();
        sb.AppendLine("    async Task<string?> IJsInteropDispatcher.__DispatchAsync(string method, JsonElement[] args)");
        sb.AppendLine("    {");
        sb.AppendLine("        switch (method)");
        sb.AppendLine("        {");

        foreach (var m in c.Methods)
        {
            sb.Append("            case \"").Append(m.JsName).AppendLine("\":");
            sb.AppendLine("            {");
            for (int i = 0; i < m.ArgTypes.Length; i++)
            {
                var a = m.ArgTypes[i];
                if (a.IsPassthrough)
                {
                    sb.Append("                var arg").Append(i).Append(" = ");
                    sb.Append(a.FullName == "global::System.Text.Json.JsonElement"
                        ? $"args[{i}]"
                        : $"new {a.FullName}(args[{i}])");
                    sb.AppendLine(";");
                }
                else
                {
                    sb.Append("                var arg").Append(i)
                      .Append(" = args[").Append(i).Append("].Deserialize<")
                      .Append(a.FullName).Append(">(global::Pengu.PenguJsonContext.Default.")
                      .Append(a.PropertyName).AppendLine(")!;");
                }
            }
            var argList = string.Join(", ", Enumerable.Range(0, m.ArgTypes.Length).Select(i => "arg" + i));
            if (m.ReturnType == null)
            {
                sb.Append("                ");
                if (m.IsAsync) sb.Append("await ");
                sb.Append(m.CSharpName).Append('(').Append(argList).AppendLine(");");
                sb.AppendLine("                return null;");
            }
            else if (m.ReturnType.IsPassthrough)
            {
                sb.Append("                var result = ");
                if (m.IsAsync) sb.Append("await ");
                sb.Append(m.CSharpName).Append('(').Append(argList).AppendLine(");");
                sb.AppendLine("                return result.GetRawText();");
            }
            else
            {
                sb.Append("                var result = ");
                if (m.IsAsync) sb.Append("await ");
                sb.Append(m.CSharpName).Append('(').Append(argList).AppendLine(");");
                sb.Append("                return JsonSerializer.Serialize(result, global::Pengu.PenguJsonContext.Default.")
                  .Append(m.ReturnType.PropertyName).AppendLine(");");
            }
            sb.AppendLine("            }");
        }

        sb.Append("            default: throw new InvalidOperationException(\"Unknown bridge method '\" + method + \"' on '")
          .Append(c.GlobalName).AppendLine("'\");");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return SourceText.From(sb.ToString(), Encoding.UTF8);
    }

    // -------- model --------

    private sealed class TypeInfo
    {
        public string FullName { get; }
        public string PropertyName { get; }
        public bool IsPassthrough { get; }
        public TypeInfo(string fullName, string propertyName, bool isPassthrough)
        { FullName = fullName; PropertyName = propertyName; IsPassthrough = isPassthrough; }
    }

    private sealed class MethodInfo
    {
        public string CSharpName { get; }
        public string JsName { get; }
        public ImmutableArray<TypeInfo> ArgTypes { get; }
        public TypeInfo ReturnType { get; }
        public bool IsAsync { get; }
        public MethodInfo(string csName, string jsName, ImmutableArray<TypeInfo> args, TypeInfo ret, bool isAsync)
        { CSharpName = csName; JsName = jsName; ArgTypes = args; ReturnType = ret; IsAsync = isAsync; }
    }

    private sealed class ClassInfo
    {
        public string Namespace { get; }
        public string ClassName { get; }
        public string Accessibility { get; }
        public string GlobalName { get; }
        public ImmutableArray<MethodInfo> Methods { get; }
        public ClassInfo(string ns, string name, string accessibility, string global, ImmutableArray<MethodInfo> methods)
        { Namespace = ns; ClassName = name; Accessibility = accessibility; GlobalName = global; Methods = methods; }
    }
}
