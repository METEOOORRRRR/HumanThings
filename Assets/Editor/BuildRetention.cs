using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace EarthRecovery.Editor
{
    public static class BuildRetention
    {
        public const string LatestName = "Latest";

        public static void Publish(string buildsRoot, string staging)
        {
            buildsRoot = Path.GetFullPath(buildsRoot);
            staging = Path.GetFullPath(staging);
            VerifyTree(buildsRoot, staging);
            if (!File.Exists(Path.Combine(staging, "EarthRecovery.exe"))) throw new IOException("Missing built executable");
            var latest = Path.Combine(buildsRoot, LatestName);
            var archive = Path.Combine(buildsRoot, "Archive");
            if (Directory.Exists(latest)) VerifyTree(buildsRoot, latest);
            if (Directory.Exists(archive)) VerifyTree(buildsRoot, archive);
            Directory.CreateDirectory(archive);
            string backup = null;
            if (Directory.Exists(latest))
            {
                backup = Path.Combine(archive, "Build-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff"));
                Directory.Move(latest, backup);
            }
            try { Directory.Move(staging, latest); }
            catch
            {
                if (backup != null && !Directory.Exists(latest)) Directory.Move(backup, latest);
                throw;
            }
            Prune(buildsRoot);
        }

        public static void Prune(string buildsRoot)
        {
            buildsRoot = Path.GetFullPath(buildsRoot);
            var latest = Path.Combine(buildsRoot, LatestName);
            if (!File.Exists(Path.Combine(latest, "EarthRecovery.exe"))) throw new IOException("Latest build missing; cleanup refused");
            var archive = Path.Combine(buildsRoot, "Archive");
            var candidates = Directory.GetDirectories(buildsRoot).Where(p => p != latest && Path.GetFileName(p) != "Archive")
                .Concat(Directory.Exists(archive) ? Directory.GetDirectories(archive) : Array.Empty<string>())
                .Where(p => File.Exists(Path.Combine(p, "EarthRecovery.exe")))
                .OrderByDescending(BuildTime).ThenBy(p => p, StringComparer.Ordinal).ToArray();
            foreach (var old in candidates.Skip(2)) VerifyTree(buildsRoot, old);
            foreach (var old in candidates.Skip(2)) Directory.Delete(old, true);
            Debug.Log("BUILD_RETENTION_PASS: latest + " + Math.Min(2, candidates.Length) + " previous builds");
        }

        static DateTime BuildTime(string path)
        {
            var dll = Path.Combine(path, "EarthRecovery_Data", "Managed", "EarthRecovery.dll");
            return File.GetLastWriteTimeUtc(File.Exists(dll) ? dll : Path.Combine(path, "EarthRecovery.exe"));
        }

        // Reject junctions/symlinks and paths outside the named build root before any recursive deletion or move.
        static void VerifyTree(string root, string path)
        {
            root = Path.GetFullPath(root);
            path = Path.GetFullPath(path);
            if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Build path escapes its root: " + path);
            for (var parent = new DirectoryInfo(path); parent != null; parent = parent.Parent)
                if ((parent.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Linked build path refused: " + parent.FullName);
            foreach (var entry in Directory.GetFileSystemEntries(path))
            {
                var attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Linked build entry refused: " + entry);
                if ((attributes & FileAttributes.Directory) != 0) VerifyTree(root, entry);
            }
        }

        public static void SelfTest()
        {
            var root = Path.GetFullPath(Path.Combine("Temp", "BuildRetention-" + Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(root);
            try
            {
                for (int i = 0; i < 5; i++)
                {
                    var staging = Path.Combine(root, ".staging"); Directory.CreateDirectory(staging);
                    File.WriteAllText(Path.Combine(staging, "EarthRecovery.exe"), i.ToString());
                    File.SetLastWriteTimeUtc(Path.Combine(staging, "EarthRecovery.exe"), DateTime.UtcNow.AddMinutes(i));
                    Publish(root, staging);
                    int count = Directory.GetFiles(root, "EarthRecovery.exe", SearchOption.AllDirectories).Length;
                    if (count != Math.Min(i + 1, 3)) throw new Exception("Incorrect retained build count");
                    if (File.ReadAllText(Path.Combine(root, LatestName, "EarthRecovery.exe")) != i.ToString()) throw new Exception("Latest build replaced incorrectly");
                }
                var versions = Directory.GetFiles(root, "EarthRecovery.exe", SearchOption.AllDirectories).Select(File.ReadAllText).OrderBy(x => x).ToArray();
                if (string.Join(",", versions) != "2,3,4") throw new Exception("Older versions retained");
                var incomplete = Path.Combine(root, ".staging"); Directory.CreateDirectory(incomplete);
                bool rejected = false;
                try { Publish(root, incomplete); } catch (IOException) { rejected = true; }
                if (!rejected || File.ReadAllText(Path.Combine(root, LatestName, "EarthRecovery.exe")) != "4") throw new Exception("Incomplete build displaced latest");
                Debug.Log("BUILD_RETENTION_SELF_TEST_PASS");
            }
            finally { VerifyTree(Path.GetDirectoryName(root), root); Directory.Delete(root, true); }
        }
    }
}
