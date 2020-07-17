using System;

namespace DtronixPackage
{
    public class FileEventArgs<T> : EventArgs
    {
        public T File { get; set; }

        public FileEventArgs(T file)
        {
            File = file;
        }
    }
}