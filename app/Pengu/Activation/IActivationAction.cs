namespace Pengu.Activation;

/// <summary>
/// Per-mode activation behaviour. Each <see cref="ActivationMode"/> has at
/// most one implementation registered with the
/// <see cref="ActivationActionRegistry"/>; the bridge surface
/// (<c>pengu.activation.*</c>) and the RCS daemon both dispatch through it
/// to cover platform differences without leaking platform code into core.
///
/// <list type="bullet">
///   <item><description><c>Universal</c> on Windows -> <c>IfeoAction</c>:
///     IFEO registry write, no daemon involvement; <see cref="SetActiveAsync"/>
///     does the work.</description></item>
///   <item><description><c>OnDemand</c> on Windows -> <c>CopyDllAction</c>:
///     <see cref="OnSessionCreatedAsync"/> copies <c>core.dll</c> to
///     <c>&lt;LoL&gt;\dwrite.dll</c> when RCS announces a session.</description></item>
///   <item><description><c>OnDemand</c> on macOS -> <c>InsertDylibAction</c>:
///     patches <c>libEGL.dylib</c> with a <c>LC_LOAD_DYLIB</c> command on
///     session create; restores the backup on delete.</description></item>
/// </list>
///
/// <para>All methods take a <see cref="CancellationToken"/> so the host can
/// abort pending work on shutdown. Implementations should respect it but
/// keep operations short and atomic — these run on the dispatcher thread
/// and shouldn't park the UI for more than a few hundred ms.</para>
/// </summary>
public interface IActivationAction
{
    /// <summary>The mode this action implements.</summary>
    ActivationMode Mode { get; }

    /// <summary>
    /// True if the activation is currently engaged on this machine. For
    /// IFEO that's "the registry value points at our core.dll"; for
    /// OnDemand that's "we have an active session and the dylib/dll is in
    /// place".
    /// </summary>
    Task<bool> IsActiveAsync(CancellationToken ct);

    /// <summary>
    /// Toggle activation. For non-daemon modes (IFEO) this performs the
    /// install/uninstall directly. For daemon modes (OnDemand) this
    /// flips an "armed" flag — the actual file operations happen in
    /// <see cref="OnSessionCreatedAsync"/> when LCUX next launches.
    /// </summary>
    Task<ActivationResult> SetActiveAsync(bool active, CancellationToken ct);

    /// <summary>Daemon callback when RCS announces a new LCUX session.
    /// No-op for non-daemon modes.</summary>
    Task OnSessionCreatedAsync(LcuxSession session, CancellationToken ct);

    /// <summary>Daemon callback when RCS announces an LCUX session ended.
    /// No-op for non-daemon modes.</summary>
    Task OnSessionDeletedAsync(LcuxSession session, CancellationToken ct);
}
