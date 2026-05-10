import { createRoot } from 'solid-js'
import { createStore } from 'solid-js/store'
import translations from '../../translations.json'

/**
 * i18n singleton. Owns a `current` store of translation strings keyed by
 * id, and a `switchTo(id)` to swap to a different language.
 *
 * Behavior contract:
 *   - Initial state: English.
 *   - Bootstrap: App.tsx calls `useI18n().switchTo(loadedLang)` after
 *     `Config.load()` resolves. Picker callers (WelcomePage,
 *     Tab.Pengu) call `switchTo` directly when the user changes language.
 *   - Unknown language id → fall back to EN (no silent no-op).
 *   - Missing keys in the chosen language → fall back to EN per-key
 *     (we spread EN first so the merge can't leak previous-language
 *     strings through Solid's createStore top-level merge semantics).
 *
 * `useI18n` is a pure getter; previous versions called `switchTo`
 * inside it, which fired a redundant store write on every call and
 * coupled translation changes to component re-render timing.
 */
const EN = translations.languages[0]
type TranslationKey = keyof typeof EN.translations
type TranslationMap = Record<TranslationKey, string>

const _i18n = createRoot(() => {
  const [current, set] = createStore<TranslationMap>({ ...EN.translations })

  const languages = translations.languages.map((x) => ({
    id: x.id,
    name: x.name,
  }))

  const switchTo = (id: string) => {
    const lang = translations.languages.find(l => l.id === id) ?? EN
    set({ ...EN.translations, ...lang.translations })
  }

  const text = (key: TranslationKey): string =>
    key in current ? current[key] : `{{${key}}}`

  return {
    languages,
    switchTo,
    t: text,
  }
})

export const useI18n = () => _i18n
