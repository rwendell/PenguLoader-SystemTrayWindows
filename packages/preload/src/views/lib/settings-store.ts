import { createStore } from 'solid-js/store';
import { createSignal, type Accessor } from 'solid-js';
import type {
  Field, Schema, SettingsRegister, SettingsHandle, InferValues,
} from '@pengujs/types';

// =============================================================================
// Persistence — DataStore-backed today, behind an interface so the future
// per-plugin async storage layer (design.md §9) is a one-file swap.
// =============================================================================

export interface SettingsPersist {
  load(id: string): Record<string, unknown> | undefined;
  save(id: string, values: Record<string, unknown>): void;
}

const KEY_PREFIX = 'pengu:settings:';

const dataStorePersist: SettingsPersist = {
  load(id) {
    return window.DataStore?.get<Record<string, unknown>>(KEY_PREFIX + id);
  },
  save(id, values) {
    window.DataStore?.set(KEY_PREFIX + id, values);
  },
};

let persist: SettingsPersist = dataStorePersist;

/** Override the persistence backend (e.g. when the per-plugin async store lands). */
export function setPersist(impl: SettingsPersist) {
  persist = impl;
}

// =============================================================================
// Registry + reactive store
// =============================================================================

interface Entry {
  id: string;
  name: string;
  description?: string;
  icon?: string;
  schema: Schema;
  hotkey?: string;
  onChange?: (values: Record<string, unknown>) => void;
  values: Accessor<Record<string, unknown>>;
  setValues: (patch: Record<string, unknown>) => void;
}

const entries = new Map<string, Entry>();
const hotkeyMap = new Map<string, string>(); // normalized hotkey → entry id
const debounceTimers = new Map<string, number>();

const [drawerState, setDrawerState] = createStore<{ open: boolean; activeId: string | null; tick: number }>({
  open: false,
  activeId: null,
  tick: 0, // bump to force <For> recompute when entries map mutates
});

function bumpTick() {
  setDrawerState('tick', t => t + 1);
}

// =============================================================================
// Schema helpers — flatten defaults, fill from persisted blob
// =============================================================================

function defaultsFromSchema(schema: Schema): Record<string, unknown> {
  const out: Record<string, unknown> = {};
  for (const [key, field] of Object.entries(schema)) {
    if (fieldHoldsValue(field)) {
      out[key] = (field as Extract<Field, { default: unknown }>).default;
    }
  }
  return out;
}

function fieldHoldsValue(field: Field): boolean {
  return field.type !== 'action' && field.type !== 'note';
}

function mergeWithDefaults(
  defaults: Record<string, unknown>,
  persisted: Record<string, unknown> | undefined,
): Record<string, unknown> {
  if (!persisted) return { ...defaults };
  const out: Record<string, unknown> = {};
  for (const k of Object.keys(defaults)) {
    out[k] = k in persisted ? persisted[k] : defaults[k];
  }
  return out;
}

// =============================================================================
// Hotkey parser — `Ctrl+Shift+S` → normalized `'ctrl+shift+s'`. Cmd ↔ Ctrl on Mac.
// =============================================================================

interface ParsedHotkey {
  ctrl: boolean;
  alt: boolean;
  shift: boolean;
  meta: boolean;
  key: string;
}

function parseHotkey(s: string): ParsedHotkey | null {
  const parts = s.split('+').map(p => p.trim()).filter(Boolean);
  if (parts.length < 2) return null;

  let ctrl = false, alt = false, shift = false, meta = false;
  let key = '';

  for (const p of parts) {
    const lc = p.toLowerCase();
    if (lc === 'ctrl' || lc === 'control') ctrl = true;
    else if (lc === 'shift') shift = true;
    else if (lc === 'alt' || lc === 'option') alt = true;
    else if (lc === 'meta' || lc === 'cmd' || lc === 'command') meta = true;
    else key = lc;
  }

  if (!key) return null;
  if (!(ctrl || alt || shift || meta)) return null; // modifier required
  return { ctrl, alt, shift, meta, key };
}

function normalizeHotkey(s: string): string | null {
  const p = parseHotkey(s);
  if (!p) return null;
  const mods: string[] = [];
  if (p.ctrl)  mods.push('ctrl');
  if (p.alt)   mods.push('alt');
  if (p.shift) mods.push('shift');
  if (p.meta)  mods.push('meta');
  return [...mods, p.key].join('+');
}

function isEditable(el: Element | null): boolean {
  if (!el) return false;
  const tag = el.tagName;
  if (tag === 'INPUT' || tag === 'TEXTAREA') return true;
  return (el as HTMLElement).isContentEditable === true;
}

let hotkeyAttached = false;

function onKeyDown(e: KeyboardEvent) {
  if (isEditable(document.activeElement)) return;

  // Cross-platform: on Mac, `Ctrl+X` registrations should match Cmd+X.
  // Plain Ctrl on Mac is rare (used for menu navigation), so collapsing
  // ctrl ↔ meta on Mac is the right call for hotkeys plugin authors write.
  const isMac = window.Pengu?.isMac;
  const ctrl = isMac ? (e.ctrlKey || e.metaKey) : e.ctrlKey;
  const meta = isMac ? false : e.metaKey;

  const norm = [
    ctrl       ? 'ctrl'  : '',
    e.altKey   ? 'alt'   : '',
    e.shiftKey ? 'shift' : '',
    meta       ? 'meta'  : '',
    e.key.toLowerCase(),
  ].filter(Boolean).join('+');

  const id = hotkeyMap.get(norm);
  if (id !== undefined) {
    e.preventDefault();
    open(id);
  }
}

function ensureHotkeyListener() {
  if (hotkeyAttached) return;
  window.addEventListener('keydown', onKeyDown);
  hotkeyAttached = true;
}

// =============================================================================
// Public API (called from window.Settings)
// =============================================================================

function nextHotkeyClear(forId: string) {
  for (const [k, v] of hotkeyMap) {
    if (v === forId) hotkeyMap.delete(k);
  }
}

export function register<S extends Schema>(opts: SettingsRegister<S>): SettingsHandle<InferValues<S>> {
  const { id, name, description, icon, schema, hotkey, onChange } = opts;

  // Replace existing entry (handles HMR / re-register).
  const existing = entries.get(id);
  if (existing) {
    console.warn(`[pengu] Settings.register("${id}") replaced an existing registration`);
    nextHotkeyClear(id);
    const t = debounceTimers.get(id);
    if (t !== undefined) { window.clearTimeout(t); debounceTimers.delete(id); }
  }

  const defaults = defaultsFromSchema(schema);
  const initial = mergeWithDefaults(defaults, persist.load(id));

  const [values, setValuesSig] = createSignal<Record<string, unknown>>(initial);

  const setValues = (patch: Record<string, unknown>) => {
    const merged = { ...values(), ...patch };
    setValuesSig(merged);
    persist.save(id, merged);
    if (onChange) {
      const t = debounceTimers.get(id);
      if (t !== undefined) window.clearTimeout(t);
      debounceTimers.set(id, window.setTimeout(() => {
        debounceTimers.delete(id);
        try { onChange(merged as InferValues<S>); }
        catch (err) { console.error(`[pengu] onChange "${id}" threw`, err); }
      }, 80));
    }
  };

  entries.set(id, {
    id, name, description, icon, schema, hotkey,
    onChange: onChange as Entry['onChange'],
    values, setValues,
  });
  bumpTick();

  if (hotkey) {
    const norm = normalizeHotkey(hotkey);
    if (norm) {
      const prevOwner = hotkeyMap.get(norm);
      if (prevOwner !== undefined && prevOwner !== id) {
        console.warn(`[pengu] hotkey "${hotkey}" replaced by plugin "${id}" (was "${prevOwner}")`);
      }
      hotkeyMap.set(norm, id);
      ensureHotkeyListener();
    } else {
      console.warn(`[pengu] invalid hotkey "${hotkey}" for "${id}" — must include at least one modifier`);
    }
  }

  return {
    values: values as Accessor<InferValues<S>>,
    set: setValues as (patch: Partial<InferValues<S>>) => void,
    unregister: () => unregister(id),
  };
}

export function unregister(id: string) {
  if (!entries.has(id)) return;
  entries.delete(id);
  nextHotkeyClear(id);
  const t = debounceTimers.get(id);
  if (t !== undefined) { window.clearTimeout(t); debounceTimers.delete(id); }

  if (drawerState.activeId === id) {
    const first = entries.size > 0 ? entries.keys().next().value ?? null : null;
    setDrawerState({ activeId: first ?? null, open: first !== null && drawerState.open });
  }
  bumpTick();
}

export function open(id?: string) {
  if (id !== undefined && entries.has(id)) {
    setDrawerState({ open: true, activeId: id });
  } else if (drawerState.activeId === null && entries.size > 0) {
    setDrawerState({ open: true, activeId: entries.keys().next().value ?? null });
  } else {
    setDrawerState('open', true);
  }
}

export function close() {
  setDrawerState('open', false);
}

export function list(): Array<{ id: string; name: string }> {
  return Array.from(entries.values()).map(e => ({ id: e.id, name: e.name }));
}

// =============================================================================
// Internals exposed to the Drawer component
// =============================================================================

export function getEntry(id: string): Entry | undefined {
  return entries.get(id);
}

export function listEntries(): Entry[] {
  return Array.from(entries.values());
}

export { drawerState };
