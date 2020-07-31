using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DtronixPackage.Tests.PackageManagerViewModelTests
{
    public class PackageManagerViewModelPackage : Package<PackageContent>
    {
        public Func<PackageManagerViewModelPackage, Task<bool>> Opening;

        public Func<PackageManagerViewModelPackage, Task> Saving;

        public Func<string> TempPackagePathRequest;

        public Exception ExceptionThrown { get; set; }

        public PackageManagerViewModelPackage()
            : base("DtronixPackage.Tests", new Version(1,0,0,0), false, true)
        {
            Logger = new NLogLogger(nameof(PackageManagerViewModelPackage));
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
                ExceptionThrown = ex;
            }

            return false;
        }

        protected override async Task OnSave()
        {
            try
            {
                if (Saving != null)
                    await Saving(this);
            }
            catch (Exception ex)
            {
                ExceptionThrown = ex;
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
