using System;
using System.Globalization;
using Avalonia.Data.Converters;
using AvaloniaEdit.Highlighting;

namespace MultiSych.Desktop.Converters;

public class LanguageToSyntaxHighlightingConverter : IValueConverter
{
    public static readonly LanguageToSyntaxHighlightingConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var language = (value as string)?.ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(language)) return null;

        var highlightingManager = HighlightingManager.Instance;
        IHighlightingDefinition? definition;

        switch (language)
        {
            case "csharp":
            case "cs":
                definition = highlightingManager.GetDefinition("C#");
                break;
            case "xml":
            case "axaml":
            case "xaml":
            case "csproj":
                definition = highlightingManager.GetDefinition("XML");
                break;
            case "json":
                definition = highlightingManager.GetDefinition("Json");
                break;
            case "js":
            case "javascript":
                definition = highlightingManager.GetDefinition("JavaScript");
                break;
            default:
                definition = highlightingManager.GetDefinition(language);
                break;
        }
        return definition;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}