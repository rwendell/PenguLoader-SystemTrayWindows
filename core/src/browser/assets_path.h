#pragma once

// Path-sandboxing + URI decoding helpers for the plugins scheme handler
// and the writable-JSON `$write` binding. Both code paths need to verify
// that a caller-supplied URL resolves to a filesystem path that stays inside
// an expected root directory — otherwise a plugin (or stray eval'd code)
// could read or overwrite files outside its scope via `..` traversal.

#include "pengu.h"   // for `path` alias and OS_* macros (`./src` is on the include path)
#include "include/capi/cef_parser_capi.h"

#include <cstring>
#include <string>

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

    /// Percent-decode a UTF-16 URL path in place. Path separators stay
    /// escaped (per UU_URL_SPECIAL_CHARS_EXCEPT_PATH_SEPARATORS) so a `%2F`
    /// in a filename can't fake a directory split.
    inline void decode_uri(std::u16string &uri)
    {
        auto rule = cef_uri_unescape_rule_t(UU_SPACES
            | UU_URL_SPECIAL_CHARS_EXCEPT_PATH_SEPARATORS);

        cef_string_t input{ (char16 *)uri.data(), uri.length(), nullptr };
        CefScopedStr output{ cef_uridecode(&input, true, rule) };

        uri.assign((char16_t *)output.str, output.length);
    }
}
