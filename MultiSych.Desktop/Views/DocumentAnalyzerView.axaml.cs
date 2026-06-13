using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using MultiSych.Desktop.ViewModels;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;

namespace MultiSych.Desktop.Views;

public partial class DocumentAnalyzerView : UserControl
{
    public DocumentAnalyzerView()
    {
        InitializeComponent();
    }

    private async void ExportToPdf_Click(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is Window window)
        {
            var file = await window.StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
            {
                Title = "PDF Olarak Kaydet",
                DefaultExtension = "pdf",
                SuggestedFileName = $"MultiSych_Ozet_{DateTime.Now:yyyyMMdd_HHmm}.pdf"
            });

            if (file != null)
            {
                try
                {
                    var text = (DataContext as DocumentAnalyzerViewModel)?.SummaryResult ?? "Özet metni bulunamadı.";
                    var password = (DataContext as DocumentAnalyzerViewModel)?.ExportPassword;
                    
                    iText.Kernel.Pdf.WriterProperties props = new iText.Kernel.Pdf.WriterProperties();
                    if (!string.IsNullOrEmpty(password))
                    {
                        var passBytes = System.Text.Encoding.UTF8.GetBytes(password);
                        props.SetStandardEncryption(passBytes, passBytes, iText.Kernel.Pdf.EncryptionConstants.ALLOW_PRINTING, iText.Kernel.Pdf.EncryptionConstants.ENCRYPTION_AES_256);
                    }
                    
                    using var writer = new iText.Kernel.Pdf.PdfWriter(file.Path.LocalPath, props);
                    using var pdf = new iText.Kernel.Pdf.PdfDocument(writer);
                    using var document = new iText.Layout.Document(pdf);
                    
                    document.Add(new iText.Layout.Element.Paragraph("MultiSych AI Belge Özeti").SetFontSize(16).SetBold());
                    document.Add(new iText.Layout.Element.Paragraph(text));
                }
                catch (Exception ex)
                {
                    await File.WriteAllTextAsync(file.Path.LocalPath + ".error.txt", $"PDF oluşturulurken hata: {ex.Message}");
                }
            }
        }
    }

    private async void ExportToWord_Click(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is Window window)
        {
            var file = await window.StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
            {
                Title = "Word Olarak Kaydet",
                DefaultExtension = "docx",
                SuggestedFileName = $"MultiSych_Ozet_{DateTime.Now:yyyyMMdd_HHmm}.docx"
            });

            if (file != null)
            {
                try
                {
                    var text = (DataContext as DocumentAnalyzerViewModel)?.SummaryResult ?? "Özet metni bulunamadı.";
                    using var wordDocument = WordprocessingDocument.Create(file.Path.LocalPath, WordprocessingDocumentType.Document);
                    var mainPart = wordDocument.AddMainDocumentPart();
                    mainPart.Document = new DocumentFormat.OpenXml.Wordprocessing.Document();
                    var body = mainPart.Document.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Body());

                    var password = (DataContext as DocumentAnalyzerViewModel)?.ExportPassword;
                    if (!string.IsNullOrEmpty(password))
                    {
                        var settingsPart = mainPart.AddNewPart<DocumentSettingsPart>();
                        settingsPart.Settings = new DocumentFormat.OpenXml.Wordprocessing.Settings();
                        settingsPart.Settings.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.DocumentProtection
                        {
                            Edit = DocumentFormat.OpenXml.Wordprocessing.DocumentProtectionValues.ReadOnly,
                            Enforcement = DocumentFormat.OpenXml.Wordprocessing.OnOffValue.FromBoolean(true)
                        });
                    }

                    var titlePara = body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Paragraph());
                    var titleRun = titlePara.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Run());
                    titleRun.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Text("MultiSych AI Belge Özeti"));

                    foreach (var line in text.Split('\n'))
                    {
                        var p = body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Paragraph());
                        p.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Run()).AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Text(line.TrimEnd('\r')));
                    }
                }
                catch (Exception ex)
                {
                    await File.WriteAllTextAsync(file.Path.LocalPath + ".error.txt", $"Word dosyası oluşturulurken hata: {ex.Message}");
                }
            }
        }
    }
}
