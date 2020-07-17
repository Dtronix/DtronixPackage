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
    public class PackageTests_Content : IntegrationTestBase
    {
        [Test]
        public async Task WritesAndReadsJson()
        {
            await CreateAndCloseFile(async file => await file.WriteJson(ContentFileName, SampleJson));

            await OpenWaitForCompletionFile(async file =>
            {
                var readJson = await file.ReadJson<SampleJsonObj>(ContentFileName);
                Assert.AreEqual(SampleJson.Data, readJson.Data);
            });
        }

        [Test]
        public async Task WritesAndReadsString()
        {
            await CreateAndCloseFile(file => file.WriteString(ContentFileName, SampleText));

            await OpenWaitForCompletionFile(async file =>
            {
                Assert.AreEqual(SampleText, await file.ReadString(ContentFileName));
                return true;
            });
        }

        [Test]
        public async Task WritesAndReadsStream()
        {
            var saveStream = new MemoryStream(SampleByteArray);
            await CreateAndCloseFile(async file => await file.WriteStream(ContentFileName, saveStream));

            await OpenWaitForCompletionFile(file =>
            {
                var stream = file.GetStream(ContentFileName);
                byte[] readBuffer = new byte[10];
                stream.Read(readBuffer);

                Assert.AreEqual(SampleByteArray, readBuffer);
                return Task.FromResult(true);
            });
        }
    }
}