import { Show, createSignal } from 'solid-js';
import penguLogo from '../assets/pengu.jpg';
import { _t } from '../lib/i18n';
import './Welcome.scss';

export function Welcome() {

  const welcome = window.DataStore?.get<boolean>('pengu-welcome', true) !== false;
  const [visible, show] = createSignal(welcome);

  const dontShowCheck = (e: Event & { currentTarget: HTMLInputElement }) => {
    const value = !e.currentTarget.value;
    window.DataStore?.set('pengu-welcome', value);
  };

  const hide = () => {
    show(false);
  };

  // if (!welcome) {
  //   onMount(() => {
  //     toast.success(_t('active_status'), {
  //       position: 'bottom-left',
  //       duration: 7000
  //     });
  //   });
  // }

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
