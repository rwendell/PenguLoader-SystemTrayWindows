/// <reference types="vite/client" />

declare const __VERSION__: string
declare const __PLATFORM__: string

declare interface Window {
  isMac: boolean
  appVersion: string
}
