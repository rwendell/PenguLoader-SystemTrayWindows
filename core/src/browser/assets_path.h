#pragma once

// Path-sandboxing helper for the plugins scheme handler and the upcoming
// writable-JSON `$write` binding. Both code paths need to verify that a
// caller-supplied URL resolves to a filesystem path that stays inside an
// expected root directory — otherwise a plugin (or stray eval'd code) could
// read or overwrite files outside its scope via `..` traversal.

#include "pengu.h"   // for `path` alias and OS_* macros (`./src` is on the include path)

#include <cstring>

namespace assets
{
    /// Returns true if the lexically-normalized `candidate` is contained
    /// within the lexically-normalized `root` (or is identical to it).
    ///
    /// Uses `lexically_normal` (string-only — no FS calls) so it's cheap
    /// to call on every request. Symlinks inside the path are NOT resolved;
    /// Pengu's data root doesn't use symlinks in any supported setup.
    ///
    /// On Windows the comparison is case-insensitive (NTFS default).
    /// On macOS it's case-sensitive (matches APFS / case-sensitive HFS+).
    inline bool is_inside(const path &root, const path &candidate)
    {
        path c = candidate.lexically_normal();
        c.make_preferred();
        path r = root.lexically_normal();
        r.make_preferred();

        const auto &cs = c.native();
        const auto &rs = r.native();

        if (cs.length() < rs.length())
            return false;

#if OS_WIN
        if (_wcsnicmp(cs.data(), rs.data(), rs.length()) != 0)
            return false;
#else
        if (std::memcmp(cs.data(), rs.data(), rs.length() * sizeof(rs[0])) != 0)
            return false;
#endif

        // After matching the root prefix, the candidate must either end there
        // (candidate == root) or have a separator next — otherwise we'd accept
        // `<root>foo` as inside `<root>`.
        if (cs.length() > rs.length() && cs[rs.length()] != path::preferred_separator)
            return false;

        return true;
    }
}
