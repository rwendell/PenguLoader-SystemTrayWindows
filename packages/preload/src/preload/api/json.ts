import { native } from './native';

/**
 * Wire up the writable-JSON module's `$write` back-end.
 *
 * The C++ scheme handler emits a SCRIPT_IMPORT_JSON shim (assets_shims.h)
 * that attaches `$write` to parsed JSON objects. That shim references
 * `window.__pwj` — captured from `window.__native.WriteJson` here and
 * exposed as a non-enumerable, non-configurable, non-writable global.
 *
 * Hiding from `Object.keys` / `for...in` / DevTools casual scan is defense
 * in depth — the actual sandbox is in C++ `v8_json_write.cc` which rejects
 * writes that escape the plugins directory. But matching the `__native`
 * pattern keeps the public surface clean.
 *
 * Must run before any plugin imports a `.json` file. The import chain in
 * `api/index.ts` places this side-effect first, before `loader.ts` starts
 * pulling plugins in.
 */
Object.defineProperty(window, '__pwj', {
  value: native.WriteJson,
  writable: false,
  configurable: false,
  enumerable: false,
});

export {};
