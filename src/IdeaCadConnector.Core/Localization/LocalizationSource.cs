using System.ComponentModel;
using System.Globalization;

namespace IdeaCadConnector.Core.Localization
{
    public sealed class LocalizationSource : INotifyPropertyChanged
    {
        public static LocalizationSource Instance { get; } = new LocalizationSource();

        private LocalizationSource()
        {
        }

        public string this[string key] => TranslationResources.GetString(
            CultureInfo.CurrentUICulture.Name, key);

        public void RaiseAllChanged()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
