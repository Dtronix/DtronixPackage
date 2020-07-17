using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;

namespace DtronixPackage.Tests.IntegrationTests
{
    public class PackageTests_AutoSave : IntegrationTestBase
    {

        [Test]
        public async Task CreatesAutoSavePackage()
        {
            var tempSave = PackageFilename + ".temp";
            var file = new DynamicPackage(new Version(1, 0), this, false, false)
            {
                TempPackagePathRequest = () => tempSave
            };
            file.ContentModifiedOverride();
            await file.ConfigureAutoSave(0, -1);

            await AssertFileExistWithin(tempSave);
        }


        [Test]
        public async Task CreatesAutoSavePackageOnlyIfNotSavedSinceLastAutoSave()
        {
            var tempSave = PackageFilename + ".temp";
            var file = new DynamicPackage(new Version(1,0), this, false, false)
            {
                TempPackagePathRequest = () => tempSave
            };

            file.ContentModifiedOverride();
            await file.Save(PackageFilename);
            await file.ConfigureAutoSave(0, -1);

            await AssertFileDoesNotExistWithin(tempSave);
        }

        [Test]
        public async Task SavesOnlyWhenPackageIsModified()
        {
            var tempSave = PackageFilename + ".temp";
            var file = new DynamicPackage(new Version(1,0), this, false, true)
            {
                TempPackagePathRequest = () => tempSave
            };
            await file.ConfigureAutoSave(0, -1);
            await AssertFileDoesNotExistWithin(tempSave);

            file.AutoSaveEnabled = false;
            file.ContentModifiedOverride();
            file.AutoSaveEnabled = true;
            await AssertFileExistWithin(tempSave);
        }

        
        [Test]
        public async Task SetsIsDataModifiedSinceAutoSaveToFalse()
        {
            var tempSave = PackageFilename + ".temp";
            var file = new DynamicPackageData(new Version(1,0), this)
            {
                TempPackagePathRequest = () => tempSave
            };
            file.Data.Children.Add(new PackageDataContractChild());

            await file.ConfigureAutoSave(0, -1);

            await AssertFileExistWithin(tempSave);
            Assert.IsFalse(file.IsDataModifiedSinceAutoSave);
        }

        [Test]
        public void IsDataModifiedSinceAutoSaveIsModified()
        {
            var tempSave = PackageFilename + ".temp";
            var file = new DynamicPackageData(new Version(1,0), this)
            {
                TempPackagePathRequest = () => tempSave
            };
            Assert.IsFalse(file.IsDataModifiedSinceAutoSave);
            file.ContentModifiedOverride();
            Assert.IsTrue(file.IsDataModifiedSinceAutoSave);
        }

        [Test]
        public async Task DoesNotChangeIsDataModified()
        {
            var tempSave = PackageFilename + ".temp";
            var file = new DynamicPackage(new Version(1,0), this, false, true)
            {
                TempPackagePathRequest = () => tempSave
            };
            file.ContentModifiedOverride();
            await file.ConfigureAutoSave(0, -1);
            await AssertFileExistWithin(tempSave);

            Assert.IsTrue(file.IsDataModified);
        }

        [Test]
        public async Task SavesAgainAfterDataIsModified()
        {
            var tempSave = PackageFilename + ".temp";
            var file = new DynamicPackage(new Version(1,0), this, false, true)
            {
                TempPackagePathRequest = () => tempSave
            };

            // Autosave once.
            file.ContentModifiedOverride();
            await file.ConfigureAutoSave(0, 100);
            await AssertFileExistWithin(tempSave);

            var initialWriteTime = new FileInfo(tempSave).LastWriteTime;

            // Attempt to autosave again.
            file.ContentModifiedOverride();

            DateTime lastWriteTime = new FileInfo(tempSave).LastWriteTime;
            for (int i = 0; i < 20; i++)
            {
                if(lastWriteTime != initialWriteTime)
                    break;

                await Task.Delay(50);

                lastWriteTime = new FileInfo(tempSave).LastWriteTime;
            }
            Assert.AreNotEqual(initialWriteTime, lastWriteTime);

            await file.ConfigureAutoSave(-1, -1);
        }

        [Test]
        public async Task SavesAgainAfterTempFileIsDeleted()
        {
            var tempSave = PackageFilename + ".temp";
            var file = new DynamicPackage(new Version(1,0), this, false, true)
            {
                TempPackagePathRequest = () => tempSave
            };

            // Autosave once.
            file.ContentModifiedOverride();
            await file.ConfigureAutoSave(0, 10);
            await AssertFileExistWithin(tempSave);

            Logger.Trace("Deleting {0}", tempSave);
            // Remove the file
            File.Delete(tempSave);
            Logger.Trace("Deleted {0}", tempSave);

            // Attempt to autosave again.
            file.ContentModifiedOverride();
            await AssertFileExistWithin(tempSave);

            await file.ConfigureAutoSave(-1, -1);
        }

    }
}