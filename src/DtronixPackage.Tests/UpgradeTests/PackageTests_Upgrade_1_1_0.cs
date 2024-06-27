using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using DtronixPackage.Upgrades;
using NUnit.Framework;

namespace DtronixPackage.Tests.UpgradeTests
{
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Local")]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    internal class PackageTests_Upgrade_1_1_0 : UpgradeTestBase<PackageUpgrade_1_1_0>
    {
        
        protected override async Task<ZipArchive> CreatePackage()
        {
            return await PackageBuilder.CreateZipArchive(new (string Path, object Data)[]
            {
                ("file_version", "0.1.0"),
                ("UpgradeTest/save_log.json", new[]
                {
                    new PackageUpgrade_1_1_0.SaveLogEntry("TestUser1", "GeneralComputer1", DateTime.Now, null),
                    new PackageUpgrade_1_1_0.SaveLogEntry("TestUser2", "GeneralComputer2", DateTime.Now.AddDays(1), false),
                    new PackageUpgrade_1_1_0.SaveLogEntry("TestUser3", "GeneralComputer3", DateTime.Now.AddDays(2), true)
                }),
                ("UpgradeTest-backup-0.1/save_log.json", new[]
                {
                    new PackageUpgrade_1_1_0.SaveLogEntry("TestUser1", "GeneralComputer1", DateTime.Now, null),
                })
            });
        }

        private async Task<ZipArchive> CreatePackageLessSaveLog()
        {
            return await PackageBuilder.CreateZipArchive(new (string Path, object Data)[]
            {
                ("file_version", "0.1.0")
            });
        }

        [Test]
        public async Task UpgradeSucceeds()
        {
            Package = await CreatePackage();
            UpgradeClass = new PackageUpgrade_1_1_0();
            Assert.That(await UpgradeClass.Upgrade(Package), Is.True);
        }

        [Test]
        public async Task UpgradeSucceedsWithMissingSaveLog()
        {
            Package = await CreatePackageLessSaveLog();
            UpgradeClass = new PackageUpgrade_1_1_0();
            Assert.That(await UpgradeClass.Upgrade(Package), Is.True);
        }

        [Test]
        public void UpgradeIgnoresBackups()
        {
            Assert.That(Package.Entries.Any(e => e.FullName == "UpgradeTest-backup-0.1/save_log.json"), Is.True);
            Assert.That(Package.Entries.Any(e => e.FullName == "UpgradeTest-backup-0.1/changelog.json"), Is.False);
        }

        [Test]
        public async Task ChangelogCreatedAndUpgradedFromSaveLog()
        {
            var changelogJson = Package.Entries.First(e => e.FullName == "UpgradeTest/changelog.json");
            await using var stream = changelogJson.Open();
            var changelogEntries = await JsonSerializer.DeserializeAsync<PackageUpgrade_1_1_0.ChangelogEntry[]>(stream);

            Assert.That(changelogEntries[0].Username, Is.EqualTo("TestUser1"));
            Assert.That(changelogEntries[0].ComputerName, Is.EqualTo("GeneralComputer1"));
            Assert.That(changelogEntries[0].Type, Is.EqualTo(PackageUpgrade_1_1_0.ChangelogItemType.Save));

            Assert.That(changelogEntries[1].Username, Is.EqualTo("TestUser2"));
            Assert.That(changelogEntries[1].ComputerName, Is.EqualTo("GeneralComputer2"));
            Assert.That(changelogEntries[1].Type, Is.EqualTo(PackageUpgrade_1_1_0.ChangelogItemType.Save));

            Assert.That(changelogEntries[2].Username, Is.EqualTo("TestUser3"));
            Assert.That(changelogEntries[2].ComputerName, Is.EqualTo("GeneralComputer3"));
            Assert.That(changelogEntries[2].Type, Is.EqualTo(PackageUpgrade_1_1_0.ChangelogItemType.AutoSave));
        }

        [Test]
        public void DeletesSaveLog()
        {
            Assert.That(Package.Entries.Any(e => e.FullName == "UpgradeTest/save_log.json"), Is.False);
        }

        [Test]
        public void DeletesFileVersion()
        {
            Assert.That(Package.Entries.Any(e => e.FullName == "file_version"), Is.False);
        }
    }
}