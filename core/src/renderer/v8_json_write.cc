#include "pengu.h"
#include "v8_wrapper.h"
#include "../browser/assets_path.h"

#include <cstdio>
#include <cstring>
#include <string>

// =============================================================================
// `__pengu_write_json(url, content)` — back-end of the writable-JSON module's
// `data.$write()`. The shim in `assets_shims.h SCRIPT_IMPORT_JSON` captures
// the module's `import.meta.url` and calls into here with the current
// `JSON.stringify(data)`. We path-sandbox the URL against the plugins dir
// and write atomically via temp-file + rename.
// =============================================================================

static constexpr size_t URL_PREFIX_LEN = 15;  // "https://plugins"

/// Temp-file + atomic rename. Crash mid-write leaves the .tmp orphaned but
/// the canonical file intact. The temp lives in the same directory as the
/// target so the rename stays on one volume (POSIX requirement for atomicity).
static bool atomic_write(const path &target, const std::string &body)
{
    path tmp_path(target);
#if OS_WIN
    tmp_path += L".tmp";
#else
    tmp_path += ".tmp";
#endif

    if (!file::write_file(tmp_path, body.data(), body.size()))
        return false;

#if OS_WIN
    if (!MoveFileExW(tmp_path.c_str(), target.c_str(),
                     MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGH))
    {
        DeleteFileW(tmp_path.c_str());  // best-effort cleanup
        return false;
    }
#else
    if (std::rename(tmp_path.c_str(), target.c_str()) != 0)
    {
        std::remove(tmp_path.c_str());
        return false;
    }
#endif

    return true;
}

static V8Value *v8_write_json(V8Value *const args[], int argc)
{
    auto *task = new V8PromiseTask();
    auto *promise = task->promise();

    // Validate arity + types. Reject syncronously (the reject still hops to
    // TID_RENDERER, but we don't burn a worker slot).
    if (argc < 2 || !args[0]->isString() || !args[1]->isString())
    {
        task->reject("WriteJson: expected (url: string, content: string)");
        return promise;
    }

    CefScopedStr url = args[0]->asString();
    CefScopedStr content = args[1]->asString();

    // URL must start with "https://plugins". The shim's `import.meta.url`
    // always satisfies this; a hand-built call from plugin code (going around
    // the shim) is the path we're guarding here.
    if (url.length < URL_PREFIX_LEN ||
        std::memcmp(url.str, u"https://plugins",
                    URL_PREFIX_LEN * sizeof(char16)) != 0)
    {
        task->reject("WriteJson: URL must be a https://plugins/ URL");
        return promise;
    }

    // Build candidate filesystem path.
    std::u16string rel((char16_t *)url.str + URL_PREFIX_LEN,
                       url.length - URL_PREFIX_LEN);

    // Strip query string if any (matches the resource handler's behavior).
    if (auto pos = rel.find('?'); pos != std::u16string::npos)
        rel = rel.substr(0, pos);

    assets::decode_uri(rel);

    auto full = config::plugins_dir().u16string() + rel;
    path target{ full };

    // Path-traversal sandbox. Same helper the resource handler uses.
    if (!assets::is_inside(config::plugins_dir(), target))
    {
        task->reject("WriteJson: path escapes plugins directory");
        return promise;
    }

    // UTF-16 → UTF-8 for the on-disk bytes. Done here (renderer thread) so
    // the worker only does file I/O.
    cef_string_utf8_t utf8{};
    cef_string_to_utf8(content.str, content.length, &utf8);
    std::string body(utf8.str, utf8.length);
    cef_string_utf8_clear(&utf8);

    task->execute([task, target, body = std::move(body)] {
        if (atomic_write(target, body))
            task->resolve();
        else
            task->reject("WriteJson: write failed");
    });

    return promise;
}

V8HandlerFunctionEntry v8_JsonWriteEntries[]
{
    { "WriteJson", v8_write_json },
    { nullptr }
};
