// internal types

interface Plugin {
  init?: (context: any) => any
  load?: () => any
  default?: Function | any
}

interface RcpAnnouceEvent extends CustomEvent {
  errorHandler: () => any
  registrationHandler: (registrar: (e) => Promise<any>) => Promise<any> | void
}

// built-in types

interface Action {
  id?: string
  name: string | (() => string)
  legend?: string | (() => string)
  tags?: string[]
  icon?: string
  group?: string | (() => string)
  hidden?: boolean
  perform?: (id?: string) => any
}

interface CommandBar {
  addAction: (action: Action) => void
  show: () => void
  update: () => void
}

type ToastType = 'success' | 'error' | 'info' | 'warning' | 'loading' | 'custom'

type ToastPosition =
  | 'top-left' | 'top-center' | 'top-right'
  | 'bottom-left' | 'bottom-center' | 'bottom-right'

interface ToastOptions {
  /** ms; 0, negative, or Infinity = sticky. Default 5000; `loading` defaults to sticky. */
  duration?: number
  position?: ToastPosition
  /** Override the type's default glyph. */
  icon?: string
  className?: string
  /** Reusing an id replaces the existing entry — useful for de-duping. */
  id?: string
  /** Show the × button. Default true. */
  dismissable?: boolean
}

interface Toast {
  success: (message: string, opts?: ToastOptions) => string
  error:   (message: string, opts?: ToastOptions) => string
  info:    (message: string, opts?: ToastOptions) => string
  warning: (message: string, opts?: ToastOptions) => string
  /** Sticky by default — pair with `update` / `dismiss` for progressive flows. */
  loading: (message: string, opts?: ToastOptions) => string
  /** Render a custom HTML body. Plugin code is already trusted in this context. */
  custom:  (html: string, opts?: ToastOptions) => string
  promise: <T>(
    promise: Promise<T>,
    msg: { loading: string, success: string, error: string | ((err: unknown) => string) },
    opts?: ToastOptions,
  ) => Promise<T>
  /** Omit `id` to dismiss every toast. */
  dismiss: (id?: string) => void
  update:  (id: string, patch: { message?: string, type?: ToastType, icon?: string }) => void
}

interface DataStore {
  has: (key: string) => boolean
  get: <T>(key: string, fallback?: T) => T | undefined
  set: (key: string, value: any) => boolean
  remove: (key: string) => boolean
}

interface ApplyEffectFn {
  (type: 'transparent' | 'blurbehind' | 'acrylic' | 'unified', options?: { color: string }): void
  (type: 'mica', options?: { material?: 'auto' | 'mica' | 'acrylic' | 'tabbed' }): void
  (type: 'vibrancy', options: { material: string, alwaysOn?: boolean }): void
}

interface Effect {
  apply: ApplyEffectFn
  clear: () => void
  setTheme: (theme: 'light' | 'dark') => void
}

// globals

declare interface Window {

  DataStore: DataStore;
  CommandBar: CommandBar;
  Toast: Toast;
  Effect: Effect;

  Pengu: {
    version: string
    superPotato: boolean
    autoUpdateCheck: boolean
    plugins: string[]
    isMac: boolean
  };

  os: {
    name: 'win' | 'mac'
    version: string
    build: string
  };

  openDevTools: () => void;
  openPluginsFolder: (subdir?: string) => void;
  reloadClient: () => void;
  restartClient: () => void;
  getScriptPath: () => string | undefined;
}