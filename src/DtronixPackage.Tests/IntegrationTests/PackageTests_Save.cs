using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using NLog;
using NLog.Config;
using NLog.Targets;
using NUnit.Framework;

namespace DtronixPackage.Tests.IntegrationTests
{
    public class PackageTests_Save : IntegrationTestBase
    {
        private async Task<PackageDynamicFile> CreateVersionMissMatchedFileInternal(
            bool saveMissMatch,
            Version firstVersion,
            Version secondVersion)
        {
            await CreateAndCloseFile(async f => await f.WriteJson(ContentFileName, SampleJson), firstVersion);

            // Open, save & close the file.
            var file = new PackageDynamicFile(secondVersion, this, saveMissMatch, false);
            file.Saving = async argsFile =>
            {
                await file.WriteJson(ContentFileName, SampleJson);
            };
            return file;
        }

        private async Task FileVersionMissMatchInternal(bool saveMissMatch)
        {
            var file = await CreateVersionMissMatchedFileInternal(saveMissMatch, new Version(1, 0), new Version(1, 1));
            await file.Open(ZipFilename);
            await file.Save(ZipFilename);
            file.Close();

            await using var fileStream = new FileStream(ZipFilename, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
            using var archive = new ZipArchive(fileStream);

            Assert.AreEqual(saveMissMatch ? 2 : 1, archive.Entries.Count(e => e.FullName.EndsWith(ContentFileName)));
        }

        [Test]
        public async Task SaveFile_SavesVersionMissMatch()
        {
            await FileVersionMissMatchInternal(true);
        }

        [Test]
        public async Task SaveFile_DoesNotSaveVersionMissMatch()
        {
            await FileVersionMissMatchInternal(false);
        }

        [Test]
        public async Task SaveFile_DoesNotCreateLockFile()
        {
            // Open, save & close the file.
            var file = new PackageDynamicFile(new Version(1,0), this, false, false);
            await file.Save(ZipFilename);
            await AssertFileDoesNotExistWithin(file.SavePath + ".lock");
            file.Close();
        }

        [Test]
        public async Task LockFile_IsReadOnly()
        {
            // Open, save & close the file.
            var file = new PackageDynamicFile(new Version(1, 0), this, false, true);
            await file.Save(ZipFilename);
            AssertFileIsReadOnly(ZipFilename + ".lock");

            file.Close();
        }

        [Test]
        public async Task SaveFile_IsReadOnly()
        {
            // Open, save & close the file.
            var file = new PackageDynamicFile(new Version(1,0), this, false, false);

            await file.Save(ZipFilename);
            AssertFileIsReadOnly(ZipFilename);

            file.Close();
        }

        [Test]
        public async Task Save_SetsIsDataModifiedToFalse()
        {
            var file = new PackageDataFile(new Version(1,0), this);

            file.Data.Children.Add(new PackageDataContractChild());
            await file.Save(ZipFilename);
            Assert.IsFalse(file.IsDataModified);
        }
        
    }
}