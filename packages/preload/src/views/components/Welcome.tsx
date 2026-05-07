import { Show, createSignal, onMount } from 'solid-js';
import penguLogo from '../assets/pengu.jpg';
import { toast } from './Toaster';
import { _t } from '../lib/i18n';
import { fetchUpdate } from '../lib/updater';
import './Welcome.css';

async function doCheckUpdate() {
  const update = await fetchUpdate();
  if (update === false) return;

  toast.custom((t) => {
    return (
      <div class={`pengu-update-toast ${!t.visible ? 'is-hidden' : ''}`}>
        <div class="pengu-update-toast-body">
          <div class="pengu-update-toast-row">
            <div class="pengu-update-toast-icon">
              <svg width="24" height="24" viewBox="0 0 24 24" stroke-width="2" stroke="currentColor" fill="none" stroke-linecap="round" stroke-linejoin="round">
                <path stroke="none" d="M0 0h24v24H0z" fill="none"></path>
                <path d="M9 12h-3.586a1 1 0 0 1 -.707 -1.707l6.586 -6.586a1 1 0 0 1 1.414 0l6.586 6.586a1 1 0 0 1 -.707 1.707h-3.586v3h-6v-3z"></path>
                <path d="M9 21h6"></path>
                <path d="M9 18h6"></path>
              </svg>
            </div>
            <div class="pengu-update-toast-content">
              <p class="pengu-update-toast-title">{_t('update_available')} - {update.version}</p>
              <p class="pengu-update-toast-hint">{_t('update_hint')}</p>
            </div>
            <div class="pengu-update-toast-actions">
              <button
                class="pengu-update-toast-close"
                onClick={() => toast.dismiss(t.id)}
              >
                <svg class="pengu-update-toast-close-icon" viewBox="0 0 20 20" fill="currentColor">
                  <path fill-rule="evenodd" d="M4.293 4.293a1 1 0 011.414 0L10 8.586l4.293-4.293a1 1 0 111.414 1.414L11.414 10l4.293 4.293a1 1 0 01-1.414 1.414L10 11.414l-4.293 4.293a1 1 0 01-1.414-1.414L8.586 10 4.293 5.707a1 1 0 010-1.414z" clip-rule="evenodd" />
                </svg>
              </button>
            </div>
          </div>
        </div>
      </div>
    )
  }, { duration: 30000, position: 'bottom-left' });
}

export function Welcome() {

  const welcome = window.DataStore?.get<boolean>('pengu-welcome', true) !== false;
  const [visible, show] = createSignal(welcome);

  const dontShowCheck = (e) => {
    const value = !e.currentTarget.value;
    window.DataStore?.set('pengu-welcome', value);
  };

  const hide = () => {
    show(false);
  };

  if (!welcome) {
    onMount(() => {
      toast.success(_t('active_status'), {
        position: 'bottom-left',
        duration: 7000
      });
    });
  }

  onMount(doCheckUpdate);

  return (
    <Show when={visible()}>
      <div class="pengu-welcome">
        <div class="pengu-welcome-backdrop"></div>
        <div class="pengu-welcome-overlay">
          <div class="pengu-welcome-center">
            <div class="pengu-welcome-modal">
              <div class="pengu-welcome-body">
                <div class="pengu-welcome-row">
                  <div class="pengu-welcome-logo-wrap">
                    <img src={penguLogo} class="pengu-welcome-logo" alt="" />
                  </div>
                  <div class="pengu-welcome-text">
                    <h3 class="pengu-welcome-title">Pengu Loader</h3>
                    <div class="pengu-welcome-desc">
                      <div class="pengu-welcome-msg">{_t('welcome_msg')}</div>
                      <div class="pengu-welcome-badges">
                        <a href="https://chat.pengu.lol/" target="_blank" rel="noreferrer" class="pengu-welcome-badge">
                          <img src="https://img.shields.io/discord/1069483280438673418?style=flat-square&logo=discord&logoColor=white&label=discord&color=5c5fff" alt="" />
                        </a>
                        <a href="https://pengu.lol/" target="_blank" rel="noreferrer" class="pengu-welcome-badge">
                          <img src="https://img.shields.io/badge/-pengu.lol-607080.svg?&style=flat-square&logo=gitbook&logoColor=white" alt="" />
                        </a>
                        <a href="https://github.com/PenguLoader/PenguLoader/" target="_blank" rel="noreferrer" class="pengu-welcome-badge">
                          <img src="https://img.shields.io/github/stars/PenguLoader/PenguLoader?style=flat-square&logo=github" alt="" />
                        </a>
                      </div>
                    </div>
                  </div>
                </div>
              </div>
              <div class="pengu-welcome-footer">
                <div class="pengu-welcome-checkbox-wrap">
                  <input type="checkbox" id="TxrO6Gew" onChange={dontShowCheck} class="pengu-welcome-checkbox" />
                  <label for="TxrO6Gew" class="pengu-welcome-checkbox-label">{_t('dont_show_again')}</label>
                </div>
                <button
                  onClick={hide}
                  type="button"
                  class="pengu-welcome-ok"
                >Okay</button>
              </div>
            </div>
          </div>
        </div>
      </div>
    </Show>
  )
}
