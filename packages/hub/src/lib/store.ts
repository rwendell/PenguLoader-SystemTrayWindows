import { pengu, type StorePlugin } from './pengu'

export type { StorePlugin }

/**
 * Plugin store registry browser. The host fetches and parses the YAML registry
 * at `https://raw.githack.com/PenguLoader/plugin-store/main/registry/plugins.yml`
 * and returns typed entries. The registry remains a placeholder pending
 * automation; this is browse-only.
 */
export const StoreManager = {
  fetchPlugins(): Promise<StorePlugin[]> {
    return pengu.plugins.fetchStoreRegistry()
  },
}
