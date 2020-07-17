using System;
using System.Threading.Tasks;
using NUnit.Framework;

namespace DtronixPackage.Tests.IntegrationTests
{
    public class PackageTests_LockFile : IntegrationTestBase
    {
        [Test]
        public async Task CreatedAndDeleted()
        {
            // Open, save & close the file.
            var file = new DynamicPackage(new Version(1,0), this, false, true);
            await file.Save(PackageFilename);
            await AssertFileExistWithin(file.SavePath + ".lock");

            file.Close();
            await AssertFileDoesNotExistWithin(file.SavePath + ".lock");
        }

        
        [Test]
        public async Task DoesNotCreateLockFile()
        {
            // Open, save & close the file.
            var file = new DynamicPackage(new Version(1,0), this, false, false);
            await file.Save(PackageFilename);
            await AssertFileDoesNotExistWithin(file.SavePath + ".lock");
            file.Close();
        }
    }
}