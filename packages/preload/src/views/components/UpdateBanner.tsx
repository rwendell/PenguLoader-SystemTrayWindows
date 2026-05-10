import { Show, createSignal, onMount } from 'solid-js';
import { _t } from '../lib/i18n';
import { fetchUpdate, type UpdateInfo } from '../lib/updater';
import './UpdateBanner.scss';

/**
 * Fixed-bottom banner that mirrors the hub's UpdateBanner, rendered inside
 * LCUX so users see "an update is available" without having to open the hub.
 * Gated on `Pengu.autoUpdateCheck` (same `app.auto_update_check` config key
 * the hub reads — toggling Settings → Pengu in the hub controls both).
 *
 * No download / no auto-apply — clicking through opens the GitHub release
 * page in the system browser (via `window.open`, which CEF routes to the
 * default OS handler since the page origin is not `https://riot:`).
 */
export function UpdateBanner() {
  const [info, setInfo] = createSignal<UpdateInfo | null>(null);
  const [dismissed, setDismissed] = createSignal(false);

  onMount(() => {
    if (!window.Pengu?.autoUpdateCheck) return;
    void fetchUpdate().then(setInfo);
  });

  const open = () => {
    const i = info();
    if (i) window.open(i.url, '_blank');
  };

  return (
    <Show when={info() && !dismissed()}>
      <div class="pengu-update-banner">
        <div class="pengu-update-banner-text">
          <span class="pengu-update-banner-tag">Pengu {info()!.tag}</span>
          <span class="pengu-update-banner-hint">{_t('update_hint')}</span>
        </div>
        <div class="pengu-update-banner-actions">
          <button
            type="button"
            class="pengu-update-banner-link"
            onClick={open}
            tabIndex={-1}
          >
            {_t('update_available')}
          </button>
          <button
            type="button"
            class="pengu-update-banner-close"
            onClick={() => setDismissed(true)}
            tabIndex={-1}
            aria-label="Dismiss"
            title="Dismiss"
          >
            <svg width="12" height="12" viewBox="0 0 10 10" fill="currentColor">
              <polygon points="10.2,0.7 9.5,0 5.1,4.4 0.7,0 0,0.7 4.4,5.1 0,9.5 0.7,10.2 5.1,5.8 9.5,10.2 10.2,9.5 5.8,5.1" />
            </svg>
          </button>
        </div>
      </div>
    </Show>
  );
}
