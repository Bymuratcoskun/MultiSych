using System;
using System.Globalization;
using Avalonia.Data.Converters;
using AvaloniaEdit.Highlighting;

namespace MultiSych.Desktop.Converters;

public class LanguageToSyntaxHighlightingConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string language && !string.IsNullOrWhiteSpace(language))
        {
            var def = HighlightingManager.Instance.GetDefinition(language);
            if (def != null)
            {
                return def;
            }

            // Yaygın diller için manuel eşleştirme / fallback (varsayılan)
            return language.ToLowerInvariant() switch
            {
                "cs" or "csharp" or "c#" => HighlightingManager.Instance.GetDefinition("C#"),
                "js" or "javascript" => HighlightingManager.Instance.GetDefinition("JavaScript"),
                "html" => HighlightingManager.Instance.GetDefinition("HTML"),
                "xml" => HighlightingManager.Instance.GetDefinition("XML"),
                "json" => HighlightingManager.Instance.GetDefinition("JavaScript"),
                "sql" => HighlightingManager.Instance.GetDefinition("TSQL"),
                "py" or "python" => HighlightingManager.Instance.GetDefinition("Python"),
                _ => null
            };
        }
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}