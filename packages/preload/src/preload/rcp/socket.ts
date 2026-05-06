import { rcp } from './hooks';

interface EventData {
  data: any;
  uri: string;
  eventType: 'Create' | 'Update' | 'Delete';
}

interface ApiListener {
  (message: EventData): void;
}

const listenersMap = new Map<string, Array<ApiListener>>();
let attached = false;

// Reuse Riot's existing WAMP WebSocket (provider.context.socket._websocket)
// instead of opening a duplicate connection. Riot's frontend subscribes to the
// broadcast endpoint OnJsonApiEvent at the wire level, so every LCDS event
// flows through that one socket; we filter client-side by inner body.uri.
rcp.preInit('rcp-fe-common-libs', async function (provider) {
  if (attached) return;
  const ws: WebSocket | undefined = provider?.context?.socket?._websocket;
  if (!ws) return;
  attached = true;
  ws.addEventListener('message', handleMessage);
});

function handleMessage(ev: MessageEvent<string>) {
  let frame: any;
  try { frame = JSON.parse(ev.data); }
  catch { return; }
  if (!Array.isArray(frame) || frame[0] !== 8) return;
  const body = frame[2] as EventData | undefined;
  if (!body || typeof body !== 'object') return;

  // 'all' subscribers receive every event
  const all = listenersMap.get('OnJsonApiEvent');
  if (all) for (const cb of all) setTimeout(() => cb(body), 0);

  // Scoped subscribers match by inner uri flattened to underscores
  if (typeof body.uri === 'string') {
    const key = 'OnJsonApiEvent' + body.uri.toLowerCase().replace(/\//g, '_');
    const scoped = listenersMap.get(key);
    if (scoped) for (const cb of scoped) setTimeout(() => cb(body), 0);
  }
}

function buildApi(api: string): string {
  if (api === 'all') return 'OnJsonApiEvent';
  api = api.toLowerCase().replace(/^\/+|\/+$/g, '');
  return 'OnJsonApiEvent_' + api.replace(/\//g, '_');
}

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
