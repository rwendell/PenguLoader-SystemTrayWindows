namespace Pengu.Activation;

/// <summary>
/// Registry of <see cref="IActivationAction"/> implementations keyed by
/// <see cref="ActivationMode"/>. Heads (Pengu.Windows / Pengu.MacOS)
/// register their platform-specific actions during startup;
/// <see cref="Pengu.Api.ActivationApi"/> resolves through the registry on
/// each bridge call.
///
/// <para>The registry also remembers which modes are "available" on the
/// current platform (a head can register an unavailable mode to surface
/// a clear "not supported on this OS" error rather than a silent fallback,
/// but for v1 we keep it simple — registered ⇔ available).</para>
/// </summary>
public sealed class ActivationActionRegistry
{
    private readonly Dictionary<ActivationMode, IActivationAction> _actions = new();

    /// <summary>Register an action for its declared mode. Replaces any
    /// previous registration for the same mode.</summary>
    public void Register(IActivationAction action)
    {
        _actions[action.Mode] = action;
    }

    /// <summary>Lookup the action for <paramref name="mode"/>, or null if
    /// no head registered one. Callers handle the null case by surfacing
    /// an <see cref="ActivationResult.Fail"/> with a "mode not available"
    /// message.</summary>
    public IActivationAction? Get(ActivationMode mode)
    {
        return _actions.TryGetValue(mode, out var a) ? a : null;
    }

    /// <summary>True if any action is registered for <paramref name="mode"/>.</summary>
    public bool IsAvailable(ActivationMode mode) => _actions.ContainsKey(mode);

    /// <summary>All modes that have a registered action. Order is registration order.</summary>
    public IEnumerable<ActivationMode> RegisteredModes => _actions.Keys;
}
