import { Component, createSignal, Match, onMount, Show, Switch } from 'solid-js'
import { Config, useConfig } from '~/lib/config'
import { CheckOption, OptionSet, RadioOption } from './templates'
import { Startup } from '~/lib/startup'
import { pengu } from '~/lib/pengu'
import { Shell } from '~/lib/shell'
import { Updater } from '~/lib/updater'

/**
 * Update controls. Two affordances:
 *   - Auto-check toggle, persisted in `app.auto_update_check`. App.tsx fires
 *     a check on launch when this is true; the result lands in
 *     `Updater.available()` so the status row below mirrors it.
 *   - Manual "Check for update" button — overrides the auto path so the
 *     user can re-check without restarting (e.g. they just bumped past a
 *     release on the page).
 *
 * Status row consumes `Updater.available()` / `Updater.error()` so a
 * launch-time check shows up here without re-fetching when the tab opens.
 */
const UpdateSettings: Component = () => {
  const { app } = useConfig()
  const [checking, setChecking] = createSignal(false)
  const [checkedOnce, setCheckedOnce] = createSignal(false)

  const checkNow = async () => {
    if (checking()) return
    setChecking(true)
    try {
      await Updater.check()
    } catch {
      /* Updater.error() already populated */
    } finally {
      setChecking(false)
      setCheckedOnce(true)
    }
  }

  const openRelease = () => {
    const info = Updater.available()
    if (info) Shell.openLink(info.url)
  }

  return (
    <OptionSet name="App Updates">
      <CheckOption
        caption="Auto check for updates"
        message="Check the GitHub releases page for a newer Pengu version on launch."
        checked={app.auto_update_check()}
        onChange={app.auto_update_check}
      />
      <div class="flex items-center gap-3 flex-wrap">
        <button
          class="inline-flex gap-1 items-center text-sm border border-foreground/10 rounded-sm px-3 py-1 hover:bg-foreground hover:text-background aria-busy:opacity-60 aria-busy:pointer-events-none"
          tabIndex={-1}
          onClick={checkNow}
          aria-busy={checking()}
        >
          {checking() ? 'Checking…' : 'Check for update'}
        </button>
        <Switch>
          <Match when={Updater.available()}>
            <span class="text-sm text-muted-foreground">
              <span class="text-primary font-semibold">{Updater.available()!.tag}</span> available —{' '}
              <a class="underline cursor-pointer text-foreground/80 hover:text-foreground" onClick={openRelease}>view release</a>
            </span>
          </Match>
          <Match when={Updater.error()}>
            <span class="text-sm text-destructive">Failed: {Updater.error()}</span>
          </Match>
          <Match when={checkedOnce() && !Updater.available() && !Updater.error()}>
            <span class="text-sm text-muted-foreground">You're on the latest version (v{window.appVersion}).</span>
          </Match>
        </Switch>
      </div>
    </OptionSet>
  )
}

const LaunchSettings: Component = () => {
  const [startup, setSatrtup] = createSignal(false)

  const toggleStartup = async () => {
    let enable = !await Startup.isEnabled()
    await Startup.setEnable(enable)
    setSatrtup(enable)
  }

  onMount(async () => {
    setSatrtup(await Startup.isEnabled())
  })

  return (
    <OptionSet name="Launch Settings">
      <CheckOption
        caption="Run on startup"
        message="Automatically run Pengu when your computer starts."
        checked={startup()}
        onClick={toggleStartup}
      />
    </OptionSet>
  )
}

export const TabPengu: Component = () => {

  const { app } = useConfig()

  const changePluginsDir = async () => {
    const dir = await pengu.host.pickFolder(Config.basePath())
    if (typeof dir === 'string') {
      await app.plugins_dir(dir)
    }
  }

  return (
    <div class="space-y-4">

      <OptionSet name="Plugins Folder">
        <span
          class="block text-base text-neutral-200 px-3 py-1 hover:bg-neutral-400/20 rounded-md"
          onClick={changePluginsDir}>
          {app.plugins_dir() || Config.basePath('plugins')}
        </span>
      </OptionSet>

      <Show when={window.isMac}>
        <LaunchSettings />
      </Show>

      <OptionSet name="Activation Mode">
        <Show when={!window.isMac}>
          {/* Windows: Universal (IFEO) only. OnDemand was considered as a
              second mode but dropped — IFEO is strictly more reliable on
              Windows (no daemon required, survives reboots, kernel-level
              redirect). The radio stays as a single visible option for
              clarity rather than collapsing to a label. */}
          <RadioOption
            caption="Universal"
            message="Apply to all League Clients via IFEO. Requires UAC once at install; survives across launches."
            checked
            disabled
          />
        </Show>
        <Show when={window.isMac}>
          <RadioOption
            caption="On-demand"
            message="Apply to a specific League Client that you launch from the Riot Client. You have to keep Pengu running in background."
            disabled
            checked
          />
        </Show>
      </OptionSet>

      <UpdateSettings />

    </div>
  )
}
