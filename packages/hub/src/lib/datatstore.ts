import { pengu } from './pengu'

/**
 * Read-only browse of `<data_root>/datastore` (the XOR-encoded JSON file the
 * core's `window.DataStore` reads/writes inside LCUX). Decoding happens in
 * C# host-side; the hub just consumes the parsed object.
 *
 * Plugins that need to *write* to the datastore go through the core's
 * `window.DataStore.set` API inside LCUX, not through the hub.
 */
export const DataStore = {
    async json(): Promise<Record<string, unknown>> {
        try {
            return await pengu.host.readDataStore()
        } catch {
            return {}
        }
    },
}
