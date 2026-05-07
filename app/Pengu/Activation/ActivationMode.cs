namespace Pengu.Activation;

/// <summary>
/// Loader activation mode. Persisted in the <c>[app]</c> section of the config
/// file as <c>activation_mode</c> (integer).
///
/// <para><see cref="Targeted"/> is reserved but unselectable: the symlink
/// activation path was dropped during the v1.2.0 redesign (see
/// <c>docs/app-hub.md</c> §9.1). The enum value remains so config files
/// written by old loaders that picked Targeted parse without exception;
/// callers that read it should fall back to <see cref="Universal"/>.</para>
/// </summary>
public enum ActivationMode
{
    Universal = 0,
    /// <summary>Reserved; symlink mode dropped in app/.</summary>
    Targeted = 1,
    OnDemand = 2,
}
