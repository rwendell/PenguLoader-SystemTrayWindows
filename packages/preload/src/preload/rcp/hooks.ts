import { graph, dependencies, dependents, names } from './graph';

/**
 * Riot Client Plugin (RCP) hooks.
 *
 * Riot's frontend (LCUX) is composed of independent plugins named `rcp-fe-*`.
 * Each one announces itself by dispatching a CustomEvent of type
 * `riotPlugin.announce:<name>` on `document`. The event carries a
 * `registrationHandler` that, when called by the announcing plugin's loader,
 * runs the plugin's registrar (which builds and returns its public API).
 *
 * Pengu intercepts that announce → registrationHandler chain so user plugins
 * can:
 *   - run code BEFORE a plugin initializes (`rcp.preInit`),
 *   - run code AFTER a plugin's API is ready (`rcp.postInit`),
 *   - await a plugin's API as a Promise (`rcp.whenReady`),
 *   - synchronously look up an already-fulfilled plugin's API (`rcp.get`).
 *
 * Plugin lifecycle, with Pengu's wrap in place:
 *
 *   announce              registrar runs       all postInit awaited
 *      │                       │                       │
 *      ▼                       ▼                       ▼
 *   ┌────────┐ before ┌──────┐ ──────────► ┌─────────┐ after  ┌──────────┐
 *   │preInit │ awaited│ init │   registrar │ postInit│ awaited│fulfilled │
 *   └────────┘        └──────┘             └─────────┘        └──────────┘
 *
 * - State `preInit`: announce has fired, before-callbacks are still running.
 * - State `init`: before-callbacks done, registrar is running.
 * - State `postInit`: registrar returned an api, after-callbacks are running.
 * - State `fulfilled`: everything done; api is final and immutable from our side.
 */

type RcpAnnouceEvent = CustomEvent & {
  errorHandler: () => any;
  registrationHandler: (
    registrar: (provider: any) => Promise<any>
  ) => Promise<any> | void;
};

type CallbackType = 'before' | 'after';

interface Callback {
  (...args: any): void | Promise<void>;
}

interface PluginContainer {
  impl: null | object;
  state: 'preInit' | 'init' | 'postInit' | 'fulfilled';
}

/**
 * Per-plugin queue of pending before/after callbacks plus a total count used
 * to garbage-collect the entry once both queues drain. `_count` tracks the
 * total number of registered callbacks across both `before` and `after`
 * arrays so we can delete the container in O(1) once it hits zero.
 */
type CallbackContainer = {
  _count: number;
} & {
  [k in CallbackType]?: Callback[];
};

const RCPE_PREF = 'riotPlugin.announce:';
const RCPE_PREF_LEN = RCPE_PREF.length;

/** Plugins that have begun their lifecycle (any state). */
const _plugins = new Map<string, PluginContainer>();

/**
 * Pending callbacks per plugin name. Entries exist only between
 * `addCallback` and `invokeCallbacks` draining them; once empty, removed.
 */
const _callbacks = new Map<string, CallbackContainer>();

/**
 * Push-style subscription: register a one-shot listener for this specific
 * plugin's announce event. Only fires for the first announcement; if the
 * plugin re-announces (it shouldn't), our wrap won't run twice. This is
 * called lazily — only when the first callback for `name` is registered —
 * so we don't intercept announces for plugins nobody is listening to.
 *
 * Note: this replaces an older "pull" approach that wrapped
 * `document.dispatchEvent` to intercept every event. The push approach
 * avoids mutating `document.dispatchEvent` (which other libraries may
 * also want to wrap) and side-steps the resulting non-configurable
 * descriptor.
 */
function subscribePlugin(name: string) {
  const type = `${RCPE_PREF}${name}`;
  document.addEventListener(type, <any>onPluginAnnounce, {
    once: true,
    capture: false,
  });
}

/**
 * Fired once when an `riotPlugin.announce:<name>` event is dispatched.
 * Replaces the event's `registrationHandler` with our wrapper so that when
 * Riot's plugin loader calls it, our before/after lifecycle runs around
 * the real registrar.
 */
function onPluginAnnounce(event: RcpAnnouceEvent) {
  const name = event.type.substring(RCPE_PREF_LEN);
  const handler = event.registrationHandler;

  function handlerWrap(this: any, registrar: Parameters<typeof handler>[0]): ReturnType<typeof handler> {
    // Forward to the original handler with a wrapped registrar that runs
    // our lifecycle around the real one. The outer `handler.call(this, ...)`
    // preserves whatever `this` Riot intends for the handler.
    return handler.call(this, async (provider: any) => {
      const container: PluginContainer = { impl: null, state: 'preInit' };
      _plugins.set(name, container);

      // Run all `before` callbacks (registered via rcp.preInit).
      // Awaited so the registrar doesn't run until preInit work completes.
      await invokeCallbacks(name, 'before', provider);
      container.state = 'init';

      // Run Riot's actual registrar; the returned value is the plugin api.
      const api = await registrar(provider);
      container.impl = api;
      container.state = 'postInit';

      // Run all `after` callbacks (registered via rcp.postInit / whenReady).
      // Non-blocking callbacks were wrapped to fire-and-forget at registration
      // time, so awaiting Promise.allSettled here only delays for blocking ones.
      await invokeCallbacks(name, 'after', api);
      container.state = 'fulfilled';

      return api;
    });
  }

  // Replace the announce event's registrationHandler with our wrapper.
  // The announce event hasn't been fully dispatched yet — listeners run
  // before dispatchEvent returns — so subsequent listeners (Riot's plugin
  // loader) will see the wrapped value.
  Object.defineProperty(event, 'registrationHandler', {
    value: handlerWrap,
  });
}

/**
 * Drain a queue of callbacks for a plugin. New callbacks added during an
 * await are picked up by the next loop iteration — important because a
 * preInit/postInit callback may itself register additional callbacks for
 * the same plugin (rare, but legal).
 */
async function invokeCallbacks(name: string, type: CallbackType, ...args: any[]) {
  const container = _callbacks.get(name);
  if (container === undefined) return;

  const callbacks = container[type];
  if (callbacks === undefined) return;

  while (callbacks.length > 0) {
    // Decrement total count by the size of THIS batch before draining,
    // so concurrent registrations (which increment _count) don't desync.
    container._count -= callbacks.length;
    // Promise.allSettled — one bad callback shouldn't stop the others;
    // their exceptions are swallowed (they should log themselves if needed).
    await Promise.allSettled(callbacks.splice(0).map(cb => cb(...args)));
  }

  // Free the container once both queues are fully consumed.
  if (container._count === 0) {
    _callbacks.delete(name);
  }
}

/**
 * Register a callback in the pending queue. Lazily creates the container
 * and the document-level announce subscription on first use per plugin.
 */
function addCallback(name: string, callback: Callback, type: CallbackType) {
  let container = _callbacks.get(name);
  if (container === undefined) {
    container = { _count: 0, [type]: [] };
    _callbacks.set(name, container);
    // First time we care about this plugin — start listening for its announce.
    subscribePlugin(name);
  }

  let callbacks = container[type];
  if (callbacks === undefined) {
    callbacks = [];
    container[type] = callbacks;
  }

  container._count++;
  callbacks.push(callback);
}

/**
 * Normalise a user-provided plugin name. Riot's plugins are all prefixed
 * with `rcp-` and are case-insensitive in the announce protocol, but our
 * Map lookups are case-sensitive — so we lowercase and ensure the prefix
 * before any registry access. Applied at every public entrypoint so
 * `rcp.preInit('FE-Common-Libs', fn)` and `rcp.preInit('rcp-fe-common-libs', fn)`
 * resolve identically.
 */
function ensureName(name: string) {
  name = String(name).toLowerCase();
  if (!name.startsWith('rcp-')) {
    return 'rcp-' + name;
  }
  return name;
}

/**
 * Register a callback to run BEFORE the named plugin's registrar.
 * Returns true if accepted (plugin is unknown or still in `preInit` state),
 * false if the plugin has already moved past `preInit` (in which case the
 * callback is ignored — register earlier, or use `postInit` / `whenReady`).
 */
function preInit(name: string, callback: (provider: any) => any): boolean {
  if (typeof callback !== 'function')
    throw new TypeError(`${callback} is not a function`);

  name = ensureName(name);
  const plugin = _plugins.get(name);

  if (plugin === undefined || plugin.state === 'preInit') {
    addCallback(name, callback, 'before');
    return true;
  }

  return false;
}

/**
 * Register a callback to run AFTER the named plugin's registrar returns its
 * api. Returns true if accepted (plugin is unknown or not yet `fulfilled`),
 * false if already fulfilled.
 *
 * `blocking = false` (the default) wraps the callback so it runs as a
 * fire-and-forget side-effect — its return value/promise is discarded so
 * a slow callback doesn't delay the plugin's transition to `fulfilled`.
 * Pass `blocking = true` only when you genuinely need to delay the plugin
 * lifecycle (e.g. installing required mocks before the plugin is observed
 * as fulfilled by other consumers).
 */
function postInit(name: string, callback: (api: any) => any, blocking: boolean = false) {
  if (typeof callback !== 'function')
    throw new TypeError(`${callback} is not a function`);

  name = ensureName(name);
  const plugin = _plugins.get(name);

  if (plugin !== undefined && plugin.state === 'fulfilled')
    return false;

  addCallback(name, blocking ? callback : (api: any) => void callback(api), 'after');
  return true;
}

/**
 * Promise-based equivalent of postInit. Resolves with the plugin's api when
 * it reaches `fulfilled`. If the plugin is already fulfilled, resolves
 * synchronously on the next microtask with the cached impl.
 */
function whenReadyOne(name: string) {
  return new Promise<any>(resolve => {
    // postInit returns false only when the plugin is already fulfilled,
    // in which case we read the cached impl directly instead of waiting.
    if (!postInit(name, resolve)) {
      const plugin = _plugins.get(name)!;
      resolve(plugin.impl);
    }
  });
}

/** Resolves with an array of impls, in the same order as `names`. */
function whenReadyAll(names: string[]) {
  return Promise.all(names.map(name => whenReadyOne(name)));
}

function whenReady(name: string): Promise<any>;
function whenReady(names: string[]): Promise<any[]>;
function whenReady(param: any) {
  if (typeof param === 'string') {
    return whenReadyOne(ensureName(param));
  }
  if (Array.isArray(param)) {
    return whenReadyAll(param.map(ensureName));
  }
  throw new TypeError(`unexpected argument ${param}`);
}

/**
 * Synchronous lookup. Returns the api if the plugin is fulfilled (or beyond
 * postInit), undefined otherwise. Does NOT wait — use `whenReady` if you
 * need to synchronise with a plugin that may not have announced yet.
 */
function get(name: string) {
  name = ensureName(name);
  return _plugins.get(name)?.impl;
}

export const rcp = {
  preInit,
  postInit,
  whenReady,
  get,
  // Plugin dependency graph access (see ./graph.ts).
  graph,
  dependencies,
  dependents,
  names,
};

// Expose on `window` for advanced plugin authors who don't have access to
// the init context. Frozen properties (non-configurable, non-writable, not
// enumerable) so plugins can't accidentally shadow or replace it.
Object.defineProperty(window, 'rcp', {
  value: rcp,
  enumerable: false,
  configurable: false,
  writable: false,
});
