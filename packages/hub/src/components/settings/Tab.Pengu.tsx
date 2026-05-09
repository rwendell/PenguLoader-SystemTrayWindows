import { Component, createSignal, onMount, Show } from 'solid-js'
import { Config, useConfig } from '~/lib/config'
import { CheckOption, OptionSet, RadioOption } from './templates'
import { Startup } from '~/lib/startup'
import { pengu } from '~/lib/pengu'

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

    </div>
  )
}
