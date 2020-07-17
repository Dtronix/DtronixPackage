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
    public class PackageTests_LockFile : IntegrationTestBase
    {
        [Test]
        public async Task CreatedAndDeleted()
        {
            // Open, save & close the file.
            var file = new PackageDynamicFile(new Version(1,0), this, false, true);
            await file.Save(ZipFilename);
            await AssertFileExistWithin(file.SavePath + ".lock");

            file.Close();
            await AssertFileDoesNotExistWithin(file.SavePath + ".lock");
        }
    }
}