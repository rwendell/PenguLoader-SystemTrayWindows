/* @refresh reload */
import { render } from 'solid-js/web';
import App from './App';
import './style.css';

import { rcp } from '../preload/rcp';
import { loadTranslation } from './lib/i18n';

const rootId = 'pengu-root';

// Light DOM (no shadow root) — our CSS uses `pengu-` prefixed classes to avoid
// colliding with LCUX's own styles. We previously used a shadow root for style
// isolation, but it required twind for tailwind-style utilities to scope inside
// the shadow tree, which dragged ~25-30 KB of bundle weight. Plain CSS with
// prefixed classes does the job at near-zero cost.
//
// Render in `connectedCallback`, not the constructor — the Custom Elements
// spec forbids adding children to the element from the constructor (browsers
// silently no-op or throw). connectedCallback runs once the element is
// inserted into the DOM, which is when child insertion is allowed.
class PenguRoot extends HTMLElement {
  private _rendered = false;
  connectedCallback() {
    if (this._rendered) return;
    this._rendered = true;
    render(() => <App />, this);
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
  const el = document.createElement(rootId);
  root.appendChild(el);
}

customElements.define(rootId, PenguRoot);
window.addEventListener('load', mount);