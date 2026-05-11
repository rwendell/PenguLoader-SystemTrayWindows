import { native } from './native';

/**
 * Persistent key/value store, backed by an XOR'd JSON file on disk.
 *
 * Reads are sync against an in-memory Map populated at init. Writes mutate
 * the Map immediately and schedule a debounced async commit — rapid `set`
 * calls (e.g. a settings panel slider) collapse into one disk write, and the
 * renderer thread never blocks on file I/O.
 *
 * Public API stays sync-returning for ergonomics:
 *   set / remove return `boolean` synchronously (arg validation + accepted/not).
 *   flush returns `Promise<void>` for the rare power-user case that needs
 *   to know the pending write is durable.
 *
 * See `core/src/renderer/v8_datastore.cc` for the C++ side — single-slot
 * pending buffer + persistent worker thread, latest-wins coalescing.
 */

const COMMIT_DEBOUNCE_MS = 100;

let data_ = new Map<string, unknown>();
let pendingTimer: number | undefined;

/**
 * Read the on-disk store. Called from the loader bootstrap before plugins —
 * plugin `init` can therefore use sync `get` / `has` and see correct values.
 */
export async function initDataStore() {
  try {
    const json = await native.LoadDataStore();
    const object = JSON.parse(json);
    data_ = new Map(Object.entries(object));
  } catch (err) {
    console.warn('Pengu failed to load DataStore, starting empty.', err);
  }
}

function commitNow() {
  if (pendingTimer !== undefined) {
    window.clearTimeout(pendingTimer);
    pendingTimer = undefined;
  }
  const object = Object.fromEntries(data_);
  native.SaveDataStore(JSON.stringify(object));
}

function scheduleCommit() {
  if (pendingTimer !== undefined) {
    window.clearTimeout(pendingTimer);
  }
  pendingTimer = window.setTimeout(() => {
    pendingTimer = undefined;
    commitNow();
  }, COMMIT_DEBOUNCE_MS);
}

window.DataStore = {

  has(key) {
    return data_.has(String(key));
  },

  get(key, fallback) {
    if (typeof key !== 'string') return undefined;
    if (data_.has(key)) return data_.get(key) as any;
    return fallback;
  },

  set(key, value) {
    if (typeof key !== 'string') return false;
    data_.set(String(key), value);
    scheduleCommit();
    return true;
  },

  remove(key) {
    const result = data_.delete(String(key));
    if (result) scheduleCommit();
    return result;
  },

  async flush() {
    // Force any pending debounced commit immediately, then wait for the
    // native writer to drain (no pending blob + no in-flight write).
    commitNow();
    await native.FlushDataStore();
  },
};
