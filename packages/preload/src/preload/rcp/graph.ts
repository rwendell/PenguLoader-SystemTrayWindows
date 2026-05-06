/**
 * RCP plugin dependency graph.
 *
 * LCUX serves `/graph.json` describing every RCP plugin in the build along
 * with its dependencies, load sequence, virtual-impl mappings, and
 * lazy-loading hints. The graph is static for a given client build, so we
 * fetch it once and cache the Promise.
 *
 * Use cases for plugin authors:
 *  - Discover all RCP plugin names without waiting for announces.
 *  - Subscribe (preInit/postInit/whenReady) to every plugin we care about
 *    BEFORE its announce fires — required by the push-style RCP contract.
 *  - Reason about plugin dependencies (e.g. "wait for X and all its deps").
 */

export interface PluginGraph {
  /** Plugin "affinities" — niche, usually empty. */
  affinities: Record<string, any>;
  /** Map of plugin name → list of its direct dependencies. */
  dependencies: Record<string, string[]>;
  /** Virtual-package resolution: e.g. `rcp-be-lol-rso-auth → rcp-be-rga-rso-auth`. */
  implementations: Record<string, string>;
  /** Plugins loaded lazily / on demand. */
  lazy: Record<string, any>;
  /** Total ordered load sequence chosen by the runtime. */
  sequence: string[];
  /** Backward-compat shims. */
  shims: Record<string, any>;
}

let _graph: Promise<PluginGraph> | null = null;

/**
 * Fetch and cache the plugin graph. Subsequent calls return the same Promise.
 * Reload the page to refresh after a Riot client update.
 */
export function graph(): Promise<PluginGraph> {
  if (!_graph) {
    _graph = fetch('/graph.json').then(r => {
      if (!r.ok) throw new Error(`graph.json ${r.status}`);
      return r.json();
    });
  }
  return _graph;
}

/**
 * Direct dependencies of `name`. Returns `[]` if the plugin is unknown or has no deps.
 * Names are returned exactly as listed in the graph (lowercase, `rcp-` prefixed).
 */
export async function dependencies(name: string): Promise<string[]> {
  const g = await graph();
  return g.dependencies[name] ?? [];
}

/**
 * Direct dependents — plugins that depend on `name`. O(N) scan over the
 * dependencies map; cache the result yourself if calling frequently.
 */
export async function dependents(name: string): Promise<string[]> {
  const g = await graph();
  const out: string[] = [];
  for (const [k, deps] of Object.entries(g.dependencies)) {
    if (deps.includes(name)) out.push(k);
  }
  return out;
}

/**
 * All RCP plugin names known to the runtime, in load order.
 * Matches the keys present in announces, so it can be used to pre-subscribe
 * (e.g. via `rcp.whenReady`) before any plugin announces.
 */
export async function names(): Promise<string[]> {
  const g = await graph();
  return g.sequence.slice();
}
