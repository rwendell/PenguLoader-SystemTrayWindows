/* @refresh reload */
import { render } from 'solid-js/web'

import App from './App'

window.appVersion = __VERSION__
window.isMac = __PLATFORM__ === 'darwin'

render(() => <App />, document.getElementById('root') as HTMLElement)
