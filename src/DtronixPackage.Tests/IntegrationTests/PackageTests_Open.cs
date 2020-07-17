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
    public class PackageTests_Open : IntegrationTestBase
    {
        [Test]
        public async Task FailsOnNewerVersionOfFile()
        {
            await CreateAndCloseFile(f => Task.CompletedTask, new Version(2, 0));
            var file = new PackageDataFile(new Version(1,0), this);
            Assert.AreEqual(PackageOpenResultType.IncompatibleVersion, (await file.Open(ZipFilename)).OpenFileOpenResultType);
        }

        [Test]
        public async Task FailsOnEmptyFile()
        {
            File.Create(ZipFilename).Close();

            // Open, save & close the file.
            var file = new PackageDynamicFile(new Version(1,0), this, false, false);
            Assert.AreEqual(PackageOpenResultType.Corrupted, (await file.Open(ZipFilename)).OpenFileOpenResultType);
        }

        [Test]
        public async Task FailsOnLockedFile()
        {
            var fileStream = File.Create(ZipFilename);

            // Open, save & close the file.
            var file = new PackageDynamicFile(new Version(1,0), this, false, false);
            Assert.AreEqual(PackageOpenResultType.Locked, (await file.Open(ZipFilename)).OpenFileOpenResultType);

            fileStream.Close();
            File.Delete(ZipFilename);
        }

        [Test]
        public async Task FailsOnLockedLockFile()
        {
            await CreateAndCloseFile(f => f.WriteString(ContentFileName, SampleText));

            var fileStream = File.Create(ZipFilename + ".lock");

            // Open, save & close the file.
            var file = new PackageDynamicFile(new Version(1,0), this, false, true);
            Assert.AreEqual(PackageOpenResultType.Locked, (await file.Open(ZipFilename)).OpenFileOpenResultType);

            fileStream.Close();
        }

        [Test]
        public async Task FileAlreadyOpenFails()
        {
            // Open, save & close the file.
            var file = new PackageDynamicFile(new Version(1,0), this, false, true);
            file.Saving = async fileArg => await file.WriteString(ContentFileName, SampleText);
            await file.Save(ZipFilename);

            var fileOpen = new PackageDynamicFile(new Version(1,0), this, false, false);

            Assert.AreEqual(PackageOpenResultType.Locked, (await fileOpen.Open(ZipFilename)).OpenFileOpenResultType);
        }

        [Test]
        public async Task FileAlreadyOpenSetsLockFileResult()
        {
            // Open, save & close the file.
            var file = new PackageDynamicFile(new Version(1,0), this, false, true);
            file.Saving = async fileArg => await file.WriteString(ContentFileName, SampleText);
            await file.Save(ZipFilename);

            var fileOpen = new PackageDynamicFile(new Version(1,0), this, false, true);

            var result = await fileOpen.Open(ZipFilename);
            Assert.AreEqual(PackageOpenResultType.Locked, result.OpenFileOpenResultType);

            Assert.IsNotNull(result.LockInfo);

            file.Close();

            result = await fileOpen.Open(ZipFilename);
            Assert.IsTrue(result.IsSuccessful);

            Assert.IsNull(result.LockInfo);
        }

        [Test]
        public async Task ReturnsSuccess()
        {
            await CreateAndCloseFile(f => f.WriteString(ContentFileName, SampleText));

            var file = new PackageDynamicFile(new Version(1,0), this, false, false);
            Assert.AreEqual(PackageOpenResult.Success, await file.Open(ZipFilename));
        }

        [Test]
        public async Task FailsOnNonExistingFile()
        {
            var file = new PackageDynamicFile(new Version(1,0), this, false, false);
            Assert.AreEqual(PackageOpenResultType.FileNotFound, (await file.Open(ZipFilename)).OpenFileOpenResultType);
        }

        [Test]
        public async Task DoesNotOpenExclusiveLock()
        {
            await CreateAndCloseFile(file => file.WriteString(ContentFileName, SampleText));
            await using (File.OpenRead(ZipFilename))
            using(var file = new PackageDataFile(new Version(1,0), this))
            {
                var result = await file.Open(ZipFilename);
                Assert.IsFalse(result.IsSuccessful);
            }

            await using (new FileStream(ZipFilename, FileMode.Open, FileAccess.Read, FileShare.Read))
            using(var file = new PackageDataFile(new Version(1,0), this))
            {
                var result = await file.Open(ZipFilename);
                Assert.IsFalse(result.IsSuccessful);
            }

            await using (new FileStream(ZipFilename, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
            using(var file = new PackageDataFile(new Version(1,0), this))
            {
                var result = await file.Open(ZipFilename);
                Assert.IsFalse(result.IsSuccessful);
            }

            await using (new FileStream(ZipFilename, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            using(var file = new PackageDataFile(new Version(1,0), this))
            {
                var result = await file.Open(ZipFilename);
                Assert.IsFalse(result.IsSuccessful);
            }

        }

        [Test]
        public async Task OpensNonExclusiveLock()
        {
            await CreateAndCloseFile(file => file.WriteString(ContentFileName, SampleText));
            
            await using (new FileStream(ZipFilename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using(var file = new PackageDataFile(new Version(1,0), this))
            {
                var result = await file.Open(ZipFilename);
                Assert.IsTrue(result.IsSuccessful);
            }
            
            await using (new FileStream(ZipFilename, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
            using(var file = new PackageDataFile(new Version(1,0), this))
            {
                var result = await file.Open(ZipFilename, true);
                Assert.IsTrue(result.IsSuccessful);
            }

            await using (File.OpenWrite(ZipFilename))
            using(var file = new PackageDataFile(new Version(1,0), this))
            {
                var result = await file.Open(ZipFilename);
                Assert.AreEqual(PackageOpenResultType.Locked, result.OpenFileOpenResultType);
            }

            await using (new FileStream(ZipFilename, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            using(var file = new PackageDataFile(new Version(1,0), this))
            {
                var result = await file.Open(ZipFilename);
                Assert.AreEqual(PackageOpenResultType.Locked, result.OpenFileOpenResultType);
            }
        }

        [Test]
        public async Task ReadOnly_DoesNotOpenExclusiveLock()
        {
            await CreateAndCloseFile(file => file.WriteString(ContentFileName, SampleText));

            await using (File.OpenWrite(ZipFilename))
            using(var file = new PackageDataFile(new Version(1,0), this))
            {
                var result = await file.Open(ZipFilename, true);
                Assert.IsFalse(result.IsSuccessful);
            }

            await using (new FileStream(ZipFilename, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            using(var file = new PackageDataFile(new Version(1,0), this))
            {
                var result = await file.Open(ZipFilename, true);
                Assert.IsFalse(result.IsSuccessful);
            }
        }

        [Test]
        public async Task ReadOnly_OpensNonExclusiveLock()
        {
            await CreateAndCloseFile(file => file.WriteString(ContentFileName, SampleText));
            await using (File.OpenRead(ZipFilename))
            using(var file = new PackageDataFile(new Version(1,0), this))
            {
                var result = await file.Open(ZipFilename, true);
                Assert.IsTrue(result.IsSuccessful);
            }

            await using (new FileStream(ZipFilename, FileMode.Open, FileAccess.Read, FileShare.Read))
            using(var file = new PackageDataFile(new Version(1,0), this))
            {
                var result = await file.Open(ZipFilename, true);
                Assert.IsTrue(result.IsSuccessful);
            }

            await using (new FileStream(ZipFilename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using(var file = new PackageDataFile(new Version(1,0), this))
            {
                var result = await file.Open(ZipFilename, true);
                Assert.IsTrue(result.IsSuccessful);
            }

            await using (new FileStream(ZipFilename, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
            using(var file = new PackageDataFile(new Version(1,0), this))
            {
                var result = await file.Open(ZipFilename, true);
                Assert.IsTrue(result.IsSuccessful);
            }

            await using (new FileStream(ZipFilename, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
            using(var file = new PackageDataFile(new Version(1,0), this))
            {
                var result = await file.Open(ZipFilename, true);
                Assert.IsTrue(result.IsSuccessful);
            }
        }
    }
}