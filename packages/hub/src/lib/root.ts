import { createRoot, createSignal } from 'solid-js'
import { isTourCompleted } from './tour'

function useSettings() {
  const [visible, setVisible] = createSignal(false)
  const show = () => setVisible(true)
  const hide = () => setVisible(false)

  return {
    visible,
    show, hide,
  }
}

const _root = createRoot(() => {
  const [ready, setReady] = createSignal(false)
  // Initial value from a sync localStorage read (see lib/tour.ts) so the
  // welcome-vs-main decision is made before first paint. Lifted to root
  // so the Settings → About "Read ToS" handler can flip it to re-show
  // the tour without going through App.tsx.
  const [welcome, setWelcome] = createSignal(!isTourCompleted())
  const [isStore, setStore] = createSignal(false)
  const settings = useSettings()

  return {
    ready, setReady,
    welcome, setWelcome,
    isStore, setStore,
    settings,
  }
})

export const useRoot = () => _root