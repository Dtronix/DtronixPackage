using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DtronixPackage.Tests
{
    public class PackageDataContractChild : INotifyPropertyChanged
    {
        private int _integer;
        private string _string;
        private ObservableCollection<PackageDataContractChild> _children
            = new ObservableCollection<PackageDataContractChild>();

        public int Integer
        {
            get => _integer;
            set
            {
                _integer = value; 
                OnPropertyChanged();
            }
        }

        public string String
        {
            get => _string;
            set
            {
                _string = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<PackageDataContractChild> Children
        {
            get => _children;
            set
            {
                _children = value; 
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}