using System;
using System.IO;
using NUnit.Framework;

namespace DtronixPackage.Tests.IntegrationTests
{
    public class PackageTests_ChangeMonitor : IntegrationTestBase
    {

        private void MonitorChangesTests(DynamicPackageData package, Action test)
        {
            Assert.That(package.IsContentModified, Is.False);
            package.Data.Children.Add(new PackageDataContractChild());
            test.Invoke();

            // Child property
            package.IsContentModified = false;
            package.Data.Children[0].Integer = 50;
            test.Invoke();

            // Child property
            package.IsContentModified = false;
            package.Data.Children[0].String = "new string";
            test.Invoke();

            // New child of child
            package.IsContentModified = false;
            package.Data.Children[0].Children.Add(new PackageDataContractChild());
            test.Invoke();

            // Child of child property
            package.IsContentModified = false;
            package.Data.Children[0].Children[0].Integer = 51;
            test.Invoke();

            // Moving child of child
            package.Data.Children[0].Children.Add(new PackageDataContractChild());
            package.IsContentModified = false;
            package.Data.Children[0].Children.Move(1, 0);
            test.Invoke();

            // Remove child of child
            package.IsContentModified = false;
            package.Data.Children[0].Children.RemoveAt(1);
            test.Invoke();
        }

        [Test]
        public void RegistersChanges()
        {
            var package = new DynamicPackageData(new Version(1,0), this);
            MonitorChangesTests(package, () => Assert.That(package.IsContentModified, Is.True));
        }

        [Test]
        public void DeRegistersChanges()
        {
            var package = new DynamicPackageData(new Version(1,0), this);
            package.MonitorDeregisterOverride(package.Data);
            MonitorChangesTests(package, () => Assert.That(package.IsContentModified, Is.False));
        }

        [Test]
        public void IgnoresChanges()
        {
            var package = new DynamicPackageData(new Version(1,0), this);

            package.MonitorIgnore(() =>
            {
                package.Data.Children.Add(new PackageDataContractChild());
            });

            Assert.That(package.IsContentModified, Is.False);
        }

        [Test]
        public void IgnoresChangesAfterClosing()
        {
            var package = new DynamicPackage<SimplePackageContent>(new Version(1,0), this, false, false);
            var subTypeInstance = package.Content.SubTypeInstance = new SimplePackageContent.SubType();
            package.IsContentModified = false;
            package.Close();

            subTypeInstance.Value = "test 2";

            Assert.That(package.IsContentModified, Is.False);
        }


    }
}