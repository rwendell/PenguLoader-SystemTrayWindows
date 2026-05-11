const GITHUB_REPO = 'PenguLoader/PenguLoader';
const RELEASE_URL = `https://api.github.com/repos/${GITHUB_REPO}/releases/latest`;

export interface UpdateInfo {
  /** Release tag, e.g. "v1.2.0". */
  tag: string;
  /** Web URL of the GitHub release page — opened by the banner's "View release" link. */
  url: string;
}

/**
 * Parse a version string into a comparable integer. Strict on shape: `vX.Y.Z`
 * or `X.Y.Z` with an optional `.W` build segment, each segment 0-9999. Returns
 * 0 for unparseable input so callers see "no update" rather than throwing —
 * we'd rather skip the banner once than crash the renderer's plugin host.
 */
function parseVersion(version: string): number {
  const match = /v?(\d+)\.(\d+)\.(\d+)(?:\.(\d+))?/i.exec(version);
  if (!match) return 0;
  const [, a, b, c, d] = match;
  return (
    Number(a) * 1_0000_0000_0000 +
    Number(b) * 1_0000_0000 +
    Number(c) * 1_0000 +
    Number(d ?? 0)
  );
}

/**
 * Fetch the latest published release from GitHub and compare to the running
 * build. Returns the release info when newer, null otherwise (also null on
 * any network / parse error so callers can treat "no update" and "couldn't
 * check" the same way — there's nothing useful to show the user either way).
 */
export async function fetchUpdate(): Promise<UpdateInfo | null> {
  const current = window.Pengu?.version ?? '';
  if (!current) return null;

  try {
    const res = await fetch(RELEASE_URL);
    if (!res.ok) return null;

    const release = await res.json();
    const tag: string = release?.tag_name ?? '';
    const url: string = release?.html_url ?? `https://github.com/${GITHUB_REPO}/releases/latest`;

    if (parseVersion(tag) > parseVersion(current)) {
      return { tag, url };
    }
  } catch (err) {
    console.warn('Pengu failed to fetch update.', err);
  }
  return null;
}
