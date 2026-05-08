using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using Pengu.Logging;

namespace Pengu.Windows.Native;

/// <summary>
/// Grants <c>Authenticated Users: Modify</c> on a directory under
/// <c>%PROGRAMDATA%</c> so all local users can read + write Pengu's
/// shared state (config, plugins, datastore).
///
/// <para>By default, when a non-admin process creates a folder under
/// <c>C:\ProgramData\</c>, the new folder inherits <c>Users: Read &amp;
/// Execute</c> from the parent ACL. The creator becomes the owner via
/// <c>CREATOR OWNER</c> and gets full control on what they create, but
/// other local users cannot modify anything inside. With Pengu running
/// in Universal mode (IFEO is HKLM, machine-wide) we want every user's
/// LCUX session to see the same plugins and the same disabled list, so
/// the data root must be writable by everyone.</para>
///
/// <para>The ACE is added with <see cref="InheritanceFlags.ContainerInherit"/>
/// + <see cref="InheritanceFlags.ObjectInherit"/> so all descendants
/// (current and future) automatically inherit it. Idempotent: subsequent
/// launches detect the existing rule and skip.</para>
///
/// <para>The folder owner can change ACLs without admin, so this works
/// from a non-elevated process — provided that process is the original
/// creator. First-launch wins; later launches just verify.</para>
/// </summary>
public static class ProgramDataAcl
{
    /// <summary>
    /// Create <paramref name="path"/> if missing and ensure
    /// <c>Authenticated Users</c> have <c>Modify</c> with inheritance.
    /// Safe to call repeatedly; subsequent calls verify and no-op if the
    /// rule is already present.
    /// </summary>
    public static void EnsureWritableByEveryone(string path)
    {
        try
        {
            var dirInfo = Directory.CreateDirectory(path);
            var security = dirInfo.GetAccessControl();
            var sid = new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null);

            // Skip if our rule is already present (or stricter than what we
            // need — caller has already locked things down). Idempotent
            // across launches; we don't want to re-grant on every startup.
            var rules = security.GetAccessRules(includeExplicit: true, includeInherited: false, typeof(SecurityIdentifier));
            foreach (FileSystemAccessRule existing in rules)
            {
                if (existing.IdentityReference is SecurityIdentifier s && s == sid
                    && existing.AccessControlType == AccessControlType.Allow
                    && existing.FileSystemRights.HasFlag(FileSystemRights.Modify)
                    && existing.InheritanceFlags.HasFlag(InheritanceFlags.ContainerInherit)
                    && existing.InheritanceFlags.HasFlag(InheritanceFlags.ObjectInherit))
                {
                    return;
                }
            }

            var rule = new FileSystemAccessRule(
                sid,
                // Modify covers Read+Write+Delete (no take-ownership / change-ACL).
                // Synchronize is needed for handle waits; .NET adds it automatically
                // for most rights, but we include it explicitly for clarity.
                FileSystemRights.Modify | FileSystemRights.Synchronize,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow);

            security.AddAccessRule(rule);
            dirInfo.SetAccessControl(security);

            Log.Info("ProgramDataAcl: granted Authenticated Users: Modify on {0}", path);
        }
        catch (UnauthorizedAccessException ex)
        {
            // Owner-mismatch can land us here when launch order goes
            // user-A-creates → user-B-launches → we try to set ACL but B
            // isn't the owner. Log loudly; activation/config will still
            // work for user A but user B will hit per-file permission
            // errors until A re-launches or an admin runs `icacls /grant`.
            Log.Warn("ProgramDataAcl: cannot set ACL on {0} (not owner?): {1}", path, ex.Message);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ProgramDataAcl.EnsureWritableByEveryone({0}) threw", path);
        }
    }
}
