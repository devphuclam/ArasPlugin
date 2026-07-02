using System;
using System.Globalization;
using IdeaCadConnector.Core.Localization;

namespace IdeaCadConnector.Desktop.Services
{
    public sealed class LocalizationService : ILocalizationService
    {
        private CultureInfo _culture;

        public LocalizationService()
        {
            var saved = SettingsService.LoadLanguage();
            _culture = !string.IsNullOrWhiteSpace(saved)
                ? new CultureInfo(saved)
                : CultureInfo.CurrentUICulture;
        }

        public CultureInfo CurrentCulture => _culture;

        public string GetString(string key)
        {
            return TranslationResources.GetString(_culture.Name, key);
        }

        public string GetString(string key, params object[] args)
        {
            var format = GetString(key);
            return string.Format(format, args);
        }

        public void SetCulture(string cultureName)
        {
            SetCulture(new CultureInfo(cultureName));
        }

        public void SetCulture(CultureInfo culture)
        {
            if (culture == null)
                throw new ArgumentNullException(nameof(culture));

            if (string.Equals(_culture.Name, culture.Name, StringComparison.OrdinalIgnoreCase))
                return;

            _culture = culture;
            SettingsService.SaveLanguage(culture.Name);
            CultureChanged?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler CultureChanged;
    }
}
