using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DtronixPackage.Tests
{
    public class SimplePackageContent : PackageContent {
        public class SubType : INotifyPropertyChanged
        {
            
            public event PropertyChangedEventHandler PropertyChanged;

            protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }

            private string _value;

            public string Value
            {
                get => _value;
                set
                {
                    _value = value;
                    OnPropertyChanged();
                }
            }

        }
        private int _integer;
        private double _d;
        private string _s;
        private byte _b;
        private byte[] _bytes;
        private DateTimeOffset _dateTimeOffset;
        private SubType _subTypeInstance;

        public int Integer
        {
            get => _integer;
            set
            {
                _integer = value;
                OnPropertyChanged();
            }
        }

        public double Double
        {
            get => _d;
            set
            {
                _d = value;
                OnPropertyChanged();
            }
        }

        public string String
        {
            get => _s;
            set
            {
                _s = value;
                OnPropertyChanged();
            }
        }

        public byte Byte
        {
            get => _b;
            set
            {
                _b = value; 
                OnPropertyChanged();
            }
        }

        public byte[] Bytes
        {
            get => _bytes;
            set
            {
                _bytes = value; 
                OnPropertyChanged();
            }
        }

        public DateTimeOffset DateTimeOffset
        {
            get => _dateTimeOffset;
            set
            {
                _dateTimeOffset = value; 
                OnPropertyChanged();
            }
        }

        public SubType SubTypeInstance
        {
            get => _subTypeInstance;
            set
            {
                _subTypeInstance = value;
                OnPropertyChanged();
            }
        }

        protected internal override void Clear(IPackage package)
        {
            Integer = default;
            Double = default;
            String = default;
            Byte = default;
            Bytes = default;
            DateTimeOffset = default;
            SubTypeInstance = default;
        }
    }
}