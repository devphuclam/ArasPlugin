using System;
using System.Globalization;

namespace IdeaCadConnector.Core.Localization
{
    public interface ILocalizationService
    {
        string GetString(string key);
        string GetString(string key, params object[] args);
        CultureInfo CurrentCulture { get; }
        void SetCulture(string cultureName);
        void SetCulture(CultureInfo culture);
        event EventHandler CultureChanged;
    }
}
