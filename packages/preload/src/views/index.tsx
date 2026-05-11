/* @refresh reload */
import { render } from 'solid-js/web';
import App from './App';
import './style.css';

import { rcp } from '../preload/rcp';
import { loadTranslation } from './lib/i18n';

// Light DOM. Our CSS uses `pengu-` prefixed classes (see styles/_tokens.scss
// and per-component .scss files) to avoid colliding with LCUX's own styles —
// no shadow root required.

const rootId = 'pengu-root';

// Push-style RCP requires subscription before announce. Pre-warm so
// rcp-fe-lol-shared-components is tracked in the registry; mount() below
// then awaits its fulfillment.
rcp.preInit('rcp-fe-lol-shared-components', () => {});

async function mount() {
  // rcp-fe-lol-shared-components does `document.body.innerHTML += ...` during
  // its init. Wait until it's fulfilled so its body manipulation is done
  // before we attach — otherwise our root gets destroyed and re-parsed,
  // remounting <App /> as a duplicate tree.
  await rcp.whenReady('rcp-fe-lol-shared-components').catch(() => {});

  await loadTranslation();

  let root = document.getElementById(rootId);
  if (!root) {
    root = document.createElement('div');
    root.setAttribute('id', rootId);
    document.body.appendChild(root);
  }

  render(() => <App />, root);
}

window.addEventListener('load', mount);
