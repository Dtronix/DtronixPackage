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
    public class PackageUpgradeTests : IntegrationTestBase
    {
        private static Logger _logger = LogManager.GetCurrentClassLogger();

        public PackageUpgradeTests()
        {

        }

        [Test]
        public async Task UpgradeIgnoresPastVersions()
        {
            await CreateAndCloseFile(async f => await f.WriteString(ContentFileName, SampleText));
            var file = new PackageDynamicFile(new Version(1, 1), this, true, false);
            bool upgradeRan = false;
            file.UpgradeOverrides.Add(new PackageCallbackUpgrade(new Version(1, 0), document =>
            {
                upgradeRan = true;
                return Task.FromResult(true);
            }));

            await file.Open(ZipFilename);

            Assert.IsFalse(upgradeRan);
        }

        [Test]
        public async Task UpgradeIgnoresFutureUpgrades()
        {
            await CreateAndCloseFile(async f => await f.WriteString(ContentFileName, SampleText));
            var file = new PackageDynamicFile(new Version(1, 0), this, true, false);
            bool upgradeRan = false;
            file.UpgradeOverrides.Add(new PackageCallbackUpgrade(new Version(1, 1), document =>
            {
                upgradeRan = true;
                return Task.FromResult(true);
            }));

            await file.Open(ZipFilename);

            Assert.IsFalse(upgradeRan);
        }

        [Test]
        public async Task UpgradeUpgradesOldFile()
        {
            await CreateAndCloseFile(async f => await f.WriteString(ContentFileName, SampleText));
            var file = new PackageDynamicFile(new Version(1, 1), this, true, false);
            bool upgradeRan = false;
            file.UpgradeOverrides.Add(new PackageCallbackUpgrade(new Version(1, 1), document =>
            {
                upgradeRan = true;
                return Task.FromResult(true);
            }));

            await file.Open(ZipFilename);

            Assert.IsTrue(upgradeRan);
        }     
        
        [Test]
        public async Task UpgradeOpenFailsOnFalseUpgradeReturn()
        {
            await CreateAndCloseFile(async f => await f.WriteString(ContentFileName, SampleText));
            var file = new PackageDynamicFile(new Version(1, 1), this, true, false);
            file.UpgradeOverrides.Add(new PackageCallbackUpgrade(new Version(1, 1), document => Task.FromResult(false)));
            var openResult = await file.Open(ZipFilename);

            Assert.AreEqual(PackageOpenResultType.UpgradeFailure, openResult.OpenFileOpenResultType);
        }

        [Test]
        public async Task UpgradeOpenSucceedsOnTrueUpgradeReturn()
        {
            await CreateAndCloseFile(async f => await f.WriteString(ContentFileName, SampleText));
            var file = new PackageDynamicFile(new Version(1, 1), this, true, false);
            file.UpgradeOverrides.Add(new PackageCallbackUpgrade(new Version(1, 1), document => Task.FromResult(true)));
            var openResult = await file.Open(ZipFilename);

            Assert.AreEqual(PackageOpenResultType.Success, openResult.OpenFileOpenResultType);
        }
        /*
        [Test]
        public async Task UpgradeIgnoresPastVersions()
        {
            await CreateAndCloseFile(async f => await f.WriteString(ContentFileName, SampleText));
            var file = new DtronixPackageDynamicFile(new Version(1, 1), this, true, false);
            bool upgradeRan = false;
            file.UpgradeOverrides.Add(new DtronixPackageCallbackUpgrade(new Version(1, 1), document =>
            {
                upgradeRan = true;
                return Task.FromResult(true);
            }));

            var openResult = await file.Open(ZipFilename);

            Assert.AreEqual(FileOpenResult.Result.Success, openResult.OpenResult);

            WaitTest
        }*/

    }
}