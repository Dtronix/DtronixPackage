using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;

namespace DtronixPackage.Tests.IntegrationTests
{
    public class PackageTests_Open : IntegrationTestBase
    {
        [Test]
        public async Task FailsOnNewerVersionOfPackage()
        {
            await CreateAndClosePackage(f => Task.CompletedTask, new Version(2, 0));
            var file = new DynamicPackageData(new Version(1,0), this);
            Assert.AreEqual(PackageOpenResultType.IncompatibleVersion, (await file.Open(PackageFilename)).Result);
        }

        [Test]
        public async Task FailsOnEmptyPackage()
        {
            File.Create(PackageFilename).Close();

            // Open, save & close the file.
            var file = new DynamicPackage(new Version(1,0), this, false, false);
            Assert.AreEqual(PackageOpenResultType.Corrupted, (await file.Open(PackageFilename)).Result);
        }

        [Test]
        public async Task FailsOnLockedPackage()
        {
            var fileStream = File.Create(PackageFilename);

            // Open, save & close the file.
            var file = new DynamicPackage(new Version(1,0), this, false, false);
            Assert.AreEqual(PackageOpenResultType.Locked, (await file.Open(PackageFilename)).Result);

            fileStream.Close();
            File.Delete(PackageFilename);
        }

        [Test]
        public async Task FailsOnLockedLockPackage()
        {
            await CreateAndClosePackage(f => f.WriteString(ContentFileName, SampleText));

            var fileStream = File.Create(PackageFilename + ".lock");

            // Open, save & close the file.
            var file = new DynamicPackage(new Version(1,0), this, false, true);
            Assert.AreEqual(PackageOpenResultType.Locked, (await file.Open(PackageFilename)).Result);

            fileStream.Close();
        }

        [Test]
        public async Task PackageAlreadyOpenFails()
        {
            // Open, save & close the file.
            var file = new DynamicPackage(new Version(1,0), this, false, true);
            file.Saving = async fileArg => await file.WriteString(ContentFileName, SampleText);
            await file.Save(PackageFilename);

            var fileOpen = new DynamicPackage(new Version(1,0), this, false, false);

            Assert.AreEqual(PackageOpenResultType.Locked, (await fileOpen.Open(PackageFilename)).Result);
        }

        [Test]
        public async Task PackageAlreadyOpenSetsLockedPackageResult()
        {
            // Open, save & close the file.
            var file = new DynamicPackage(new Version(1,0), this, false, true);
            file.Saving = async fileArg => await file.WriteString(ContentFileName, SampleText);
            await file.Save(PackageFilename);

            var fileOpen = new DynamicPackage(new Version(1,0), this, false, true);

            var result = await fileOpen.Open(PackageFilename);
            Assert.AreEqual(PackageOpenResultType.Locked, result.Result);

            Assert.IsNotNull(result.LockInfo);

            file.Close();

            result = await fileOpen.Open(PackageFilename);
            Assert.IsTrue(result.IsSuccessful);

            Assert.IsNull(result.LockInfo);
        }

        [Test]
        public async Task ReturnsSuccess()
        {
            await CreateAndClosePackage(f => f.WriteString(ContentFileName, SampleText));

            var file = new DynamicPackage(new Version(1,0), this, false, false);
            Assert.AreEqual(PackageOpenResult.Success, await file.Open(PackageFilename));
        }

        [Test]
        public async Task FailsOnNonExistingPackage()
        {
            var file = new DynamicPackage(new Version(1,0), this, false, false);
            Assert.AreEqual(PackageOpenResultType.FileNotFound, (await file.Open(PackageFilename)).Result);
        }

        [Test]
        public async Task DoesNotOpenExclusiveLock()
        {
            await CreateAndClosePackage(file => file.WriteString(ContentFileName, SampleText));
            await using (File.OpenRead(PackageFilename))
            using(var file = new DynamicPackageData(new Version(1,0), this))
            {
                var result = await file.Open(PackageFilename);
                Assert.IsFalse(result.IsSuccessful);
            }

            await using (new FileStream(PackageFilename, FileMode.Open, FileAccess.Read, FileShare.Read))
            using(var file = new DynamicPackageData(new Version(1,0), this))
            {
                var result = await file.Open(PackageFilename);
                Assert.IsFalse(result.IsSuccessful);
            }

            await using (new FileStream(PackageFilename, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
            using(var file = new DynamicPackageData(new Version(1,0), this))
            {
                var result = await file.Open(PackageFilename);
                Assert.IsFalse(result.IsSuccessful);
            }

            await using (new FileStream(PackageFilename, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            using(var file = new DynamicPackageData(new Version(1,0), this))
            {
                var result = await file.Open(PackageFilename);
                Assert.IsFalse(result.IsSuccessful);
            }

        }

        [Test]
        public async Task OpensNonExclusiveLock()
        {
            await CreateAndClosePackage(file => file.WriteString(ContentFileName, SampleText));
            
            await using (new FileStream(PackageFilename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using(var file = new DynamicPackageData(new Version(1,0), this))
            {
                var result = await file.Open(PackageFilename);
                Assert.IsTrue(result.IsSuccessful);
            }
            
            await using (new FileStream(PackageFilename, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
            using(var file = new DynamicPackageData(new Version(1,0), this))
            {
                var result = await file.Open(PackageFilename, true);
                Assert.IsTrue(result.IsSuccessful);
            }

            await using (File.OpenWrite(PackageFilename))
            using(var file = new DynamicPackageData(new Version(1,0), this))
            {
                var result = await file.Open(PackageFilename);
                Assert.AreEqual(PackageOpenResultType.Locked, result.Result);
            }

            await using (new FileStream(PackageFilename, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            using(var file = new DynamicPackageData(new Version(1,0), this))
            {
                var result = await file.Open(PackageFilename);
                Assert.AreEqual(PackageOpenResultType.Locked, result.Result);
            }
        }

        [Test]
        public async Task ReadOnly_DoesNotOpenExclusiveLock()
        {
            await CreateAndClosePackage(file => file.WriteString(ContentFileName, SampleText));

            await using (File.OpenWrite(PackageFilename))
            using(var file = new DynamicPackageData(new Version(1,0), this))
            {
                var result = await file.Open(PackageFilename, true);
                Assert.IsFalse(result.IsSuccessful);
            }

            await using (new FileStream(PackageFilename, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            using(var file = new DynamicPackageData(new Version(1,0), this))
            {
                var result = await file.Open(PackageFilename, true);
                Assert.IsFalse(result.IsSuccessful);
            }
        }

        [Test]
        public async Task ReadOnly_OpensNonExclusiveLock()
        {
            await CreateAndClosePackage(file => file.WriteString(ContentFileName, SampleText));
            await using (File.OpenRead(PackageFilename))
            using(var file = new DynamicPackageData(new Version(1,0), this))
            {
                var result = await file.Open(PackageFilename, true);
                Assert.IsTrue(result.IsSuccessful);
            }

            await using (new FileStream(PackageFilename, FileMode.Open, FileAccess.Read, FileShare.Read))
            using(var file = new DynamicPackageData(new Version(1,0), this))
            {
                var result = await file.Open(PackageFilename, true);
                Assert.IsTrue(result.IsSuccessful);
            }

            await using (new FileStream(PackageFilename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using(var file = new DynamicPackageData(new Version(1,0), this))
            {
                var result = await file.Open(PackageFilename, true);
                Assert.IsTrue(result.IsSuccessful);
            }

            await using (new FileStream(PackageFilename, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
            using(var file = new DynamicPackageData(new Version(1,0), this))
            {
                var result = await file.Open(PackageFilename, true);
                Assert.IsTrue(result.IsSuccessful);
            }

            await using (new FileStream(PackageFilename, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
            using(var file = new DynamicPackageData(new Version(1,0), this))
            {
                var result = await file.Open(PackageFilename, true);
                Assert.IsTrue(result.IsSuccessful);
            }
        }

        [Test]
        public async Task FailsOpeningOtherApplicationPackage()
        {
            // Open, save & close the file.
            using (var file = new DynamicPackage(new Version(1, 0), this, false, true, "OtherApp"))
            {
                await file.Save(PackageFilename);
            }

            var fileOpen = new DynamicPackage(new Version(1,0), this, false, true);

            var result = await fileOpen.Open(PackageFilename);
            Assert.AreEqual(PackageOpenResultType.IncompatibleApplication, result.Result);
        }

        [Test]
        public async Task FailsOpeningWhenAlreadyOpen()
        {
            var package = await CreateAndSavePackage(null);
            Assert.ThrowsAsync<InvalidOperationException>(async () => await package.Open(PackageFilename));
        }
    }
}