using System;
using System.Collections.Specialized;
using Gtk;
using MultiSych.Desktop.ViewModels;

namespace MultiSych.Desktop.Views;

public class AIChatWindow : Gtk.Window
{
    private readonly AIChatViewModel _viewModel;
    private Gtk.TextView? _chatTextView;
    private Gtk.Button? _dataModeButton;

    public AIChatWindow(Gtk.Window parent, AIChatViewModel viewModel)
    {
        _viewModel = viewModel;

        SetTitle($"AI Chat ({viewModel.Provider})");
        SetDefaultSize(500, 600);
        SetTransientFor(parent);

        BuildUi();

        // ViewModel mesaj koleksiyonunu dinleyip ekranı güncelle.
        _viewModel.ChatMessages.CollectionChanged += OnMessagesChanged;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void BuildUi()
    {
        var box = Gtk.Box.New(Gtk.Orientation.Vertical, 10);
        box.SetMarginStart(15);
        box.SetMarginEnd(15);
        box.SetMarginTop(15);
        box.SetMarginBottom(15);

        // Başlık satırı + "Verilerinle Sohbet" toggle butonu
        var headerBox = Gtk.Box.New(Gtk.Orientation.Horizontal, 10);
        var titleLabel = Gtk.Label.New($"MultiSych AI Assistant - {_viewModel.Provider}");
        titleLabel.SetFontSize(14);
        titleLabel.SetFontWeight(Pango.Weight.Bold);
        titleLabel.SetHalign(Gtk.Align.Start);
        titleLabel.SetHexpand(true);
        headerBox.Append(titleLabel);

        _dataModeButton = Gtk.Button.NewWithLabel(_viewModel.DataModeButtonText);
        _dataModeButton.SetTooltipText("Açıkken sorular e-posta/dosya/takvim verilerinizde aranır (RAG).");
        _dataModeButton.OnClicked += (_, _) => _viewModel.IsDataAwareMode = !_viewModel.IsDataAwareMode;
        headerBox.Append(_dataModeButton);
        box.Append(headerBox);

        _chatTextView = Gtk.TextView.New();
        _chatTextView.SetEditable(false);
        _chatTextView.SetWrapMode(Gtk.WrapMode.Word);

        var scroll = Gtk.ScrolledWindow.New();
        scroll.SetChild(_chatTextView);
        scroll.SetVexpand(true);
        box.Append(scroll);

        var entryBox = Gtk.Box.New(Gtk.Orientation.Horizontal, 10);
        var entry = Gtk.Entry.New();
        entry.SetHexpand(true);
        entry.SetPlaceholderText("Mesajınızı yazın…");

        void Send()
        {
            var text = entry.GetText();
            if (string.IsNullOrWhiteSpace(text)) return;
            _viewModel.CurrentMessage = text;
            entry.SetText(string.Empty);
            if (_viewModel.SendMessageCommand.CanExecute(null))
                _viewModel.SendMessageCommand.Execute(null);
        }

        entry.OnActivate += (_, _) => Send();

        var btnSend = Gtk.Button.NewWithLabel("Gönder");
        btnSend.OnClicked += (_, _) => Send();

        entryBox.Append(entry);
        entryBox.Append(btnSend);
        box.Append(entryBox);

        SetChild(box);
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AIChatViewModel.DataModeButtonText))
        {
            GLib.Functions.IdleAdd(0, () =>
            {
                _dataModeButton?.SetLabel(_viewModel.DataModeButtonText);
                return false;
            });
        }
    }

    private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add || e.NewItems == null) return;

        // ViewModel arka plan iş parçacığından mesaj ekleyebilir; UI güncellemesini
        // GTK ana döngüsüne marshalling yaparak yapıyoruz.
        foreach (ChatMessage msg in e.NewItems)
        {
            var line = $"{msg.Sender}: {msg.Content}\n\n";
            GLib.Functions.IdleAdd(0, () =>
            {
                if (_chatTextView?.Buffer != null)
                    _chatTextView.Buffer.Text += line;
                return false;
            });
        }
    }
}
