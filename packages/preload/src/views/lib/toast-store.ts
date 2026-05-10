import { createStore } from 'solid-js/store';

export type ToastType =
  | 'success'
  | 'error'
  | 'info'
  | 'warning'
  | 'loading'
  | 'custom';

export type ToastPosition =
  | 'top-left' | 'top-center' | 'top-right'
  | 'bottom-left' | 'bottom-center' | 'bottom-right';

export interface ToastOptions {
  /** ms; 0, negative, or Infinity = sticky. Default 5000. `loading` defaults to sticky. */
  duration?: number;
  position?: ToastPosition;
  /** Override the type's default glyph. Plain string (emoji / single char). */
  icon?: string;
  className?: string;
  /** Reusing an id replaces the existing entry — useful for de-duping. */
  id?: string;
  /** Show the × button. Default true. */
  dismissable?: boolean;
}

export interface ToastEntry {
  id: string;
  type: ToastType;
  message: string;
  /** Set only when `type === 'custom'`; rendered via innerHTML. */
  html?: string;
  icon?: string;
  position: ToastPosition;
  duration: number;
  dismissable: boolean;
  className?: string;
  createdAt: number;
}

const DEFAULT_DURATION = 5000;
const DEFAULT_POSITION: ToastPosition = 'bottom-right';

const [state, setState] = createStore<{ toasts: ToastEntry[] }>({ toasts: [] });

// One auto-dismiss timer per toast id. Cleared on remove / update / replace.
const timers = new Map<string, number>();

let counter = 0;
const nextId = () => `t_${++counter}`;

let defaultPosition: ToastPosition = DEFAULT_POSITION;
export function setDefaultPosition(p: ToastPosition) {
  defaultPosition = p;
}

function isSticky(duration: number) {
  return !isFinite(duration) || duration <= 0;
}

function scheduleDismiss(id: string, duration: number) {
  clearDismiss(id);
  if (isSticky(duration)) return;
  const handle = window.setTimeout(() => removeToast(id), duration);
  timers.set(id, handle);
}

function clearDismiss(id: string) {
  const h = timers.get(id);
  if (h !== undefined) {
    window.clearTimeout(h);
    timers.delete(id);
  }
}

function resolveDuration(type: ToastType, opts?: ToastOptions): number {
  if (opts?.duration !== undefined) return opts.duration;
  if (type === 'loading') return Infinity;
  return DEFAULT_DURATION;
}

export function addToast(
  type: ToastType,
  message: string,
  opts?: ToastOptions,
  html?: string,
): string {
  const id = opts?.id ?? nextId();
  const duration = resolveDuration(type, opts);
  const entry: ToastEntry = {
    id,
    type,
    message,
    html,
    icon: opts?.icon,
    position: opts?.position ?? defaultPosition,
    duration,
    dismissable: opts?.dismissable ?? true,
    className: opts?.className,
    createdAt: Date.now(),
  };

  const existing = state.toasts.findIndex(t => t.id === id);
  if (existing >= 0) {
    setState('toasts', existing, entry);
  } else {
    setState('toasts', toasts => [...toasts, entry]);
  }
  scheduleDismiss(id, duration);
  return id;
}

export function removeToast(id: string) {
  clearDismiss(id);
  setState('toasts', toasts => toasts.filter(t => t.id !== id));
}

export function dismissAll() {
  for (const t of state.toasts) clearDismiss(t.id);
  setState('toasts', []);
}

export function updateToast(
  id: string,
  patch: { message?: string; type?: ToastType; icon?: string },
) {
  const idx = state.toasts.findIndex(t => t.id === id);
  if (idx < 0) return;
  const prev = state.toasts[idx];

  setState('toasts', idx, t => ({
    ...t,
    ...(patch.message !== undefined && { message: patch.message }),
    ...(patch.type !== undefined && { type: patch.type }),
    ...(patch.icon !== undefined && { icon: patch.icon }),
  }));

  // Re-arm the dismiss timer when transitioning out of `loading` (or any
  // sticky-by-default state) into a terminal type — `promise` relies on this
  // so the success/error toast auto-dismisses without callers tracking it.
  if (
    patch.type !== undefined &&
    patch.type !== prev.type &&
    patch.type !== 'loading'
  ) {
    const newDuration = resolveDuration(patch.type);
    setState('toasts', idx, 'duration', newDuration);
    scheduleDismiss(id, newDuration);
  }
}

export const toastsState = state;
