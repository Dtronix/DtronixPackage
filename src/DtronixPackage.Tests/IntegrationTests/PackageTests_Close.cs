using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;

namespace DtronixPackage.Tests.IntegrationTests
{
    public class PackageTests_Close : IntegrationTestBase
    {
        [Test]
        public void ClosesEmptyPackage()
        {
            var package = new DynamicPackage<SimplePackageContent>(new Version(1, 0), this, false, false);

            Assert.DoesNotThrow(() => package.Close());
        }
    }
}