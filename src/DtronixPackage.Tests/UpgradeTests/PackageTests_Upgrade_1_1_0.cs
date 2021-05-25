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
            Assert.IsTrue(await UpgradeClass.Upgrade(Package));
        }

        [Test]
        public async Task UpgradeSucceedsWithMissingSaveLog()
        {
            Package = await CreatePackageLessSaveLog();
            UpgradeClass = new PackageUpgrade_1_1_0();
            Assert.IsTrue(await UpgradeClass.Upgrade(Package));
        }

        [Test]
        public void UpgradeIgnoresBackups()
        {
            Assert.IsTrue(Package.Entries.Any(e => e.FullName == "UpgradeTest-backup-0.1/save_log.json"));
            Assert.IsFalse(Package.Entries.Any(e => e.FullName == "UpgradeTest-backup-0.1/changelog.json"));
        }

        [Test]
        public async Task ChangelogCreatedAndUpgradedFromSaveLog()
        {
            var changelogJson = Package.Entries.First(e => e.FullName == "UpgradeTest/changelog.json");
            await using var stream = changelogJson.Open();
            var changelogEntries = await JsonSerializer.DeserializeAsync<PackageUpgrade_1_1_0.ChangelogEntry[]>(stream);

            Assert.AreEqual("TestUser1", changelogEntries[0].Username);
            Assert.AreEqual("GeneralComputer1", changelogEntries[0].ComputerName);
            Assert.AreEqual(PackageUpgrade_1_1_0.ChangelogItemType.Save, changelogEntries[0].Type);

            Assert.AreEqual("TestUser2", changelogEntries[1].Username);
            Assert.AreEqual("GeneralComputer2", changelogEntries[1].ComputerName);
            Assert.AreEqual(PackageUpgrade_1_1_0.ChangelogItemType.Save, changelogEntries[1].Type);

            Assert.AreEqual("TestUser3", changelogEntries[2].Username);
            Assert.AreEqual("GeneralComputer3", changelogEntries[2].ComputerName);
            Assert.AreEqual(PackageUpgrade_1_1_0.ChangelogItemType.AutoSave, changelogEntries[2].Type);
        }

        [Test]
        public void DeletesSaveLog()
        {
            Assert.IsFalse(Package.Entries.Any(e => e.FullName == "UpgradeTest/save_log.json"));
        }

        [Test]
        public void DeletesFileVersion()
        {
            Assert.IsFalse(Package.Entries.Any(e => e.FullName == "file_version"));
        }
    }
}