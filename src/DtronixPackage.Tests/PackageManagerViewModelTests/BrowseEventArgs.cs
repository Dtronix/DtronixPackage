using System;
using System.Collections.Generic;
using System.Text;

namespace DtronixPackage.Tests.PackageManagerViewModelTests
{
    public class BrowseEventArgs : EventArgs
    {
        public string Path { get; set; }
        public bool ReadOnly { get; set; }
        public bool Result { get; set; }
    }
}
