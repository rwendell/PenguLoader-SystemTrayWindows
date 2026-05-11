import { Show, For, createMemo, onMount } from 'solid-js';
import {
  drawerState, listEntries, getEntry,
  register, open, close, list,
} from '../../lib/settings-store';
import { Form } from './Form';
import './Settings.scss';

function isEditable(el: Element | null): boolean {
  if (!el) return false;
  const tag = el.tagName;
  if (tag === 'INPUT' || tag === 'TEXTAREA') return true;
  return (el as HTMLElement).isContentEditable === true;
}

export function SettingsDrawer() {
  let panelRef!: HTMLDivElement;

  // Read `tick` so `<For>` re-runs `listEntries()` whenever the registry mutates.
  // The Map isn't reactive on its own; the tick is bumped from register/unregister.
  const entries = createMemo(() => (drawerState.tick, listEntries()));
  const active = createMemo(() =>
    drawerState.activeId !== null ? getEntry(drawerState.activeId) : undefined,
  );

  onMount(() => {
    window.addEventListener('keydown', (e) => {
      if (drawerState.open && e.key === 'Escape' && !isEditable(document.activeElement)) {
        e.preventDefault();
        close();
      }
    });
  });

  // Modal entrance — subtle scale pop. Opacity stays at the SCSS default (1)
  // so an interrupted animation can never park the panel at opacity:0. Scale
  // alone is enough to read as "appearing" without the visibility risk.
  const animateIn = (el: HTMLDivElement) => {
    el.animate(
      [{ transform: 'scale(0.96)' }, { transform: 'scale(1)' }],
      { duration: 160, easing: 'ease-out' },
    );
  };

  return (
    <Show when={drawerState.open}>
      <div class="pengu-settings-overlay">
        <div class="pengu-settings-backdrop" onClick={close} />
        <div
          class="pengu-settings-panel"
          ref={(el) => { panelRef = el; animateIn(el); }}
        >
          <aside class="pengu-settings-sidebar">
            <div class="pengu-settings-sidebar-title">Plugins</div>
            <Show
              when={entries().length > 0}
              fallback={<div class="pengu-settings-sidebar-empty">No plugins have registered settings.</div>}
            >
              <For each={entries()}>
                {(entry) => (
                  <button
                    type="button"
                    class={`pengu-settings-sidebar-item${drawerState.activeId === entry.id ? ' is-active' : ''}`}
                    onClick={() => open(entry.id)}
                  >
                    <Show when={entry.icon}>
                      <span class="pengu-settings-sidebar-icon">{entry.icon}</span>
                    </Show>
                    <span class="pengu-settings-sidebar-name">{entry.name}</span>
                  </button>
                )}
              </For>
            </Show>
          </aside>

          <main class="pengu-settings-content">
            <header class="pengu-settings-header">
              <div class="pengu-settings-header-text">
                <Show when={active()} fallback={<h2 class="pengu-settings-title">Settings</h2>}>
                  {(e) => (
                    <>
                      <h2 class="pengu-settings-title">{e().name}</h2>
                      <Show when={e().description}>
                        <p class="pengu-settings-description">{e().description}</p>
                      </Show>
                    </>
                  )}
                </Show>
              </div>
              <button
                type="button"
                class="pengu-settings-close"
                onClick={close}
                aria-label="Close"
              >
                <svg width="14" height="14" viewBox="0 0 10 10" fill="currentColor">
                  <polygon points="10.2,0.7 9.5,0 5.1,4.4 0.7,0 0,0.7 4.4,5.1 0,9.5 0.7,10.2 5.1,5.8 9.5,10.2 10.2,9.5 5.8,5.1" />
                </svg>
              </button>
            </header>

            <div class="pengu-settings-body">
              <Show
                when={active()}
                fallback={<div class="pengu-settings-placeholder">Select a plugin to configure.</div>}
              >
                {(e) => <Form entry={e()} />}
              </Show>
            </div>
          </main>
        </div>
      </div>
    </Show>
  );
}

// Bind the public surface. `register` is the only generic-typed entry-point;
// the cast preserves its full type for plugin authors via @pengujs/types.
window.Settings = { register, open, close, list } as Settings;
