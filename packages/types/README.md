# @pengujs/types

Public TypeScript types for [Pengu Loader](https://github.com/PenguLoader/PenguLoader) plugin authors.

## Install

```sh
npm install --save-dev @pengujs/types
# or
pnpm add -D @pengujs/types
```

## Use

Either reference the package globally:

```ts
/// <reference types="@pengujs/types" />
```

Or include it in your `tsconfig.json`:

```json
{
  "compilerOptions": {
    "types": ["@pengujs/types"]
  }
}
```

After that, every Pengu surface on `window` is type-checked: `window.Pengu`, `window.Settings`, `window.Toast`, `window.DataStore`, `window.CommandBar`, `window.Effect`, `window.os`, etc.

## Settings example

```ts
const settings = window.Settings.register({
  id: 'auto-accept',
  name: 'Auto Accept',
  hotkey: 'Ctrl+Shift+A',
  schema: {
    enabled:   { type: 'boolean', label: 'Enabled',   default: true },
    threshold: { type: 'number',  label: 'Threshold', default: 50, min: 0, max: 100 },
    mode:      { type: 'select',  label: 'Mode',      default: 'fast',
                 options: [{ value: 'fast', label: 'Fast' }, { value: 'precise', label: 'Precise' }] },
  } as const,
  onChange(v) {
    // v is typed as { enabled: boolean; threshold: number; mode: 'fast' | 'precise' }
    console.log('settings changed', v);
  },
});

// Read current values whenever you need them
if (settings.values().enabled) {
  // ...
}

// Mutate from your own UI
settings.set({ enabled: false });
```
