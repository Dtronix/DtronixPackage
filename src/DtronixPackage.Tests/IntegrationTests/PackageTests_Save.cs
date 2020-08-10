using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;

namespace DtronixPackage.Tests.IntegrationTests
{
    public class PackageTests_Save : IntegrationTestBase
    {
        private async Task<DynamicPackage> CreateVersionMissMatchedPackageInternal(
            bool saveMissMatch,
            Version firstVersion,
            Version secondVersion)
        {
            await CreateAndClosePackage(async (writer, package) => await writer.WriteJson(ContentFileName, SampleJson), firstVersion);

            // Open, save & close the package.
            var package = new DynamicPackage(secondVersion, this, saveMissMatch, false)
            {
                Saving = async (writer, package) =>
                {
                    await writer.WriteJson(ContentFileName, SampleJson);
                }
            };
            return package;
        }

        private async Task PackageVersionMissMatchInternal(bool saveMissMatch)
        {
            var package = await CreateVersionMissMatchedPackageInternal(saveMissMatch, new Version(1, 0), new Version(1, 1));
            await package.Open(PackageFilename);
            await package.Save(PackageFilename);
            package.Close();

            await using var fileStream = new FileStream(PackageFilename, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
            using var archive = new ZipArchive(fileStream);

            Assert.AreEqual(saveMissMatch ? 2 : 1, archive.Entries.Count(e => e.FullName.EndsWith(ContentFileName)));
        }

        [Test]
        public async Task SavesVersionMissMatch()
        {
            await PackageVersionMissMatchInternal(true);
        }

        [Test]
        public async Task DoesNotSaveVersionMissMatch()
        {
            await PackageVersionMissMatchInternal(false);
        }

        [Test]
        public async Task LockFile_IsReadOnly()
        {
            // Open, save & close the package.
            var package = new DynamicPackage(new Version(1, 0), this, false, true);
            await package.Save(PackageFilename);
            Utilities.AssertFileIsReadOnly(PackageFilename + ".lock");

            package.Close();
        }

        [Test]
        public async Task IsReadOnly()
        {
            // Open, save & close the package.
            var package = new DynamicPackage(new Version(1,0), this, false, false);

            await package.Save(PackageFilename);
            Utilities.AssertFileIsReadOnly(PackageFilename);

            package.Close();
        }

        [Test]
        public async Task SetsIsDataModifiedToFalse()
        {
            var package = new DynamicPackageData(new Version(1,0), this);

            package.Data.Children.Add(new PackageDataContractChild());
            await package.Save(PackageFilename);
            Assert.IsFalse(package.IsContentModified);
        }

        [Test]
        public async Task ChangesSavePath()
        {
            var package = new DynamicPackageData(new Version(1,0), this);
            package.Data.Children.Add(new PackageDataContractChild());
            await package.Save(PackageFilename);

            await Utilities.AssertFileExistWithin(PackageFilename);
            var secondPath = Path.Combine("saves/", Guid.NewGuid() + ".file");
            await package.Save(secondPath);
            await Utilities.AssertFileExistWithin(secondPath);
        }

        [Test]
        public async Task ChangeSavePathWritesContentToPackage()
        {
            var package = new DynamicPackageData(new Version(1,0), this);
            package.Data.Children.Add(new PackageDataContractChild());
            await package.Save(PackageFilename);
            var secondPath = Path.Combine("saves/", Guid.NewGuid() + ".file");
            await package.Save(secondPath);
            Assert.AreNotEqual(new FileInfo(secondPath).Length, 0, "File length was zero.");
        }
    }
}