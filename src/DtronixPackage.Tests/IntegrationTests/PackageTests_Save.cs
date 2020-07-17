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
            await CreateAndClosePackage(async f => await f.WriteJson(ContentFileName, SampleJson), firstVersion);

            // Open, save & close the file.
            var file = new DynamicPackage(secondVersion, this, saveMissMatch, false)
            {
                Saving = async argsPackage =>
                {
                    await argsPackage.WriteJson(ContentFileName, SampleJson);
                }
            };
            return file;
        }

        private async Task PackageVersionMissMatchInternal(bool saveMissMatch)
        {
            var file = await CreateVersionMissMatchedPackageInternal(saveMissMatch, new Version(1, 0), new Version(1, 1));
            await file.Open(PackageFilename);
            await file.Save(PackageFilename);
            file.Close();

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
            // Open, save & close the file.
            var file = new DynamicPackage(new Version(1, 0), this, false, true);
            await file.Save(PackageFilename);
            AssertFileIsReadOnly(PackageFilename + ".lock");

            file.Close();
        }

        [Test]
        public async Task IsReadOnly()
        {
            // Open, save & close the file.
            var file = new DynamicPackage(new Version(1,0), this, false, false);

            await file.Save(PackageFilename);
            AssertFileIsReadOnly(PackageFilename);

            file.Close();
        }

        [Test]
        public async Task SetsIsDataModifiedToFalse()
        {
            var file = new DynamicPackageData(new Version(1,0), this);

            file.Data.Children.Add(new PackageDataContractChild());
            await file.Save(PackageFilename);
            Assert.IsFalse(file.IsDataModified);
        }
        
    }
}