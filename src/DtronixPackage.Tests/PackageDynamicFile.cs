using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DtronixPackage.Tests
{
    public class PackageDynamicFile : Package<FileContent>
    {
        private readonly IntegrationTestBase _integrationTest;

        public Func<PackageDynamicFile, Task<bool>> Opening;

        public Func<PackageDynamicFile, Task> Saving;

        public Func<string> TempFilePathRequest;

        public List<PackageUpgrade<FileContent>> UpgradeOverrides => Upgrades;

        public PackageDynamicFile(
            Version appVersion,
            IntegrationTestBase integrationTest, 
            bool preserveUpgrade,
            bool useLockFile) 
            : base("DtronixPackage.Tests", appVersion, preserveUpgrade, useLockFile)
        {
            _integrationTest = integrationTest;
        }

        protected override async Task<bool> OnOpen(bool isUpgrade)
        {
            try
            {
                if (Opening == null)
                    return true;

                return await Opening(this);
            }
            catch (Exception ex)
            {
                _integrationTest.ThrowException = ex;
                _integrationTest.TestComplete.Set();
            }
            
            return false;
        }

        protected override async Task OnSave()
        {
            try
            {
                if(Saving != null)
                    await Saving(this);
            }
            catch (Exception ex)
            {
                _integrationTest.ThrowException = ex;
                _integrationTest.TestComplete.Set();
            }

        }

        protected override string OnTempFilePathRequest(string fileName)
        {
            var path = TempFilePathRequest?.Invoke();

            return path;
        }
        
        public void ContentModifiedOverride()
        {
            DataModified();
        }
    }
}
