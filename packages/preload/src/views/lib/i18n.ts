import { fallback, translations } from '../trans.json';

type Locale = (typeof translations)[number];
export type TranslationKey = Exclude<keyof Locale, '_locales'>;

function findTranslation(locale: string): Locale | undefined {
  if (!locale) return undefined;
  const needle = locale.toLowerCase();
  for (const trans of translations) {
    if (trans._locales.some(l => l.toLowerCase() === needle)) {
      return trans as Locale;
    }
  }
  return undefined;
}

// EN base — resolved once at module load. Used both as the initial active
// locale (so `_t` is callable before `loadTranslation` resolves, which
// matters during the first synchronous render pass) and as the per-key
// fallback when the active locale is missing a translation.
const EN = findTranslation(fallback);
let active: Locale | undefined = EN;

export async function loadTranslation() {
  // Can't read body's dataset before LCUX's locale plugin loads, but the
  // RCS proxy at /riotclient/region-locale is up earlier — same source LCUX
  // itself uses to pick its locale, so we get the right answer first try.
  let locale = fallback;
  try {
    const data = await fetch('/riotclient/region-locale').then(r => r.json());
    if (typeof data?.locale === 'string') {
      locale = data.locale.replace(/_/g, '-');
    }
  } catch {
    // network / parse failure — stay on the en-US fallback.
  }
  active = findTranslation(locale) ?? EN;
}

/**
 * Look up a translation. Per-key fallback chain: active locale → EN base →
 * the key itself. The key-as-fallback is a developer-facing tell that the
 * key is missing from trans.json, but it still reads as a sensible-enough
 * placeholder if it ever surfaces to a user.
 */
export function _t(key: TranslationKey): string {
  return active?.[key] || EN?.[key] || key;
}
