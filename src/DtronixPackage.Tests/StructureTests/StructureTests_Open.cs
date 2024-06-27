using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using DtronixPackage.Upgrades;
using NLog;
using NUnit.Framework;

namespace DtronixPackage.Tests.StructureTests
{
    public class StructureTests_Open : StructureTestBase
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        [Test]
        public async Task BasicPackage()
        {
            var packagePath = await PackageBuilder.CreateFile(new (string Path, object Data)[]
            {
                ("version", "1.1.0"),
                ("DtronixPackage.Tests/version", "1.0.0"),
            });
            var result = await Package.Open(packagePath);
            Assert.That(result, Is.EqualTo(PackageOpenResult.Success));
        }

        [Test]
        public async Task ReadsChangelog()
        {
            var packagePath = await PackageBuilder.CreateFile(new (string Path, object Data)[]
            {
                ("version", "1.1.0"),
                ("DtronixPackage.Tests/version", "1.0.0"),
                ("DtronixPackage.Tests/changelog.json", "[{\"Type\":3,\"Username\":\"TestUser1\",\"ComputerName\":\"UserComputer\",\"Time\":\"2020-08-03T17:42:58.0241586-04:00\",\"Note\":\"Test Note\"}]")
            });

            await Package.Open(packagePath);

            Assert.That(Package.Changelog.Count, Is.EqualTo(1));

            Assert.That(Package.Changelog[0].Note, Is.EqualTo("Test Note"));
            Assert.That(Package.Changelog[0].Time, Is.EqualTo(DateTimeOffset.Parse("2020-08-03T17:42:58.0241586-04:00")));
            Assert.That(Package.Changelog[0].ComputerName, Is.EqualTo("UserComputer"));
            Assert.That(Package.Changelog[0].Username, Is.EqualTo("TestUser1"));
            Assert.That(Package.Changelog[0].Type, Is.EqualTo(ChangelogEntryType.Save));
        }

        [Test]
        public async Task ReadsJsonFile()
        {
            var packagePath = await PackageBuilder.CreateFile(new (string Path, object Data)[]
            {
                ("version", "1.1.0"),
                ("DtronixPackage.Tests/version", "1.0.0"),
                ("DtronixPackage.Tests/test.json", "{\"Integer\":543210,\"Double\":1234.5678,\"String\":\"Test String\",\"Byte\":128,\"Bytes\":\"AAECAwQFBgcICQ==\",\"DateTimeOffset\":\"2020-08-03T17:42:58.0241586-04:00\"}")
            });
            Package.Reading = async (reader, package) =>
            {
                var testJson = await reader.ReadJson<TestJsonObject>("test.json");

                Assert.That(testJson.Byte, Is.EqualTo((byte)128));
                Assert.That(testJson.Bytes, Is.EqualTo(new byte[] {0, 1, 2, 3, 4, 5, 6, 7, 8, 9}));
                Assert.That(testJson.DateTimeOffset, Is.EqualTo(DateTimeOffset.Parse("2020-08-03T17:42:58.0241586-04:00")));
                Assert.That(testJson.Double, Is.EqualTo(1234.5678d));
                Assert.That(testJson.Integer, Is.EqualTo(543210));
                Assert.That(testJson.String, Is.EqualTo("Test String"));

                return true;
            };

            Assert.That(await Package.Open(packagePath), Is.EqualTo(PackageOpenResult.Success));
        }

        [Test]
        public async Task ReadsStringFile()
        {
            var testString = "This is the test string.";
            var packagePath = await PackageBuilder.CreateFile(new (string Path, object Data)[]
            {
                ("version", "1.1.0"),
                ("DtronixPackage.Tests/version", "1.0.0"),
                ("DtronixPackage.Tests/test.txt", testString)
            });
            Package.Reading = async (reader, package) =>
            {
                Assert.That(await reader.ReadString("test.txt"), Is.EqualTo(testString));
                return true;
            };

            Assert.That(await Package.Open(packagePath), Is.EqualTo(PackageOpenResult.Success));
        }  
        
        [Test]
        public async Task ReadsBinaryFile()
        {
            var binaryData = new byte[] {1, 2, 3, 4, 5, 6, 7, 8, 9, 0};
            var packagePath = await PackageBuilder.CreateFile(new (string Path, object Data)[]
            {
                ("version", "1.1.0"),
                ("DtronixPackage.Tests/version", "1.0.0"),
                ("DtronixPackage.Tests/test.bin", binaryData)
            });
            Package.Reading = async (reader, package) =>
            {
                var readData = new byte[binaryData.Length];
                await using var stream = reader.GetStream("test.bin");
                await stream.ReadAsync(readData);
                Assert.That(readData, Is.EqualTo(binaryData));
                return true;
            };

            Assert.That(await Package.Open(packagePath), Is.EqualTo(PackageOpenResult.Success));
        }

    }
}
