namespace Pengu.Activation;

/// <summary>
/// Snapshot of an LCUX session announced by Riot Client Services on the
/// <c>OnJsonApiEvent_product-session_v1_sessions</c> WAMP topic. Carried
/// into <see cref="IActivationAction.OnSessionCreatedAsync"/> /
/// <see cref="IActivationAction.OnSessionDeletedAsync"/> so actions have
/// the context they need (e.g. the LoL install path on macOS, the LCUX
/// pid for diagnostics).
///
/// <para>Populated by <c>RcsDaemon</c> in C.3. Internal to the host —
/// doesn't cross the bridge boundary.</para>
/// </summary>
public sealed record LcuxSession(
    string ProductId,
    string? PatchlineId = null,
    string? InstallPath = null,
    int Pid = 0);
