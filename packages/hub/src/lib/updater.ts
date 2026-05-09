import { createSignal } from 'solid-js'
import { pengu, type UpdateInfo } from './pengu'

export type { UpdateInfo }

/**
 * Notification-only updater. The host fetches GitHub Releases and compares
 * versions; this module just wraps the bridge call and exposes the result
 * as a reactive signal so the Settings tab can render it whether the
 * check was triggered by the auto-on-launch path or the manual button.
 *
 * No download / no auto-apply — see docs/app-hub.md §13.
 */

const [available, setAvailable] = createSignal<UpdateInfo | null>(null)
const [error, setError] = createSignal<string | null>(null)

export const Updater = {
  /** UpdateInfo if the most recent check found a newer release; null otherwise. */
  available,

  /** Error message from the most recent check, or null if it succeeded. */
  error,

  /**
   * Run a check now. Stores the outcome in the reactive signals before
   * resolving; rethrows network / HTTP errors so callers can show their
   * own status if they want to differentiate "checking" → "errored".
   */
  async check(): Promise<UpdateInfo | null> {
    setError(null)
    try {
      const info = await pengu.update.check()
      setAvailable(info)
      return info
    } catch (e) {
      const msg = e instanceof Error ? e.message : String(e)
      setError(msg)
      throw e
    }
  },
}
