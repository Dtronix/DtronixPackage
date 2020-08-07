using System;
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

        [Test]
        public void ContentIsInstancedOnPackageCreation()
        {
            var package = new DynamicPackage<SimplePackageContent>(new Version(1, 0), this, false, false);
            Assert.NotNull(package.Content);
        }

        [Test]
        public void ContentIsResetOnClose()
        {
            var package = new DynamicPackage<SimplePackageContent>(new Version(1, 0), this, false, false)
            {
                Content =
                {
                    Bytes = new byte[] {0, 1, 2, 3, 4},
                    Double = 12345.6789,
                    String = "String Test",
                    DateTimeOffset = DateTimeOffset.Now,
                    Integer = 50,
                    Byte = 128
                }
            }; 
            package.Close();
            Assert.Multiple(() =>
            {
                Assert.AreEqual(default(byte[]), package.Content.Bytes);
                Assert.AreEqual(default(byte), package.Content.Byte);
                Assert.AreEqual(default(string), package.Content.String);
                Assert.AreEqual(default(int), package.Content.Integer);
                Assert.AreEqual(default(double), package.Content.Double);
                Assert.AreEqual(default(DateTimeOffset), package.Content.DateTimeOffset);
            });
        }
    }
}