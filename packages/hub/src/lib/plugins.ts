import { pengu, type PluginInfo } from './pengu'

export type { PluginInfo }

/**
 * Plugin discovery + state management. The host walks the plugins directory,
 * parses JSDoc tags from each entry file, computes the FNV-1a 32-bit path
 * hash, and intersects with the `disabled_plugins` config csv before
 * returning. The hub just consumes the typed result.
 */
export const PluginManager = new class {

  /** All discovered plugins, with disabled-state already computed by the host. */
  getPlugins(): Promise<PluginInfo[]> {
    return pengu.plugins.list()
  }

  /** Toggle a plugin's enabled state by path; returns the new state. */
  async toggleState(path: string): Promise<boolean> {
    return await pengu.plugins.toggleEnabled(path)
  }

  /** Open the configured plugins directory in Explorer / Finder. */
  openFolder(): Promise<void> {
    return pengu.plugins.openFolder()
  }

  /** Reveal a specific plugin entry in Explorer / Finder. */
  revealInFolder(path: string): Promise<void> {
    return pengu.plugins.revealInFolder(path)
  }
}
