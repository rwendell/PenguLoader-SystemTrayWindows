import { createSignal, onMount, Show } from 'solid-js'
import { Config } from './lib/config'
import { Updater } from './lib/updater'
import { WelcomePage } from './pages/WelcomePage'
import { Appbar } from './components/Appbar'
import { MainPage } from './pages/MainPage'

import './App.css'
// Side-effect import: the theme module reads localStorage and applies the
// persisted accent to `:root` synchronously on first load, so the user's
// saved palette is in place before this component renders.
import './lib/theme'
import 'tippy.js/dist/tippy.css'

function App() {
  const [ready, setReady] = createSignal(false)
  const [welcome, setWelcome] = createSignal(true)

  onMount(async () => {
    setWelcome(!await Config.load())
    setReady(true)
    // Fire-and-forget update check on launch when the user opted in. The
    // result is held in Updater.available() and surfaced in Settings → Pengu.
    if (Config.get('app', 'auto_update_check', true)) {
      void Updater.check().catch(() => { /* surfaced via Updater.error() */ })
    }
  })

  return (
    <div class="h-screen flex flex-col">
      {/* Soft ambient glow at the top of the hub. Stops come from the
          active theme via `--bg-glow-stops` (see lib/theme.ts), so the
          gradient hue follows the accent picker — blue / green / purple
          / pink each get their own deep → bright → light walk in the
          same family. Heavily blurred so it reads as a mood layer rather
          than a defined shape. */}
      <div class="blur-[140px] h-[10rem] max-w-[40rem] absolute top-[10rem] z-10 pointer-events-none w-[-webkit-fill-available]">
        <div class="w-full h-full bg-[linear-gradient(97.62deg,var(--bg-glow-stops))]">
        </div>
      </div>
      <Show when={ready()}>
        <Appbar isHome={!welcome()} />
        <Show
          when={!welcome()}
          fallback={<WelcomePage onDone={() => setWelcome(false)} />}
        >
          <MainPage />
        </Show>
      </Show>
    </div>
  )
}

export default App