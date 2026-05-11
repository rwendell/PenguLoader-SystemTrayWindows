// Re-exports from `@pengujs/types` (the public plugin-author types package).
// `index.d.ts` already augments `Window`; the `import('@pengujs/types').*` aliases
// below promote the named exports to ambient globals so internal call sites can
// keep referencing `Action` / `Schema` / `Field` without per-file imports.

import type * as Pengu from '@pengujs/types'

declare global {
  type Action       = Pengu.Action
  type Toast        = Pengu.Toast
  type ToastOptions = Pengu.ToastOptions
  type ToastPosition = Pengu.ToastPosition
  type ToastType    = Pengu.ToastType
  type DataStore    = Pengu.DataStore
  type Effect       = Pengu.Effect
  type Settings     = Pengu.Settings
  type Schema       = Pengu.Schema
  type Field        = Pengu.Field

  interface RcpAnnouceEvent extends CustomEvent {
    errorHandler: () => any
    registrationHandler: (registrar: (e: any) => Promise<any>) => Promise<any> | void
  }

  // Mirrors `PluginModule` in `@pengujs/types`. Inlined for loader.ts.
  interface Plugin {
    init?: (context: any) => any
    load?: () => any
    default?: Function | any
  }
}
