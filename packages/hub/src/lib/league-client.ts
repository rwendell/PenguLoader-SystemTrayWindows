import { pengu } from './pengu'

/**
 * League of Legends install discovery. Logic lives host-side: the C# host
 * walks `RiotClientInstalls.json` and returns a path; this module is a thin
 * passthrough so existing call sites don't change shape.
 */
export const LeagueClient = {
  /** Validate a folder by checking that `LeagueClientUx.exe` exists in it. */
  validateDir(dir: string): Promise<boolean> {
    if (!dir || typeof dir !== 'string') return Promise.resolve(false)
    return pengu.league.validateDir(dir)
  },

  /** Resolve the LoL install dir from the Riot Client manifest, or null. */
  findLeaguePath(): Promise<string | null> {
    return pengu.league.findInstall()
  },
}
