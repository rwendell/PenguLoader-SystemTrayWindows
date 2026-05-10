import { createSignal, onMount, Show } from 'solid-js'
import { Config } from './lib/config'
import { Updater } from './lib/updater'
import { markTourCompleted } from './lib/tour'
import { useRoot } from './lib/root'
import { useI18n } from './lib/i18n'
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
  // The welcome signal lives in root so the Settings → About "Read ToS"
  // handler can flip it back on without a custom event bus. Initial value
  // is set in lib/root.ts from a sync localStorage read.
  const { welcome, setWelcome } = useRoot()

  onMount(async () => {
    await Config.load()
    // Apply the persisted language now that the snapshot is loaded. Picker
    // call sites (WelcomePage, Tab.Pengu) keep calling i18n.switchTo()
    // directly on user change; this bootstrap covers the cold-start case
    // where no one's clicked the language picker yet.
    useI18n().switchTo(Config.get('app', 'language', 'en'))
    setReady(true)
    // Fire-and-forget update check on launch when the user opted in. The
    // result is held in Updater.available() and surfaced in Settings → Pengu.
    if (Config.get('app', 'auto_update_check', true)) {
      void Updater.check().catch(() => { /* surfaced via Updater.error() */ })
    }
  })

  // Final-step CTA in WelcomePage. Persists the flag *before* swapping
  // to the main UI so a crash mid-render still leaves the tour marked
  // done — better to skip a re-show than nag the user every launch.
  const finishTour = () => {
    markTourCompleted()
    setWelcome(false)
  }

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
          fallback={<WelcomePage onDone={finishTour} />}
        >
          <MainPage />
        </Show>
      </Show>
    </div>
  )
}

export default App