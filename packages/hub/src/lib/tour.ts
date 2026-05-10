/**
 * Welcome-tour completion flag, persisted in localStorage. Read
 * synchronously at startup so we know which screen (welcome tour vs
 * main UI) to render before first paint — same pattern as the theme
 * module, no bridge round-trip in the critical path.
 *
 * Lives in localStorage rather than the host-backed pengu config because:
 *   - Read is sync; pengu.config.read() is async and would either flash
 *     the wrong screen briefly or block ready-state on a bridge call.
 *   - The flag is per-install / per-user-data rather than per-League-
 *     account, so localStorage's per-origin scope is the right shape.
 */
const TOUR_KEY = 'pengu:tour-completed'

export function isTourCompleted(): boolean {
  try {
    return localStorage.getItem(TOUR_KEY) === 'true'
  } catch {
    // localStorage may be disabled (privacy mode in some WebViews).
    // Falling back to false makes the tour reappear each launch, which
    // is annoying but functionally correct.
    return false
  }
}

export function markTourCompleted(): void {
  try {
    localStorage.setItem(TOUR_KEY, 'true')
  } catch {
    /* see isTourCompleted — non-fatal, the tour just won't persist */
  }
}

/**
 * Clear the completion flag so the tour shows again on next read of
 * {@link isTourCompleted}. Used by the Settings → About "Read ToS"
 * button — that handler also flips the live welcome signal so the tour
 * appears immediately, not just on next launch.
 */
export function resetTour(): void {
  try {
    localStorage.removeItem(TOUR_KEY)
  } catch {
    /* non-fatal — flag was already absent in disabled-localStorage mode */
  }
}
