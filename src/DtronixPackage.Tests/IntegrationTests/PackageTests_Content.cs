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
            await CreateAndClosePackage(async (writer, file) => await writer.WriteJson(ContentFileName, SampleJson));

            await OpenWaitForCompletionPackage(async (reader, file) =>
            {
                var readJson = await reader.ReadJson<SampleJsonObj>(ContentFileName);
                Assert.That(readJson.Data, Is.EqualTo(SampleJson.Data));
            });
        }

        [Test]
        public async Task WritesAndReadsString()
        {
            await CreateAndClosePackage((writer, file) => writer.Write(ContentFileName, SampleText));

            await OpenWaitForCompletionPackage(async (reader, file) =>
            {
                Assert.That(await reader.ReadString(ContentFileName), Is.EqualTo(SampleText));
                return true;
            });
        }

        [Test]
        public async Task WritesAndReadsStream()
        {
            var saveStream = new MemoryStream(SampleByteArray);
            await CreateAndClosePackage(async (writer, file) => await writer.Write(ContentFileName, saveStream));

            await OpenWaitForCompletionPackage((reader, file) =>
            {
                var stream = reader.GetStream(ContentFileName);
                byte[] readBuffer = new byte[10];
                stream.Read(readBuffer);

                Assert.That(readBuffer, Is.EqualTo(SampleByteArray));
                return Task.FromResult(true);
            });
        }

        [Test]
        public void ContentIsInstancedOnPackageCreation()
        {
            var package = new DynamicPackage<SimplePackageContent>(new Version(1, 0), this, false, false);
            Assert.That(package.Content, Is.Not.Null);
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
                Assert.That(package.Content.Bytes, Is.EqualTo(default(byte[])));
                Assert.That(package.Content.Byte, Is.EqualTo(default(byte)));
                Assert.That(package.Content.String, Is.EqualTo(default(string)));
                Assert.That(package.Content.Integer, Is.EqualTo(default(int)));
                Assert.That(package.Content.Double, Is.EqualTo(default(double)));
                Assert.That(package.Content.DateTimeOffset, Is.EqualTo(default(DateTimeOffset)));
            });
        }
    }
}