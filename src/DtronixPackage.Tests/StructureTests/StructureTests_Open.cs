using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using DtronixPackage.Upgrades;
using NUnit.Framework;

namespace DtronixPackage.Tests.StructureTests
{
    public class StructureTests_Open : StructureTestBase
    {

        [Test]
        public async Task BasicPackage()
        {
            var packagePath = await PackageBuilder.CreateFile(new (string Path, object Data)[]
            {
                ("version", "1.1.0"),
                ("DtronixPackage.Tests/version", "1.0.0"),
            });

            Assert.AreEqual(PackageOpenResult.Success, await Package.Open(packagePath));
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

            Assert.AreEqual(1, Package.Changelog.Count);

            Assert.AreEqual("Test Note", Package.Changelog[0].Note);
            Assert.AreEqual(DateTimeOffset.Parse("2020-08-03T17:42:58.0241586-04:00"), Package.Changelog[0].Time);
            Assert.AreEqual("UserComputer", Package.Changelog[0].ComputerName);
            Assert.AreEqual("TestUser1", Package.Changelog[0].Username);
            Assert.AreEqual(ChangelogEntryType.Save, Package.Changelog[0].Type);
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
            Package.Opening = async package =>
            {
                var testJson = await Package.ReadJson<TestJsonObject>("test.json");
                
                Assert.AreEqual((byte)128, testJson.Byte);
                Assert.AreEqual(new byte[] {0, 1, 2, 3, 4, 5, 6, 7, 8, 9}, testJson.Bytes);
                Assert.AreEqual(DateTimeOffset.Parse("2020-08-03T17:42:58.0241586-04:00"),testJson.DateTimeOffset);
                Assert.AreEqual(1234.5678d, testJson.Double);
                Assert.AreEqual(543210, testJson.Integer);
                Assert.AreEqual("Test String", testJson.String);

                return true;
            };

            Assert.AreEqual(PackageOpenResult.Success, await Package.Open(packagePath));
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
            Package.Opening = async package =>
            {
                Assert.AreEqual(testString, await Package.ReadString("test.txt"));
                return true;
            };

            Assert.AreEqual(PackageOpenResult.Success, await Package.Open(packagePath));
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
            Package.Opening = async package =>
            {
                var readData = new byte[binaryData.Length];
                await using var stream = Package.GetStream("test.bin");
                await stream.ReadAsync(readData);
                Assert.AreEqual(binaryData, readData);
                return true;
            };

            Assert.AreEqual(PackageOpenResult.Success, await Package.Open(packagePath));
        }

    }
}
