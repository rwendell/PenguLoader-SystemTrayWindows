import { pengu, ActivationMode, type ActivationResult } from './pengu'

export { ActivationMode }

/**
 * Activation surface — installs / uninstalls the core module via the host's
 * mode-specific {@link IActivationAction}. The hub doesn't know whether the
 * action is IFEO, dwrite-copy, or insert_dylib; that's a config + platform
 * choice handled in C#.
 */
export const CoreModule = {
  /** True if `core.dll` / `core.dylib` is resolvable next to the host exe. */
  exists(): Promise<boolean> {
    return pengu.activation.coreExists()
  },

  /** Whether the current activation mode is engaged. */
  isActivated(): Promise<boolean> {
    return pengu.activation.isActive()
  },

  /**
   * Toggle activation. Returns a typed result with optional error / stage
   * strings — replaces the v1.1.6/Tauri "non-empty error string means failure"
   * convention. Caller pairs this with a follow-up `isActivated()` check if
   * the new state needs to be reflected in UI immediately.
   */
  async doActivate(active: boolean): Promise<{ activated: boolean; error: string }> {
    const result: ActivationResult = await pengu.activation.setActive(active)
    const activated = await pengu.activation.isActive()
    return {
      activated,
      error: result.ok ? '' : (result.error ?? 'Activation failed'),
    }
  },
}
