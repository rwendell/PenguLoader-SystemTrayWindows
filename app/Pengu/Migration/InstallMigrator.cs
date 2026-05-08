using Pengu.Logging;

namespace Pengu.Migration;

/// <summary>
/// One-shot migrator that moves user state from the legacy install-dir layout
/// (where <c>config</c>, <c>datastore</c>, and <c>plugins/</c> sat next to
/// <c>Pengu.exe</c>) into the new <c>%LOCALAPPDATA%\.pengu\</c> root.
///
/// <para>Runs every launch but only acts when both conditions hold:</para>
/// <list type="bullet">
///   <item><description>The legacy item exists in <paramref name="installDir"/>.</description></item>
///   <item><description>The corresponding item under <paramref name="dataRoot"/> doesn't exist yet.</description></item>
/// </list>
///
/// <para>So a freshly migrated install no-ops on subsequent launches, and a
/// user who deliberately keeps a per-install config (e.g. a portable copy on
/// a USB drive — though we don't officially support that) won't get clobbered
/// once they have a data-root entry too.</para>
///
/// <para>Cross-volume safety: <see cref="Directory.Move"/> fails when source
/// and destination span different drives (common: install on D:\Games,
/// AppData on C:). We catch <see cref="IOException"/> from the move and fall
/// back to a recursive copy + delete.</para>
/// </summary>
public static class InstallMigrator
{
    public static void Run(string installDir, string dataRoot)
    {
        Directory.CreateDirectory(dataRoot);

        TryMoveFile(Path.Combine(installDir, "config"),    Path.Combine(dataRoot, "config"));
        TryMoveFile(Path.Combine(installDir, "datastore"), Path.Combine(dataRoot, "datastore"));
        TryMoveDir (Path.Combine(installDir, "plugins"),   Path.Combine(dataRoot, "plugins"));
    }

    private static void TryMoveFile(string src, string dst)
    {
        if (!File.Exists(src)) return;
        if (File.Exists(dst)) return;

        try
        {
            var dstDir = Path.GetDirectoryName(dst);
            if (!string.IsNullOrEmpty(dstDir))
                Directory.CreateDirectory(dstDir);
            File.Move(src, dst);
            Log.Info("Migrated {0} -> {1}", src, dst);
        }
        catch (IOException)
        {
            // Cross-volume move; fall back to copy + delete.
            try
            {
                File.Copy(src, dst, overwrite: false);
                File.Delete(src);
                Log.Info("Migrated {0} -> {1} (cross-volume copy)", src, dst);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to migrate {0} (copy fallback)", src);
                TryDelete(dst);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to migrate {0}", src);
        }
    }

    private static void TryMoveDir(string src, string dst)
    {
        if (!Directory.Exists(src)) return;
        if (Directory.Exists(dst)) return;

        try
        {
            Directory.Move(src, dst);
            Log.Info("Migrated {0}\\ -> {1}\\", src, dst);
        }
        catch (IOException)
        {
            // Cross-volume; fall back to recursive copy + delete.
            try
            {
                CopyDirectory(src, dst);
                Directory.Delete(src, recursive: true);
                Log.Info("Migrated {0}\\ -> {1}\\ (cross-volume copy)", src, dst);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to migrate {0}\\ (copy fallback)", src);
                // Roll back partial destination so the next launch retries cleanly.
                if (Directory.Exists(dst))
                {
                    try { Directory.Delete(dst, recursive: true); } catch { /* best effort */ }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to migrate {0}\\", src);
        }
    }

    private static void CopyDirectory(string src, string dst)
    {
        Directory.CreateDirectory(dst);
        foreach (var file in Directory.EnumerateFiles(src))
        {
            var leaf = Path.GetFileName(file);
            File.Copy(file, Path.Combine(dst, leaf), overwrite: false);
        }
        foreach (var sub in Directory.EnumerateDirectories(src))
        {
            var leaf = Path.GetFileName(sub);
            CopyDirectory(sub, Path.Combine(dst, leaf));
        }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* best effort */ }
    }
}
