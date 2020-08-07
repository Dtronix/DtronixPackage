using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using NLog;
using NUnit.Framework;

namespace DtronixPackage.Tests
{
    public static class Utilities
    {
        public static async Task AssertFileExistWithin(string path, int timeout = 5000)
        {
            Assert.IsTrue(await FileExistWithin(path, timeout), $"File {path} does not exist.");
        }

        public static async Task AssertFileDoesNotExistWithin(string path, int timeout = 100)
        {
            Assert.IsFalse(await FileExistWithin(path, timeout), $"File {path} exists.");
        }

        public static async Task<bool> FileExistWithin(string path, int timeout)
        {
            // Fast path.
            if (File.Exists(path))
                return true;
            
            // Slow Path.
            var sw = new Stopwatch();
            sw.Start();
            while (sw.ElapsedMilliseconds < timeout)
            {
                if (File.Exists(path))
                    return true;

                await Task.Delay(10);
            }

            return false;
        }

        
        public static void AssertFileIsReadOnly(string path)
        {
            Assert.Multiple(() =>
            {
                Assert.Throws<IOException>(() => new FileStream(path, FileMode.Create));
                Assert.Throws<IOException>(() => File.Delete(path));
                Assert.DoesNotThrow(() => new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite).Close());
            });
        }
    }
}
