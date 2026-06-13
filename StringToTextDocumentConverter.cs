using System;
using System.Globalization;
using Avalonia.Data.Converters;
using AvaloniaEdit.Document;

namespace MultiSych.Desktop.Converters;

public class StringToTextDocumentConverter : IValueConverter
{
    public static readonly StringToTextDocumentConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is string s ? new TextDocument(s) : null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}