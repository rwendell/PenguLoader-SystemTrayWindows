import { createStore } from 'solid-js/store';
import { createSignal, type Accessor } from 'solid-js';
import type {
  Field, Schema, SettingsRegister, SettingsHandle, InferValues,
} from '@pengujs/types';

// =============================================================================
// Settings registry + reactive store.
//
// Persistence is the plugin author's choice — they pass a `state` object on
// register() (a writable-JSON import, a DataStore proxy, or anything else),
// and the drawer mutates it directly. `onChange` is the persistence hook.
//
// If no `state` is provided, an in-memory mirror is created from schema
// defaults — useful for transient/ephemeral toggles.
// =============================================================================

interface Entry {
  id: string;
  name: string;
  description?: string;
  icon?: string;
  schema: Schema;
  hotkey?: string;
  onChange?: (values: Record<string, unknown>) => void | Promise<void>;
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
// Schema helpers
// =============================================================================

function fieldHoldsValue(field: Field): boolean {
  return field.type !== 'action' && field.type !== 'note';
}

function defaultsFromSchema(schema: Schema): Record<string, unknown> {
  const out: Record<string, unknown> = {};
  for (const [key, field] of Object.entries(schema)) {
    if (fieldHoldsValue(field)) {
      out[key] = (field as Extract<Field, { default: unknown }>).default;
    }
  }
  return out;
}

/**
 * Fill missing keys in the supplied state with their schema defaults.
 * Mutates in place — the plugin's `state` reference is updated so a later
 * `state.$write()` captures the defaulted values.
 */
function fillDefaults(schema: Schema, state: Record<string, unknown>) {
  for (const [key, field] of Object.entries(schema)) {
    if (!fieldHoldsValue(field)) continue;
    if (!(key in state)) {
      state[key] = (field as Extract<Field, { default: unknown }>).default;
    }
  }
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

function clearHotkeysFor(forId: string) {
  for (const [k, v] of hotkeyMap) {
    if (v === forId) hotkeyMap.delete(k);
  }
}

export function register<S extends Schema>(opts: SettingsRegister<S>): SettingsHandle<InferValues<S>> {
  const { id, name, description, icon, schema, hotkey, onChange, state } = opts;

  // Replace existing entry (handles HMR / re-register).
  const existing = entries.get(id);
  if (existing) {
    console.warn(`[pengu] Settings.register("${id}") replaced an existing registration`);
    clearHotkeysFor(id);
    const t = debounceTimers.get(id);
    if (t !== undefined) { window.clearTimeout(t); debounceTimers.delete(id); }
  }

  // The backing state object. If the plugin supplied one, mutate it directly
  // so `state.$write()` (or any external persistence) sees current values.
  // Otherwise we own a fresh object from schema defaults (in-memory only).
  const backing: Record<string, unknown> =
    (state as Record<string, unknown> | undefined) ?? defaultsFromSchema(schema);
  fillDefaults(schema, backing);

  // Solid signal mirrors `backing` so the drawer rerenders on writes through
  // `setValues`. External mutations to `backing` (without going through
  // `setValues`) don't refresh the drawer — documented behavior; plugins
  // that need that contract should funnel through `set()`.
  const [values, setValuesSig] = createSignal<Record<string, unknown>>({ ...backing });

  const setValues = (patch: Record<string, unknown>) => {
    // Mutate the plugin-owned object in place AND update the signal copy.
    // Two writes are intentional: plugin reads `backing` (for $write), drawer
    // reads `values()` (for reactivity).
    Object.assign(backing, patch);
    setValuesSig({ ...backing });

    if (onChange) {
      const t = debounceTimers.get(id);
      if (t !== undefined) window.clearTimeout(t);
      debounceTimers.set(id, window.setTimeout(() => {
        debounceTimers.delete(id);
        try {
          const result = onChange(backing as InferValues<S>);
          // Swallow async rejections — plugins are responsible for their own
          // persistence errors; we don't want one bad save to crash the host.
          if (result && typeof (result as Promise<void>).catch === 'function') {
            (result as Promise<void>).catch(err =>
              console.error(`[pengu] onChange "${id}" rejected`, err));
          }
        } catch (err) {
          console.error(`[pengu] onChange "${id}" threw`, err);
        }
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
  clearHotkeysFor(id);
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
