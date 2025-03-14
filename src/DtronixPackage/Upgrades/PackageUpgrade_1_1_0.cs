using System;
using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace DtronixPackage.Upgrades;

/// <summary>
/// Changes non-backup save_log.json files to changelog.json.
///     Updates formatting of file for new Type property.
/// Deletes old file_version file if it exists.
/// </summary>
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Local")]
[SuppressMessage("ReSharper", "InconsistentNaming")]
[SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
internal class PackageUpgrade_1_1_0 : InternalPackageUpgrade
{
    internal class SaveLogEntry {
        public string Username { get; set; } = null!;
        public string ComputerName { get; set; } = null!;
        public DateTime Time { get; set; } 
        public bool? AutoSave { get; set; }

        public SaveLogEntry(string username, string computerName, DateTime time, bool? autoSave)
        {
            Username = username;
            ComputerName = computerName;
            Time = time;
            AutoSave = autoSave;
        }

        public SaveLogEntry()
        {
                
        }
    }

    internal class ChangelogEntry
    {
        public ChangelogItemType Type { get; set; }
        public string Username { get; set; } = null!;
        public string ComputerName { get; set; } = null!;
        public DateTimeOffset Time { get; set; }
        public string Note { get; set; } = null!;
    }

    internal enum ChangelogItemType
    {
        Unset = 0,
        PackageUpgrade = 1,
        ApplicationUpgrade = 2,
        Save = 3,
        AutoSave = 4
    }

    public PackageUpgrade_1_1_0() : base(PackageUpgradeVersion.V1_1_0)
    {
    }

    protected override async Task<bool> OnUpgrade(ZipArchive archive)
    {
        var archiveEntries = archive.Entries.ToArray();
        foreach (var zipArchiveEntry in archiveEntries)
        {
            if(zipArchiveEntry.Name != "save_log.json" || zipArchiveEntry.FullName.Contains("-backup-"))
                continue;

            SaveLogEntry[]? saveLogs;
            await using (var saveLogStream = zipArchiveEntry.Open())
                saveLogs = await JsonSerializer.DeserializeAsync<SaveLogEntry[]>(saveLogStream);
            
            if(saveLogs == null)
                throw new Exception("Unable to load save log file");

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
            var changelogEntry = archive.CreateEntry(basePath + "changelog.json");
            await using (var saveLogStream = changelogEntry.Open())
            {
                await JsonSerializer.SerializeAsync(saveLogStream, changelogEntries);
            }

            zipArchiveEntry.Delete();
        }

        var fileVersionFile = archiveEntries.FirstOrDefault(f => f.FullName == "file_version");
        fileVersionFile?.Delete();

        return true;
    }
}