using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;

namespace DtronixPackage.Tests.IntegrationTests
{
    public class PackageTests_Content : IntegrationTestBase
    {
        [Test]
        public async Task WritesAndReadsJson()
        {
            await CreateAndClosePackage(async file => await file.WriteJson(ContentFileName, SampleJson));

            await OpenWaitForCompletionPackage(async file =>
            {
                var readJson = await file.ReadJson<SampleJsonObj>(ContentFileName);
                Assert.AreEqual(SampleJson.Data, readJson.Data);
            });
        }

        [Test]
        public async Task WritesAndReadsString()
        {
            await CreateAndClosePackage(file => file.WriteString(ContentFileName, SampleText));

            await OpenWaitForCompletionPackage(async file =>
            {
                Assert.AreEqual(SampleText, await file.ReadString(ContentFileName));
                return true;
            });
        }

        [Test]
        public async Task WritesAndReadsStream()
        {
            var saveStream = new MemoryStream(SampleByteArray);
            await CreateAndClosePackage(async file => await file.WriteStream(ContentFileName, saveStream));

            await OpenWaitForCompletionPackage(file =>
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