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
            await CreateAndClosePackage((writer, package) => Task.CompletedTask, new Version(2, 0));
            var file = new DynamicPackageData(new Version(1,0), this);
            Assert.That((await file.Open(PackageFilename)).Result, Is.EqualTo(PackageOpenResultType.IncompatibleVersion));
        }

        [Test]
        public async Task FailsOnEmptyPackage()
        {
            File.Create(PackageFilename).Close();

            // Open, save & close the file.
            var file = new DynamicPackage(new Version(1,0), this, false, false);
            Assert.That((await file.Open(PackageFilename)).Result, Is.EqualTo(PackageOpenResultType.Corrupted));
        }

        [Test]
        public async Task FailsOnLockedPackage()
        {
            var fileStream = File.Create(PackageFilename);

            // Open, save & close the file.
            var file = new DynamicPackage(new Version(1,0), this, false, false);
            Assert.That((await file.Open(PackageFilename)).Result, Is.EqualTo(PackageOpenResultType.Locked));

            fileStream.Close();
            File.Delete(PackageFilename);
        }

        [Test]
        public async Task FailsOnLockedLockPackage()
        {
            await CreateAndClosePackage((writer, package) => writer.Write(ContentFileName, SampleText));

            var fileStream = File.Create(PackageFilename + ".lock");

            // Open, save & close the file.
            var file = new DynamicPackage(new Version(1,0), this, false, true);
            Assert.That((await file.Open(PackageFilename)).Result, Is.EqualTo(PackageOpenResultType.Locked));

            fileStream.Close();
        }

        [Test]
        public async Task PackageAlreadyOpenFails()
        {
            // Open, save & close the file.
            var file = new DynamicPackage(new Version(1,0), this, false, true);
            file.Writing = async (writer, package) => await writer.Write(ContentFileName, SampleText);
            await file.Save(PackageFilename);

            var fileOpen = new DynamicPackage(new Version(1,0), this, false, false);

            Assert.That((await fileOpen.Open(PackageFilename)).Result, Is.EqualTo(PackageOpenResultType.Locked));
        }

        [Test]
        public async Task PackageAlreadyOpenSetsLockedPackageResult()
        {
            // Open, save & close the file.
            var file = new DynamicPackage(new Version(1,0), this, false, true);
            file.Writing = async (writer, package) => await writer.Write(ContentFileName, SampleText);
            await file.Save(PackageFilename);

            var fileOpen = new DynamicPackage(new Version(1,0), this, false, true);

            var result = await fileOpen.Open(PackageFilename);
            Assert.That(result.Result, Is.EqualTo(PackageOpenResultType.Locked));

            Assert.That(result.LockInfo, Is.Not.Null);

            file.Close();

            result = await fileOpen.Open(PackageFilename);
            Assert.That(result.IsSuccessful, Is.True);

            Assert.That(result.LockInfo, Is.Null);
        }

        [Test]
        public async Task ReturnsSuccess()
        {
            await CreateAndClosePackage((writer, package) => writer.Write(ContentFileName, SampleText));

            var file = new DynamicPackage(new Version(1,0), this, false, false);
            Assert.That(await file.Open(PackageFilename), Is.EqualTo(PackageOpenResult.Success));
        }

        [Test]
        public async Task FailsOnNonExistingPackage()
        {
            var file = new DynamicPackage(new Version(1,0), this, false, false);
            Assert.That((await file.Open(PackageFilename)).Result, Is.EqualTo(PackageOpenResultType.FileNotFound));
        }

        [Test]
        public async Task DoesNotOpenExclusiveLock()
        {
            await CreateAndClosePackage((writer, package) => writer.Write(ContentFileName, SampleText));
            await using (File.OpenRead(PackageFilename))
            using(var file = new DynamicPackageData(new Version(1,0), this))
            {
                var result = await file.Open(PackageFilename);
                Assert.That(result.IsSuccessful, Is.False);
            }

            await using (new FileStream(PackageFilename, FileMode.Open, FileAccess.Read, FileShare.Read))
            using(var file = new DynamicPackageData(new Version(1,0), this))
            {
                var result = await file.Open(PackageFilename);
                Assert.That(result.IsSuccessful, Is.False);
            }

            await using (new FileStream(PackageFilename, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
            using(var file = new DynamicPackageData(new Version(1,0), this))
            {
                var result = await file.Open(PackageFilename);
                Assert.That(result.IsSuccessful, Is.False);
            }

            await using (new FileStream(PackageFilename, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            using(var file = new DynamicPackageData(new Version(1,0), this))
            {
                var result = await file.Open(PackageFilename);
                Assert.That(result.IsSuccessful, Is.False);
            }

        }

        [Test]
        public async Task OpensNonExclusiveLock()
        {
            await CreateAndClosePackage((writer, package) => writer.Write(ContentFileName, SampleText));
            
            await using (new FileStream(PackageFilename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using(var file = new DynamicPackageData(new Version(1,0), this))
            {
                var result = await file.Open(PackageFilename);
                Assert.That(result.IsSuccessful, Is.True);
            }
            
            await using (new FileStream(PackageFilename, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
            using(var file = new DynamicPackageData(new Version(1,0), this))
            {
                var result = await file.Open(PackageFilename, true);
                Assert.That(result.IsSuccessful, Is.True);
            }

            await using (File.OpenWrite(PackageFilename))
            using(var file = new DynamicPackageData(new Version(1,0), this))
            {
                var result = await file.Open(PackageFilename);
                Assert.That(result.Result, Is.EqualTo(PackageOpenResultType.Locked));
            }

            await using (new FileStream(PackageFilename, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            using(var file = new DynamicPackageData(new Version(1,0), this))
            {
                var result = await file.Open(PackageFilename);
                Assert.That(result.Result, Is.EqualTo(PackageOpenResultType.Locked));
            }
        }

        [Test]
        public async Task ReadOnly_DoesNotOpenExclusiveLock()
        {
            await CreateAndClosePackage((writer, package) => writer.Write(ContentFileName, SampleText));

            await using (File.OpenWrite(PackageFilename))
            using(var file = new DynamicPackageData(new Version(1,0), this))
            {
                var result = await file.Open(PackageFilename, true);
                Assert.That(result.IsSuccessful, Is.False);
            }

            await using (new FileStream(PackageFilename, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            using(var file = new DynamicPackageData(new Version(1,0), this))
            {
                var result = await file.Open(PackageFilename, true);
                Assert.That(result.IsSuccessful, Is.False);
            }
        }

        [Test]
        public async Task ReadOnly_OpensNonExclusiveLock()
        {
            await CreateAndClosePackage((writer, package) => writer.Write(ContentFileName, SampleText));
            await using (File.OpenRead(PackageFilename))
            using(var file = new DynamicPackageData(new Version(1,0), this))
            {
                var result = await file.Open(PackageFilename, true);
                Assert.That(result.IsSuccessful, Is.True);
            }

            await using (new FileStream(PackageFilename, FileMode.Open, FileAccess.Read, FileShare.Read))
            using(var file = new DynamicPackageData(new Version(1,0), this))
            {
                var result = await file.Open(PackageFilename, true);
                Assert.That(result.IsSuccessful, Is.True);
            }

            await using (new FileStream(PackageFilename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using(var file = new DynamicPackageData(new Version(1,0), this))
            {
                var result = await file.Open(PackageFilename, true);
                Assert.That(result.IsSuccessful, Is.True);
            }

            await using (new FileStream(PackageFilename, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
            using(var file = new DynamicPackageData(new Version(1,0), this))
            {
                var result = await file.Open(PackageFilename, true);
                Assert.That(result.IsSuccessful, Is.True);
            }

            await using (new FileStream(PackageFilename, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
            using(var file = new DynamicPackageData(new Version(1,0), this))
            {
                var result = await file.Open(PackageFilename, true);
                Assert.That(result.IsSuccessful, Is.True);
            }
        }

        [Test]
        public async Task FailsOpeningOtherApplicationPackage()
        {
            // Open, save & close the file.
            using (var file = new DynamicPackage(new Version(1, 0), this, false, true, "OtherApp"))
                await file.Save(PackageFilename);
            

            var fileOpen = new DynamicPackage(new Version(1,0), this, false, true);

            var result = await fileOpen.Open(PackageFilename);
            Assert.That(result.Result, Is.EqualTo(PackageOpenResultType.IncompatibleApplication));
        }

        [Test]
        public async Task FailsOpeningWhenAlreadyOpen()
        {
            var package = await CreateAndSavePackage(null);
            Assert.ThrowsAsync<InvalidOperationException>(async () => await package.Open(PackageFilename));
        }

        [Test]
        public async Task DoesNotNotifyChangesMadeToPackageContents()
        {
            await CreateAndClosePackage(null);

            var fileOpen = new DynamicPackage(new Version(1, 0), this, false, true)
            {
                IsMonitorEnabled = true,
                Reading = (reader, package) =>
                {
                    package.Content.MainText = "test";
                    return Task.FromResult(true);
                }
            };

            await fileOpen.Open(PackageFilename);

            Assert.That(fileOpen.IsContentModified, Is.False);
        }
    }
}