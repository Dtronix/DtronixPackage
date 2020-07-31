using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;

namespace DtronixPackage.Tests.IntegrationTests
{
    public class PackageTests_LockFile : IntegrationTestBase
    {
        [Test]
        public async Task CreatesOnOpen()
        {
            // Open, save & close the file.
            var file = new DynamicPackage(new Version(1,0), this, false, true);
            await file.Save(PackageFilename);
            await Utilities.AssertFileExistWithin(file.SavePath + ".lock");
        }

        [Test]
        public async Task DeletesOnClose()
        {
            // Open, save & close the file.
            var file = new DynamicPackage(new Version(1,0), this, false, true);
            await file.Save(PackageFilename);
            file.Close();
            await Utilities.AssertFileDoesNotExistWithin(file.SavePath + ".lock");
        }

        
        [Test]
        public async Task DoesNotCreateLockFile()
        {
            // Open, save & close the file.
            var file = new DynamicPackage(new Version(1,0), this, false, false);
            await file.Save(PackageFilename);
            await Utilities.AssertFileDoesNotExistWithin(file.SavePath + ".lock");
            file.Close();
        }

        [Test]
        public async Task RemovesLockFileAfterChangeInSaveDestination()
        {
            // Open, save & close the file.
            var file = new DynamicPackage(new Version(1,0), this, false, true);
            await file.Save(PackageFilename);
            await Utilities.AssertFileExistWithin(PackageFilename + ".lock");
            var secondPath = Path.Combine("saves/", Guid.NewGuid() + ".file");
            await file.Save(secondPath);
            await Utilities.AssertFileDoesNotExistWithin(PackageFilename + ".lock");
            await Utilities.AssertFileExistWithin(secondPath + ".lock");
        }
    }
}