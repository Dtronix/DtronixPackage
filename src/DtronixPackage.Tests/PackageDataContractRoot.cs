using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DtronixPackage.Tests
{
    class PackageDataContractRoot : INotifyPropertyChanged
    {
        private ObservableCollection<PackageDataContractChild> _children
            = new ObservableCollection<PackageDataContractChild>();

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