using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DtronixPackage.Tests.IntegrationTests;

namespace DtronixPackage.Tests
{

    public class DynamicPackage : DynamicPackage<TestPackageContent>
    {
        public DynamicPackage(
            Version appVersion,
            IntegrationTestBase integrationTest,
            bool preserveUpgrade,
            bool useLockFile)
            : base(appVersion, integrationTest, preserveUpgrade, useLockFile)
        {
        }

        public DynamicPackage(
            Version appVersion,
            IntegrationTestBase integrationTest,
            bool preserveUpgrade,
            bool useLockFile,
            string appName)
            : base(appVersion, integrationTest, preserveUpgrade, useLockFile, appName)
        {
        }
    }

    public class DynamicPackage<TContent> : Package<TContent>
        where TContent : PackageContent, new()
    {
        private readonly IntegrationTestBase _integrationTest;

        public Func<PackageReader, DynamicPackage<TContent>, Task<bool>> Reading;

        public Func<PackageWriter, DynamicPackage<TContent>, Task> Writing;

        public Func<string> TempPackagePathRequest;

        public List<PackageUpgrade> UpgradeOverrides => Upgrades;

        public DateTimeOffset? DateTimeOffsetOverride { get; set; }

        internal override DateTimeOffset CurrentDateTimeOffset => DateTimeOffsetOverride ?? DateTimeOffset.Now;

        public DynamicPackage(
            Version appVersion,
            IntegrationTestBase integrationTest, 
            bool preserveUpgrade,
            bool useLockFile) 
            : this(appVersion, integrationTest, preserveUpgrade, useLockFile, "DtronixPackage.Tests")
        {
        }

        public DynamicPackage(
            Version appVersion,
            IntegrationTestBase integrationTest, 
            bool preserveUpgrade,
            bool useLockFile,
            string appName) 
            : base(appName, appVersion, preserveUpgrade, useLockFile)
        {
            _integrationTest = integrationTest;
            Logger = new NLogLogger(nameof(DynamicPackage<TContent>));
        }

        protected override async Task<bool> OnRead(PackageReader reader)
        {
            try
            {
                if (Reading == null)
                    return true;

                return await Reading(reader, this);
            }
            catch (Exception ex)
            {
                if (_integrationTest != null)
                {
                    _integrationTest.ThrowException = ex;
                    _integrationTest.TestComplete.Set();
                }

                throw;
            }
        }

        protected override async Task OnWrite(PackageWriter writer)
        {
            try
            {
                if(Writing != null)
                    await Writing(writer, this);
            }
            catch (Exception ex)
            {
                if (_integrationTest != null)
                {
                    _integrationTest.ThrowException = ex;
                    _integrationTest.TestComplete.Set();
                }

                throw;
            }

        }

        protected override string OnTempFilePathRequest(string fileName)
        {
            var path = TempPackagePathRequest?.Invoke();

            return path;
        }
        
        public void ContentModifiedOverride()
        {
            DataModified();
        }
    }
}
