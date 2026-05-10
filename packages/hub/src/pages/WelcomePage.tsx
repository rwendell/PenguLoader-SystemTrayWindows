import { Component, createSignal, For, Match, Show, Switch } from 'solid-js'
import { Button, ComboBox, Checkbox } from '../components/ui'
import { useI18n } from '../lib/i18n'
import { useConfig } from '../lib/config'
import { PluginIcon, PowerIcon } from '../components/Icons'

/**
 * First-launch welcome tour. Linear flow, four steps:
 *   1. Choose language (saves to config on selection).
 *   2. Terms & Conditions — Continue is gated on the accept checkbox.
 *   3. Activation — explains the green Activate button.
 *   4. Plugins — points at the plugins folder + Plugin Store, then Get Started.
 *
 * Step state is held in the local `step` signal; no Back affordance —
 * each step is a one-direction commit (language can be changed later in
 * Settings, ToS only needs to be agreed once, the info screens are
 * advisory). The CTA reads "Continue" until the last step, where it
 * becomes "Get Started" and fires `onDone`.
 */
const TOTAL_STEPS = 4

export const WelcomePage: Component<{
  onDone: () => void
}> = (props) => {

  const i18n = useI18n()
  const config = useConfig()
  const [accepted, setAccepted] = createSignal(false)
  const [step, setStep] = createSignal(1)

  const selectLang = async (id: string) => {
    i18n.switchTo(id)
    await config.app.language(id)
  }

  const stepTitle = () => {
    switch (step()) {
      case 1: return i18n.t('welcome')
      case 2: return i18n.t('tour_tos_title')
      case 3: return i18n.t('tour_activation_title')
      case 4: return i18n.t('tour_plugins_title')
      default: return ''
    }
  }

  // Continue is enabled by default; ToS step gates on the accept checkbox.
  const canContinue = () => step() !== 2 || accepted()

  const isFirst = () => step() === 1
  const isLast = () => step() === TOTAL_STEPS

  const next = () => {
    if (!canContinue()) return
    if (isLast()) props.onDone()
    else setStep(s => s + 1)
  }

  const back = () => {
    if (!isFirst()) setStep(s => s - 1)
  }

  return (
    <div class="flex flex-col justify-center items-center my-auto w-full">
      <div class="mb-6">
        <h2 class="text-4xl font-semibold text-center">{stepTitle()}</h2>
      </div>
      <div class="flex flex-col max-w-md w-full px-6 gap-6">
        <Switch>
          <Match when={step() === 1}>
            <div>
              <p class="text-sm my-2 pl-2">{i18n.t('choose_lang')}</p>
              <ComboBox
                items={i18n.languages}
                selected={config.app.language()}
                onSelect={selectLang}
              />
            </div>
          </Match>

          <Match when={step() === 2}>
            <>
              {/* Scrollable ToS body — outside the checkbox label so reading
                  it doesn't accidentally toggle the checkbox. */}
              <div class="text-sm text-muted-foreground border border-foreground/10 rounded-md p-3 max-h-48 overflow-y-auto whitespace-pre-line leading-relaxed">
                {i18n.t('tos_content')}
              </div>
              <label class="flex items-center space-x-2 cursor-pointer">
                <Checkbox checked={accepted()} onClick={() => setAccepted(v => !v)} />
                <h2 class="text-sm font-medium select-none">{i18n.t('accept_tos')}</h2>
              </label>
            </>
          </Match>

          <Match when={step() === 3}>
            <div class="flex flex-col items-center text-center gap-4">
              <div class="text-primary p-3 rounded-full bg-primary/10">
                <PowerIcon size={32} thickness={1.75} />
              </div>
              <p class="text-sm text-muted-foreground whitespace-pre-line leading-relaxed">
                {i18n.t('tour_activation_body')}
              </p>
            </div>
          </Match>

          <Match when={step() === 4}>
            <div class="flex flex-col items-center text-center gap-4">
              <div class="text-primary p-3 rounded-full bg-primary/10">
                <PluginIcon size={32} thickness={1.75} />
              </div>
              <p class="text-sm text-muted-foreground whitespace-pre-line leading-relaxed">
                {i18n.t('tour_plugins_body')}
              </p>
            </div>
          </Match>
        </Switch>

        {/* Footer: progress dots on the left, primary CTA on the right.
            Dots are filled for completed/current, faint for upcoming —
            gives a sense of how much tour is left without numeric noise. */}
        <div class="flex items-center justify-between mt-2">
          <div class="flex gap-2">
            <For each={[1, 2, 3, 4]}>
              {n => (
                <div
                  class="size-2 rounded-full transition-colors"
                  classList={{
                    'bg-primary': n === step(),
                    'bg-primary/40': n < step(),
                    'bg-foreground/15': n > step(),
                  }}
                />
              )}
            </For>
          </div>
          <div class="flex items-center gap-2">
            <Show when={!isFirst()}>
              <Button variant="outline" onClick={back} size="sm">
                {i18n.t('tour_back')}
              </Button>
            </Show>
            <Button onClick={next} disabled={!canContinue()} size="sm">
              {isLast() ? i18n.t('get_started') : i18n.t('tour_continue')}
            </Button>
          </div>
        </div>
      </div>
    </div>
  )
}
