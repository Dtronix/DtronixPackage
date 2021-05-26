using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;

namespace DtronixPackage.Tests.IntegrationTests
{
    public class PackageTests_Upgrade : IntegrationTestBase
    {
        [Test]
        public async Task UpgradeIgnoresPastVersions()
        {
            await CreateAndClosePackage(async (writer, package) => await writer.Write(ContentFileName, SampleText));
            var file = new DynamicPackage(new Version(1, 1), this, true, false);
            bool upgradeRan = false;
            file.UpgradeOverrides.Add(new ApplicationPackageUpgradeCallback(new Version(1, 2), new Version(1, 0), args =>
            {
                upgradeRan = true;
                return Task.FromResult(true);
            }));

            await file.Open(PackageFilename);

            Assert.IsFalse(upgradeRan);
        }

        [Test]
        public async Task UpgradeIgnoresFutureUpgrades()
        {
            await CreateAndClosePackage(async (writer, package) => await writer.Write(ContentFileName, SampleText));
            var file = new DynamicPackage(new Version(1, 0), this, true, false);
            bool upgradeRan = false;
            file.UpgradeOverrides.Add(new ApplicationPackageUpgradeCallback(new Version(1,2),new Version(1, 1), args =>
            {
                upgradeRan = true;
                return Task.FromResult(true);
            }));

            await file.Open(PackageFilename);

            Assert.IsFalse(upgradeRan);
        }

        [Test]
        public async Task UpgradeUpgradesOldFile()
        {
            await CreateAndClosePackage(async (writer, package) => await writer.Write(ContentFileName, SampleText));
            var file = new DynamicPackage(new Version(1, 1), this, true, false);
            bool upgradeRan = false;
            file.UpgradeOverrides.Add(new ApplicationPackageUpgradeCallback(new Version(1, 2),new Version(1, 1), args =>
            {
                upgradeRan = true;
                return Task.FromResult(true);
            }));

            await file.Open(PackageFilename);

            Assert.IsTrue(upgradeRan);
        }     
        
        [Test]
        public async Task UpgradeOpenFailsOnFalseUpgradeReturn()
        {
            await CreateAndClosePackage(async (writer, package) => await writer.Write(ContentFileName, SampleText));
            var file = new DynamicPackage(new Version(1, 1), this, true, false);
            file.UpgradeOverrides.Add(new ApplicationPackageUpgradeCallback(new Version(1, 2), new Version(1, 1), args => Task.FromResult(false)));
            var openResult = await file.Open(PackageFilename);

            Assert.AreEqual(PackageOpenResultType.UpgradeFailure, openResult.Result);
        }

        [Test]
        public async Task UpgradeOpenSucceedsOnTrueUpgradeReturn()
        {
            await CreateAndClosePackage(async (writer, package) => await writer.Write(ContentFileName, SampleText));
            var file = new DynamicPackage(new Version(1, 1), this, true, false);
            file.UpgradeOverrides.Add(new ApplicationPackageUpgradeCallback(new Version(1, 2), new Version(1, 1), args => Task.FromResult(true)));
            var openResult = await file.Open(PackageFilename);

            Assert.AreEqual(PackageOpenResultType.Success, openResult.Result);
        }

        [Test]
        public async Task UpgradeBacksUpInitialFiles()
        {
            await CreateAndClosePackage(async (writer, package) => await writer.Write(ContentFileName, SampleText));
            var file = new DynamicPackage(new Version(1, 1), this, true, false);
            file.UpgradeOverrides.Add(new ApplicationPackageUpgradeCallback(new Version(1, 2), new Version(1, 1), args =>
            {
                if(args.Archive.Entries.All(en => en.FullName != "DtronixPackage.Tests/" + ContentFileName))
                    return Task.FromResult(false);

                if(args.Archive.Entries.All(en => en.FullName != "DtronixPackage.Tests-backup-1.0/DtronixPackage.Tests/" + ContentFileName))
                    return Task.FromResult(false);

                return Task.FromResult(true);
            }));
            var openResult = await file.Open(PackageFilename);

            Assert.AreEqual(PackageOpenResultType.Success, openResult.Result);
        }

        [Test]
        public async Task UpgradeIgnoresOtherFiles()
        {
            await CreateAndClosePackage(async (writer, package) => await writer.Write(ContentFileName, SampleText));

            await using(var fs = File.Open(PackageFilename, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
            using (var archive = new ZipArchive(fs, ZipArchiveMode.Update))
            {
                var entry = archive.CreateEntry("test.text");
                await using (var entryStream = entry.Open())
                await using(var sw = new StreamWriter(entryStream))
                {
                    await sw.WriteAsync(SampleText);
                }
            }

            var file = new DynamicPackage(new Version(1, 1), this, true, false);
            file.UpgradeOverrides.Add(new ApplicationPackageUpgradeCallback(new Version(1, 2), new Version(1, 1), args =>
            {
                return Task.FromResult(args.Archive.Entries.Any(en => en.FullName == "test.text"));
            }));
            var openResult = await file.Open(PackageFilename);

            Assert.AreEqual(PackageOpenResultType.Success, openResult.Result);
        }

        [Test]
        public async Task UpgradeModifiesFilesBeforeOpen()
        {
            await CreateAndClosePackage(async (writer, package) => await writer.Write(ContentFileName, SampleText));

            var file = new DynamicPackage(new Version(1, 1), this, true, false);
            file.UpgradeOverrides.Add(new ApplicationPackageUpgradeCallback(new Version(1, 2), new Version(1, 1), args =>
            {
                var entry = args.Archive.Entries.FirstOrDefault(f => f.Name == ContentFileName);

                using (var stream = entry!.Open())
                using(var sw = new StreamWriter(stream))
                {
                    // Set the stream to the end for writing.
                    stream.Position = stream.Length;
                    sw.WriteAsync(SampleText);
                }
                return Task.FromResult(true);
            }));

            file.Reading += async (reader, package) =>
            {
                var fileContents = await reader.ReadString(ContentFileName);
                Assert.AreEqual(SampleText + SampleText, fileContents);
                return true;
            };

            var openResult = await file.Open(PackageFilename);

            Assert.AreEqual(PackageOpenResultType.Success, openResult.Result);
        }

        [Test]
        public async Task UpgradeDeletesFilesBeforeOpen()
        {
            await CreateAndClosePackage(async (writer, package) => await writer.Write(ContentFileName, SampleText));

            var file = new DynamicPackage(new Version(1, 1), this, true, false);
            file.UpgradeOverrides.Add(new ApplicationPackageUpgradeCallback(new Version(1, 2), new Version(1, 1), args =>
            {
                var entry = args.Archive.Entries.FirstOrDefault(f => f.Name == ContentFileName);
                entry?.Delete();
                return Task.FromResult(true);
            }));

            file.Reading += (reader, package) =>
            {
                Assert.IsFalse(reader.FileExists(ContentFileName));
                return Task.FromResult(true);
            };

            var openResult = await file.Open(PackageFilename);

            Assert.AreEqual(PackageOpenResultType.Success, openResult.Result);
        }
    }
}