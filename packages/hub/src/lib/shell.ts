import { pengu } from './pengu'

/**
 * Thin wrappers over the host bridge's shell methods. The hub uses these
 * everywhere it would have called Tauri's `shell` / `invoke('plugin:shell|...')`.
 */
export const Shell = {
    /** Open a folder in Explorer / Finder. */
    async expandFolder(path: string) {
        await pengu.host.openFolder(path)
    },

    /** Reveal a file (highlighted) in Explorer / Finder. */
    async revealFile(path: string) {
        await pengu.host.revealFile(path)
    },

    /** Open an external URL in the system browser. Hardened to https only. */
    async openLink(url: string) {
        if (typeof url === 'string' && url.startsWith('https://')) {
            await pengu.host.openExternal(url)
        }
    },
}
