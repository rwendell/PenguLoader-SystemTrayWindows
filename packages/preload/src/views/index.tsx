/* @refresh reload */
import { render } from 'solid-js/web';
import App from './App';
import './style.css';

import install from '@twind/with-web-components';
import config from '../../twind.config';
import { rcp } from '../preload/rcp';
import { loadTranslation } from './lib/i18n';

const rootId = 'pengu-root';
const withTwind = install(config);

class PenguRoot extends withTwind(HTMLElement) {
  constructor() {
    super();
    const shadow = this.attachShadow({ mode: 'open' });
    render(() => <App />, shadow);
  }
}

// Push-style RCP requires subscription before announce. Pre-warm so
// rcp-fe-lol-shared-components is tracked in the registry; mount() below
// then awaits its fulfillment.
rcp.preInit('rcp-fe-lol-shared-components', () => {});

async function mount() {
  // rcp-fe-lol-shared-components does `document.body.innerHTML += ...` during
  // its init. That destroys every custom element inside body — including our
  // <pengu-root> if we mount before it runs — and re-parses them, which fires
  // PenguRoot's constructor a second time and renders a duplicate App tree
  // (visible as the welcome toast firing twice, etc). Wait until the plugin
  // is fulfilled so its body manipulation is done before we attach.
  await rcp.whenReady('rcp-fe-lol-shared-components').catch(() => {});

  await loadTranslation();

  let root = document.getElementById(rootId);
  if (!root) {
    root = document.createElement('div');
    root.setAttribute('id', rootId);
    document.body.appendChild(root);
  }

  await customElements.whenDefined(rootId);
  const twind = document.createElement(rootId);
  root.appendChild(twind);
}

customElements.define(rootId, PenguRoot);
window.addEventListener('load', mount);