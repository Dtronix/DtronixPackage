using System;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace DtronixPackage.Upgrades
{
    internal class PackageUpgrade_1_1_0<T> : PackageUpgrade<T>
        where T : PackageContent, new()
    {
        private class SaveLogEntry {
            public string Username { get; set; } 
            public string ComputerName { get; set; } 
            public DateTime Time { get; set; } 
            public bool? AutoSave { get; set; }
        }

        public PackageUpgrade_1_1_0() : base(new Version(1, 1, 0))
        {
        }

        protected override async Task<bool> OnUpgrade(ZipArchive archive)
        {
            var archiveEntries = archive.Entries.ToArray();
            foreach (var zipArchiveEntry in archiveEntries)
            {
                if(zipArchiveEntry.Name != "save_log.json" || zipArchiveEntry.Name.Contains("-backup-"))
                    continue;

                SaveLogEntry[] saveLogs;
                await using (var saveLogStream = zipArchiveEntry.Open())
                    saveLogs = await JsonSerializer.DeserializeAsync<SaveLogEntry[]>(saveLogStream);

                var changelogEntries = new ChangelogEntry[saveLogs.Length];
                for (var i = 0; i < saveLogs.Length; i++)
                {
                    changelogEntries[i] = new ChangelogEntry
                    {
                        Username = saveLogs[i].Username,
                        ComputerName = saveLogs[i].ComputerName,
                        Time = saveLogs[i].Time,
                        Type = saveLogs[i].AutoSave == true ? ChangelogItemType.AutoSave : ChangelogItemType.Save
                    };
                }

                var basePath = zipArchiveEntry.FullName.Replace(zipArchiveEntry.Name, "");
                var changelogEntry = archive.CreateEntry(basePath + "/changelog.json");
                await using (var saveLogStream = changelogEntry.Open())
                {
                    await JsonSerializer.SerializeAsync(saveLogStream, changelogEntries);
                }

                zipArchiveEntry.Delete();
            }

            return true;
        }
    }
}
