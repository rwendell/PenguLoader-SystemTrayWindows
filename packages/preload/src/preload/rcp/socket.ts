import { rcp } from './hooks';

/**
 * LCDS event subscription.
 *
 * LCUX talks to its backend over a WAMP-over-WebSocket connection. Every
 * LCDS state change (summoner data, gameflow, party, ...) is published as
 * a WAMP type-8 EVENT frame on the broadcast endpoint `OnJsonApiEvent`.
 * The frame body has the shape `{data, uri, eventType}` where:
 *   - `uri` is the LCDS path (e.g. `/lol-summoner/v1/current-summoner`),
 *   - `eventType` is `Create | Update | Delete`,
 *   - `data` is the resource payload (or null for Delete).
 *
 * This module exposes `socket.observe(api, fn)` for plugins to subscribe
 * to those events, and routes incoming frames to matching listeners.
 *
 * # How we get the socket
 *
 * Riot's frontend already maintains the WAMP connection on
 * `provider.context.socket._websocket` for the `rcp-fe-common-libs` plugin.
 * We attach a `message` listener to that same WebSocket — no second
 * connection, no duplicate TLS handshake, no separate WAMP session.
 *
 * # Why we don't use Riot's `socket.subscribe`
 *
 * Riot's `socket` object exposes a `subscribe(uri, context, action)` method,
 * but in this CEF/CEF108 build the underlying `_socket` (the WAMP-aware
 * wrapper that would forward subscribes to the wire) is permanently
 * undefined. Calling `subscribe` only registers a local handler in the
 * dispatcher; no WAMP SUBSCRIBE goes on the wire. So in practice subscribing
 * via Riot's socket would only work for the broadcast endpoint, which Riot's
 * frontend already subscribed to. We therefore listen on the raw WebSocket
 * and parse frames ourselves — same delivery, simpler dispatch, full
 * `{data, uri, eventType}` envelope preserved (Riot's dispatcher would
 * have stripped `eventType` and `uri`, passing only `body.data`).
 *
 * # Server-side filtering: not used
 *
 * Riot only ever pushes events to `OnJsonApiEvent` (the broadcast). Pengu's
 * older `socket.observe('lol-summoner/v1/current-summoner', fn)` API
 * accepts URI-style names, but with this socket impl the server doesn't
 * filter by scope — every event flows through and we filter client-side
 * by matching `body.uri` to the listener's registered key. For typical
 * LCUX traffic (a few events per minute at idle) this is negligible.
 */

interface EventData {
  data: any;
  uri: string;
  eventType: 'Create' | 'Update' | 'Delete';
}

interface ApiListener {
  (message: EventData): void;
}

/**
 * Map from `buildApi(api)` key to its registered listener functions.
 * Two key shapes coexist:
 *   - `OnJsonApiEvent`                                  — broadcast subscribers
 *     (registered via observe('all', fn))
 *   - `OnJsonApiEvent_<flattened-uri>`                  — scoped subscribers
 *     (registered via observe('lol-x/v1/y', fn) or observe('/lol-x/v1/y', fn))
 *
 * `flattened-uri` replaces every `/` with `_`, so `/lol-x/v1/y` becomes
 * `_lol-x_v1_y`. This is the convention from the original Pengu socket; we
 * keep it for backwards compatibility with existing user plugins.
 */
const listenersMap = new Map<string, Array<ApiListener>>();

/**
 * Guards against re-registering our message handler if `rcp-fe-common-libs`
 * preInit ever fires twice (it shouldn't, but defensive). Set on first
 * successful attach; never reset.
 */
let attached = false;

// Capture the WAMP WebSocket on the first announce of rcp-fe-common-libs.
// This callback runs during the plugin's `before` phase, so the socket is
// already constructed but the plugin's registrar hasn't run yet — a fine
// time to add a passive message listener.
rcp.preInit('rcp-fe-common-libs', async function (provider) {
  if (attached) return;
  const ws: WebSocket | undefined = provider?.context?.socket?._websocket;
  if (!ws) return;
  attached = true;
  ws.addEventListener('message', handleMessage);
});

/**
 * Parse a WAMP frame and dispatch to matching listeners.
 *
 * WAMP frame format on this socket: `[type, endpoint, body]` where
 * type 8 = EVENT. Anything else (HELLO, RESULT, GOODBYE, heartbeat) is
 * ignored. The body for type-8 events is `{data, uri, eventType}`.
 *
 * Listeners are invoked via `setTimeout(..., 0)` rather than synchronously
 * so an exception in one listener can't disrupt other listeners or the
 * underlying WebSocket message loop. This also yields back to the event
 * loop between callbacks, keeping the UI responsive when many listeners
 * are registered.
 */
function handleMessage(ev: MessageEvent<string>) {
  let frame: any;
  try { frame = JSON.parse(ev.data); }
  catch { return; }
  if (!Array.isArray(frame) || frame[0] !== 8) return;
  const body = frame[2] as EventData | undefined;
  if (!body || typeof body !== 'object') return;

  // Broadcast: every observer registered with api='all' gets every event.
  const all = listenersMap.get('OnJsonApiEvent');
  if (all) for (const cb of all) setTimeout(() => cb(body), 0);

  // Scoped: route by the body's inner uri to listeners registered with a
  // matching path. Lowercased + slashes-to-underscores so plugin authors
  // can use either '/lol-x/v1/y' or 'lol-x/v1/y' without caring about case.
  if (typeof body.uri === 'string') {
    const key = 'OnJsonApiEvent' + body.uri.toLowerCase().replace(/\//g, '_');
    const scoped = listenersMap.get(key);
    if (scoped) for (const cb of scoped) setTimeout(() => cb(body), 0);
  }
}

/**
 * Build the listenersMap key from a user-supplied api string.
 *
 * - `api === 'all'` → `OnJsonApiEvent` (broadcast)
 * - anything else → `OnJsonApiEvent_<lowercased, slashes-to-underscores>`
 *
 * Leading and trailing slashes are stripped first, so the normalized form
 * matches keys produced from `body.uri` in `handleMessage`.
 */
function buildApi(api: string): string {
  if (api === 'all') return 'OnJsonApiEvent';
  api = api.toLowerCase().replace(/^\/+|\/+$/g, '');
  return 'OnJsonApiEvent_' + api.replace(/\//g, '_');
}

/**
 * Subscribe to LCDS events.
 *
 * Returns an object with a `disconnect()` method on success; returns
 * `false` if either argument is invalid (non-string api, empty api,
 * non-function listener).
 *
 * Multiple listeners can register for the same api; they're invoked in
 * registration order on each event. Subscribing the same listener twice
 * registers it twice (no dedupe).
 */
function observe(api: string, listener: ApiListener) {
  if (typeof api !== 'string' || api === ''
    || typeof listener !== 'function')
    return false;

  const endpoint = buildApi(api);
  const arr = listenersMap.get(endpoint);
  if (arr) arr.push(listener);
  else listenersMap.set(endpoint, [listener]);

  return {
    disconnect: () => disconnect(api, listener),
  };
}

/**
 * Remove a previously-registered listener. Returns `true` if the
 * listener was found and removed, `false` if no matching listener
 * was registered for this api (silent — not an error).
 *
 * Note: this only removes the specific listener function. Other
 * listeners on the same api stay registered.
 */
function disconnect(api: string, listener: ApiListener) {
  const endpoint = buildApi(api);
  const arr = listenersMap.get(endpoint);
  if (!arr) return false;
  const filtered = arr.filter(x => x !== listener);
  if (filtered.length === 0) listenersMap.delete(endpoint);
  else listenersMap.set(endpoint, filtered);
  return true;
}

export const socket = {
  observe,
  disconnect,
};
