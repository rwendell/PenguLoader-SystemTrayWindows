import { Component, createSignal, onMount } from 'solid-js'
import { Dynamic } from 'solid-js/web'
import { CoreModule } from '../lib/core-module'
import { BoltIcon, PowerIcon } from './Icons'

export const Activator: Component = () => {

  const [loading, setLoading] = createSignal(true)
  const [active, setActive] = createSignal(false)

  const activate = async () => {
    if (!loading()) {
      setLoading(true)

      try {
        if (!await CoreModule.exists()) {
          // TODO(overlay): replace browser alert with the in-app message overlay
          // once the component lands. Native dialogs are out of scope for app/.
          alert('Failed to perform activation, the core module is not found.')
          return
        }

        const nextActive = !active()
        const { activated, error } = await CoreModule.doActivate(nextActive)

        if (error) {
          alert(`Failed to perform activation, got error:\n${error}`)
        } else if (activated === nextActive) {
          setActive(activated)
        }
      }
      finally {
        setLoading(false)
      }
    }
  }

  onMount(async () => {
    setActive(await CoreModule.isActivated())
    setLoading(false)

    // Daemon-driven state changes (RCS WAMP detect / tray toggle). Replaces
    // Tauri's `event.listen('active-status', ...)`. The host emits
    // `activation:stateChanged { active: boolean }` from C# (see
    // docs/app-hub.md §8.8) once activation lands; this listener is a no-op
    // until then.
    window.addEventListener('activation:stateChanged', (e) => {
      const detail = (e as CustomEvent<{ active: boolean }>).detail
      if (detail && typeof detail.active === 'boolean') {
        setActive(detail.active)
      }
    })
  })

  return (
    <div
      class="fixed bottom-6 right-0 z-10 translate-x-28 hover:translate-x-0 transition-transform"
    >
      <div
        class="flex items-center justify-between pl-3 shadow-lg w-44 h-14 rounded-l-full border border-neutral-700/30 border-r-0
        cursor-pointer aria-disabled::cursor-not-allowed group bg-card aria-checked:bg-primary
        hover:shadow-xl transition-colors ease-out duration-300"
        aria-disabled={loading()}
        aria-checked={active()}
        onClick={activate}
      >
        <div
          class="flex items-center justify-center size-8 text-primary rounded-full group-hover:bg-primary
          aria-checked:bg-muted group-hover:text-accent group-hover:aria-checked:bg-muted group-hover:aria-checked:text-primary"
          aria-checked={active()}>
          <span class="group-hover:animate-pulse">
            <Dynamic component={active() ? BoltIcon : PowerIcon} thickness={2.5} />
          </span>
        </div>
        <div
          class="flex-1 px-6 text-lg text-center font-semibold text-primary aria-checked:text-muted"
          aria-checked={active()}
        >{active() ? 'READY' : 'Activate'}</div>
      </div>
    </div>
  )
}
