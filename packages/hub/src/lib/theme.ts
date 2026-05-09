/**
 * Theme presets — picked at runtime, written to `:root` CSS custom
 * properties so every Tailwind utility that reads through the theme
 * (`bg-primary`, `text-primary`, `border-primary`, `ring-...`, etc.)
 * follows automatically without component-level changes.
 *
 * The triplets mirror the format App.css uses:
 *   --primary               → "<r> <g> <b>"   (consumed via rgb(var(...) / a))
 *   --primary-foreground    → "<h> <s>% <l>%" (consumed via hsl(var(...)))
 *   --ring                  → "<h> <s>% <l>%"
 *
 * Foreground is intentionally a single dark slate across all four presets:
 * each `--primary` is a bright mid-tone that wants near-black for AA
 * contrast, and reusing the same color keeps the visual rhythm consistent
 * when the user swaps themes mid-session.
 *
 * Persistence lives in `localStorage` rather than the host-backed pengu
 * config. The pengu-config path goes through an async bridge round-trip,
 * which means the first read happens after first paint — leaving a
 * visible flash while the page settles to the saved theme. localStorage
 * is synchronous and same-process, so the persisted theme is applied
 * before React/Solid renders anything.
 */
import { createSignal } from 'solid-js'

/** rgba() tuple for one gradient stop: r, g, b (0–255), alpha (0–1). */
type Rgba = [number, number, number, number]

export interface Theme {
  id: string
  name: string
  /** rgb(r, g, b) for `--primary`. */
  primary: [number, number, number]
  /** hsl(h, s%, l%) for `--ring`, hue-matched to primary. */
  ring: [number, number, number]
  /**
   * Three stops for the ambient blur glow at the top of the hub. Walks
   * deep → bright (the accent) → light within the same hue family. Alpha
   * ramp `0.22 → 0.32 → 0.42` is consistent across themes so the glow's
   * intensity profile doesn't shift when the user swaps colors.
   */
  glow: [Rgba, Rgba, Rgba]
}

const FOREGROUND: [number, number, number] = [222, 47, 11]

export const Themes: Theme[] = [
  {
    id: 'blue',
    name: 'Pengu Blue',
    // sky-400 — matches the bright cyan-blue in the logo's beanie.
    primary: [56, 189, 248],
    ring:    [199, 94, 59],
    glow: [
      [0,   71,  225, 0.22], // blue-700-ish royal
      [26,  214, 255, 0.32], // bright cyan-blue, near the accent
      [125, 211, 252, 0.42], // sky-300 light
    ],
  },
  {
    id: 'green',
    name: 'Mint Green',
    // green-500 — the original v1.2 design accent before the blue palette
    // landed. Kept as a first-class theme for users who prefer it.
    primary: [34, 197, 94],
    ring:    [142, 71, 45],
    glow: [
      [21,  128, 61,  0.22], // green-700
      [74,  222, 128, 0.32], // green-400
      [187, 247, 208, 0.42], // green-200
    ],
  },
  {
    id: 'purple',
    name: 'Royal Purple',
    // purple-500.
    primary: [168, 85, 247],
    ring:    [271, 91, 65],
    glow: [
      [107, 33,  168, 0.22], // purple-800
      [192, 132, 252, 0.32], // purple-400
      [233, 213, 255, 0.42], // purple-200
    ],
  },
  {
    id: 'pink',
    name: 'Sakura Pink',
    // pink-500.
    primary: [236, 72, 153],
    ring:    [330, 81, 60],
    glow: [
      [190, 24,  93,  0.22], // pink-700
      [244, 114, 182, 0.32], // pink-400
      [251, 207, 232, 0.42], // pink-200
    ],
  },
  {
    id: 'yellow',
    name: 'Lemon Yellow',
    // yellow-400 — bright lemon, contrasts cleanly against the dark card
    // and shares the same near-black foreground as the rest.
    primary: [250, 204, 21],
    ring:    [50, 96, 53],
    glow: [
      [161, 98,  7,   0.22], // yellow-700 (golden brown)
      [250, 204, 21,  0.32], // yellow-400 (the accent)
      [254, 240, 138, 0.42], // yellow-200 (pale lemon)
    ],
  },
]

const DEFAULT_ID = 'blue'
const STORAGE_KEY = 'pengu:theme'

export function findTheme(id: string | undefined): Theme {
  return Themes.find(t => t.id === id) ?? Themes.find(t => t.id === DEFAULT_ID)!
}

function loadStoredId(): string {
  try {
    const v = localStorage.getItem(STORAGE_KEY)
    return v && Themes.some(t => t.id === v) ? v : DEFAULT_ID
  } catch {
    // localStorage may be disabled (privacy mode in some WebViews); fall
    // back to the default. Theme picks during this session work in-memory
    // via the signal but won't persist across launches.
    return DEFAULT_ID
  }
}

const [activeThemeId, setActiveThemeId] = createSignal(loadStoredId())

/** Reactive accessor for the currently-applied theme id. Used by the
 *  picker to decorate the active swatch. */
export const themeId = activeThemeId

/**
 * Switch theme: update the in-memory signal, persist to localStorage, and
 * write the four CSS variables. The picker calls this; everything else
 * stays consistent because every other consumer reads through the CSS
 * vars (set here) or the {@link themeId} signal (set here too).
 */
export function setTheme(id: string): void {
  if (!Themes.some(t => t.id === id)) return
  setActiveThemeId(id)
  try {
    localStorage.setItem(STORAGE_KEY, id)
  } catch {
    /* see loadStoredId — non-fatal, in-memory state still updates */
  }
  applyTheme(id)
}

/**
 * Apply a theme by writing the four CSS variables on the document root:
 * `--primary`, `--primary-foreground`, `--ring`, and `--bg-glow-stops`.
 * Idempotent — calling repeatedly with the same id is a no-op visually.
 *
 * Public so an external trigger (e.g. a future "system colors changed"
 * hook) can re-apply, but normal flow goes through {@link setTheme}.
 */
export function applyTheme(id: string | undefined): void {
  const theme = findTheme(id)
  const root = document.documentElement
  root.style.setProperty('--primary', `${theme.primary[0]} ${theme.primary[1]} ${theme.primary[2]}`)
  root.style.setProperty('--primary-foreground', `${FOREGROUND[0]} ${FOREGROUND[1]}% ${FOREGROUND[2]}%`)
  root.style.setProperty('--ring', `${theme.ring[0]} ${theme.ring[1]}% ${theme.ring[2]}%`)
  root.style.setProperty('--bg-glow-stops', stopsToCss(theme.glow))
}

function stopsToCss(stops: readonly Rgba[]): string {
  return stops.map(([r, g, b, a]) => `rgba(${r},${g},${b},${a})`).join(',')
}

/** Inline-style RGB for swatch buttons in the picker. */
export function themeSwatchStyle(theme: Theme): string {
  return `background-color: rgb(${theme.primary[0]}, ${theme.primary[1]}, ${theme.primary[2]})`
}

// Apply the persisted theme synchronously at module load — before any
// component mounts and before the browser does its first paint. This
// closes the cold-start flash window where CSS defaults from App.css
// briefly showed Pengu Blue while the loaded theme caught up.
applyTheme(activeThemeId())
