namespace Pengu.Activation;

/// <summary>
/// Outcome of an activation toggle. Replaces v1.1.6 / Tauri's
/// "non-empty error string means failure" convention with a typed result
/// that carries an optional human-readable error and a stage tag.
///
/// <para><see cref="Stage"/> identifies which step of the activation flow
/// produced the failure (e.g. <c>"SetDebugger"</c>, <c>"RunElevated"</c>,
/// <c>"CopyDll"</c>, <c>"ResolveAction"</c>). Useful in logs and for
/// hub-side messages without needing a separate error code.</para>
/// </summary>
public sealed record ActivationResult(bool Ok, string? Error = null, string? Stage = null)
{
    public static ActivationResult Success { get; } = new(true);

    public static ActivationResult Fail(string error, string? stage = null) =>
        new(false, error, stage);
}

/// <summary>
/// Per-mode availability metadata returned by
/// <c>pengu.activation.listModes()</c>. The hub uses it to grey out
/// unavailable radio options (Universal on macOS, Targeted always since
/// it was dropped) and to show a UAC indicator on modes that need
/// elevation.
/// </summary>
public sealed record ActivationModeInfo(
    ActivationMode Mode,
    bool Available,
    bool RequiresAdmin);
