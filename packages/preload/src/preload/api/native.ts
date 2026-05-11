// @ts-ignore
export const native: Native = window.__native;

// @ts-ignore
delete window.__native;

interface Native {
  OpenDevTools: () => void;
  OpenPluginsFolder: (path?: string) => boolean;
  ReloadClient: () => void;

  SetWindowTheme: (dark: boolean) => void;
  SetWindowVibrancy: (kind: number | null, state?: number) => void;

  // Async DataStore — see core/src/renderer/v8_datastore.cc and
  // packages/preload/src/preload/api/DataStore.ts.
  LoadDataStore:  () => Promise<string>;
  SaveDataStore:  (data: string) => void;          // fire-and-forget
  FlushDataStore: () => Promise<void>;

  // Writable-JSON $write back-end. Captured + rebound as `window.__pwj` by
  // api/json.ts so the SCRIPT_IMPORT_JSON shim can call into it.
  WriteJson: (url: string, content: string) => Promise<void>;
}
