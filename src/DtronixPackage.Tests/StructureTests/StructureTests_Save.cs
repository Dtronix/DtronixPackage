using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DtronixPackage.Upgrades;
using NUnit.Framework;

namespace DtronixPackage.Tests.StructureTests
{
    public class StructureTests_Save : StructureTestBase
    {
        private string _changelogEntry;

        private Dictionary<string, object> _expectedContents;

        public override async Task Setup()
        {
            await base.Setup();

            // Set to a specific point in time so that the JSON writer does not trim off trailing zeros.
            var dateTimeOffsetNow = DateTimeOffset.Parse("2020-08-03T17:42:58.0241586-04:00");
            Package.DateTimeOffsetOverride = dateTimeOffsetNow;
            _changelogEntry = "[{\"Type\":3,\"Username\":\"" + Package.Username + "\",\"ComputerName\":\"" +
                              Package.ComputerName + "\",\"Time\":\"" + dateTimeOffsetNow.ToString("O") + "\"}]";

            _expectedContents = new Dictionary<string, object>
            {
                {"version", DynamicPackage.CurrentPkgVersion},
                {"DtronixPackage.Tests/version", "1.0.0.0"},
                {"DtronixPackage.Tests/changelog.json", _changelogEntry},
            };

        }

        private async Task<ZipArchive> BuildArchive()
        {
            var items = _expectedContents.Select(k => (k.Key, k.Value));

            return await PackageBuilder.CreateZipArchive(items.ToArray());
        }

        private async Task CompareArchives()
        {
            var expected = await BuildArchive();
            await Package.Save(PackageFilename);
            Package.Close();
            await PackageBuilder.AreEqual(expected, PackageFilename);
        }

        [Test]
        public async Task WritesChangelog()
        {
            await CompareArchives();
        }

        [Test]
        public async Task WritesJsonFile()
        {
            _expectedContents.Add("DtronixPackage.Tests/test.json", "{\"Integer\":543210,\"Double\":1234.5678,\"String\":\"Test String\",\"Byte\":128,\"Bytes\":\"AAECAwQFBgcICQ==\",\"DateTimeOffset\":\"2020-08-03T17:42:58.0241586-04:00\"}");
            Package.Writing = async (writer, package) =>
            {
                await writer.WriteJson("test.json", new TestJsonObject()
                {
                    Byte = 128,
                    Bytes = new byte[] {0, 1, 2, 3, 4, 5, 6, 7, 8, 9},
                    DateTimeOffset = DateTimeOffset.Parse("2020-08-03T17:42:58.0241586-04:00"),
                    Double = 1234.5678d,
                    Integer = 543210,
                    String = "Test String"
                });
            };
            await CompareArchives();
        }

        [Test]
        public async Task WritesStringFile()
        {
            var testString = "This is the test string.";
            _expectedContents.Add("DtronixPackage.Tests/test.txt", testString);
            Package.Writing = async (writer, package) =>
            {
                await writer.Write("test.txt", testString);
            };
            await CompareArchives();
        }

        [Test]
        public async Task WritesBinaryFile()
        {
            var binaryData = new byte[] {1, 2, 3, 4, 5, 6, 7, 8, 9, 0};
            _expectedContents.Add("DtronixPackage.Tests/test.bin", binaryData);
            Package.Writing = async (writer, package) =>
            {
                await using var memoryStream = new MemoryStream(binaryData);
                await writer.Write("test.bin", memoryStream);
            };
            await CompareArchives();
        }

    }
}
