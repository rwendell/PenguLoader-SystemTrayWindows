import { For, Show, onMount } from 'solid-js';
import {
  addToast, removeToast, dismissAll, updateToast, setDefaultPosition,
  toastsState,
  type ToastEntry, type ToastOptions, type ToastPosition, type ToastType,
} from '../lib/toast-store';
import './Toaster.scss';

const POSITIONS: ToastPosition[] = [
  'top-left', 'top-center', 'top-right',
  'bottom-left', 'bottom-center', 'bottom-right',
];

const TYPE_ICONS: Record<ToastType, string> = {
  success: '✓',
  error:   '✕',
  info:    'i',
  warning: '!',
  loading: '⟳',
  custom:  '',
};

interface ToasterProps {
  /** Default position for toasts that don't specify one in their options. */
  position?: ToastPosition;
  /** Gap (px) between stacked toasts in the same corner. Default 8. */
  gutter?: number;
}

export function Toaster(props: ToasterProps) {
  // Set the default position once at mount. Components mount once for the
  // lifetime of the preload, so a one-shot is correct here.
  if (props.position) setDefaultPosition(props.position);
  const gutter = () => props.gutter ?? 8;

  return (
    <For each={POSITIONS}>
      {(pos) => {
        const items = () => toastsState.toasts.filter(t => t.position === pos);
        return (
          <Show when={items().length > 0}>
            <div
              class={`pengu-toaster pengu-toaster-${pos}`}
              style={{ '--pengu-toaster-gutter': `${gutter()}px` }}
            >
              <For each={items()}>
                {(t) => <ToastItem entry={t} />}
              </For>
            </div>
          </Show>
        );
      }}
    </For>
  );
}

function ToastItem(props: { entry: ToastEntry }) {
  let ref!: HTMLDivElement;

  onMount(() => {
    ref.animate(
      [
        { opacity: 0, transform: 'translateY(8px)' },
        { opacity: 1, transform: 'translateY(0)' },
      ],
      { duration: 180, easing: 'ease-out', fill: 'forwards' }
    );
  });

  const cls = () => {
    const base = `pengu-toast pengu-toast-${props.entry.type}`;
    return props.entry.className ? `${base} ${props.entry.className}` : base;
  };

  return (
    <div ref={ref} class={cls()}>
      <Show when={props.entry.type !== 'custom' || !props.entry.html}>
        <div class="pengu-toast-icon">
          {props.entry.icon ?? TYPE_ICONS[props.entry.type]}
        </div>
      </Show>
      <Show
        when={props.entry.type === 'custom' && props.entry.html}
        fallback={<div class="pengu-toast-message">{props.entry.message}</div>}
      >
        <div class="pengu-toast-message" innerHTML={props.entry.html} />
      </Show>
      <Show when={props.entry.dismissable}>
        <button
          type="button"
          class="pengu-toast-close"
          aria-label="Dismiss"
          onClick={() => removeToast(props.entry.id)}
        >
          <svg width="10" height="10" viewBox="0 0 10 10" fill="currentColor">
            <polygon points="10.2,0.7 9.5,0 5.1,4.4 0.7,0 0,0.7 4.4,5.1 0,9.5 0.7,10.2 5.1,5.8 9.5,10.2 10.2,9.5 5.8,5.1" />
          </svg>
        </button>
      </Show>
    </div>
  );
}

type PromiseMessages = {
  loading: string;
  success: string;
  error: string | ((err: unknown) => string);
};

/** Internal singleton — same shape as `window.Toast` but methods always return ids/promises. */
export const toast = {
  success: (m: string, o?: ToastOptions) => addToast('success', m, o),
  error:   (m: string, o?: ToastOptions) => addToast('error',   m, o),
  info:    (m: string, o?: ToastOptions) => addToast('info',    m, o),
  warning: (m: string, o?: ToastOptions) => addToast('warning', m, o),
  loading: (m: string, o?: ToastOptions) => addToast('loading', m, o),
  custom:  (html: string, o?: ToastOptions) => addToast('custom', '', o, html),

  dismiss: (id?: string) => (id ? removeToast(id) : dismissAll()),

  update: (id: string, patch: { message?: string; type?: ToastType; icon?: string }) =>
    updateToast(id, patch),

  promise<T>(p: Promise<T>, msg: PromiseMessages, opts?: ToastOptions): Promise<T> {
    const id = addToast('loading', msg.loading, opts);
    p.then(
      () => updateToast(id, { type: 'success', message: msg.success }),
      (err) => updateToast(id, {
        type: 'error',
        message: typeof msg.error === 'function' ? msg.error(err) : msg.error,
      }),
    );
    return p;
  },
};

// Public surface for plugin authors. Same methods, same return values — the
// type declared in `types.d.ts` mirrors the internal `toast` shape so plugins
// see the full API.
window.Toast = toast as Toast;
