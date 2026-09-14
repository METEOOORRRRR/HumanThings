using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
namespace EarthRecovery
{
    public sealed class ArchiveStore
    {
        [Serializable] public sealed class Save { public int version = 1; public List<ArchiveEntry> entries = new(); }
        readonly string path;
        public string LastError { get; private set; }
        public ArchiveStore(string path) { this.path = path; }
        public List<ArchiveEntry> Load()
        {
            LastError = "";
            foreach (var file in new[] { path, path + ".bak" })
            {
                if (!File.Exists(file)) continue;
                try
                {
                    if (new FileInfo(file).Length > 1048576) throw new InvalidDataException("Archive too large");
                    var save = JsonUtility.FromJson<Save>(File.ReadAllText(file));
                    if (save == null || save.version != 1 || save.entries == null || save.entries.Count > 512 || save.entries.Any(e => e == null || e.artifactId == null || e.observations == null || e.recoverCount < 0)
                        || save.entries.Select(e => e.artifactId).Distinct().Count() != save.entries.Count) throw new InvalidDataException("Archive schema");
                    return save.entries;
                }
                catch (Exception e) when (e is IOException || e is InvalidDataException || e is ArgumentException || e is UnauthorizedAccessException) { LastError = e.Message; }
            }
            return new List<ArchiveEntry>();
        }
        public bool Write(List<ArchiveEntry> entries)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path + ".tmp", JsonUtility.ToJson(new Save { entries = entries }, true));
                if (File.Exists(path)) File.Replace(path + ".tmp", path, path + ".bak"); else File.Move(path + ".tmp", path);
                LastError = ""; return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
            { LastError = e.Message; return false; }
        }
    }
}
