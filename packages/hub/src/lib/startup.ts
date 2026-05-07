import { pengu } from './pengu'

/** Auto-launch on user login. HKCU\...\Run on Windows, LaunchAgent on macOS. */
export const Startup = {
  isEnabled() {
    return pengu.host.startupGetEnabled()
  },

  setEnable(enable: boolean) {
    return pengu.host.startupSetEnabled(enable)
  },
}
