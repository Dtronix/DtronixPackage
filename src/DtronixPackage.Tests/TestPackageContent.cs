namespace DtronixPackage.Tests
{
    public class TestPackageContent : PackageContent {
        private string _mainText;

        public string MainText
        {
            get => _mainText;
            set
            {
                _mainText = value;
                OnPropertyChanged();
            }
        }

        protected internal override void Clear(IPackage package)
        {
            _mainText = null;
        }
    }
}