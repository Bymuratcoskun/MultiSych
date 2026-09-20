using System;
using System.ComponentModel;
using System.Text;
using Gtk;
using MultiSych.Desktop.ViewModels;

namespace MultiSych.Desktop.Views;

/// <summary>
/// Ana pencere içi sohbet ekranı. ChatViewModel'e bağlıdır ve "🗂️ Verilerim"
/// (RAG / Verilerinle Sohbet) modunu içerir. Segment tabanlı daktilo efektini
/// yansıtmak için mesaj/segment değişikliklerinde tüm sohbeti yeniden çizer.
/// </summary>
public class ChatView : Gtk.Box
{
    private ChatViewModel? _viewModel;
    private Gtk.TextView? _chatView;
    private Gtk.Entry? _entry;
    private Gtk.Button? _dataModeButton;
    private bool _renderQueued;

    public ChatViewModel? DataContext
    {
        get => _viewModel;
        set
        {
            if (ReferenceEquals(_viewModel, value)) return;
            _viewModel = value;
            if (_viewModel != null) InitializeBindings();
        }
    }

    public ChatView() : base()
    {
        ((Gtk.Orientable)this).Orientation = Gtk.Orientation.Vertical;
        this.Spacing = 10;
        SetMarginStart(15);
        SetMarginEnd(15);
        SetMarginTop(15);
        SetMarginBottom(15);
        BuildUi();
    }

    private void BuildUi()
    {
        var header = Gtk.Box.New(Gtk.Orientation.Horizontal, 10);
        var title = Gtk.Label.New("💬 Sohbet");
        title.SetFontSize(20);
        title.SetFontWeight(Pango.Weight.Bold);
        title.SetHalign(Gtk.Align.Start);
        title.SetHexpand(true);
        header.Append(title);

        _dataModeButton = Gtk.Button.NewWithLabel("🗂️ Verilerim: Kapalı");
        _dataModeButton.SetTooltipText("Açıkken sorular e-posta/dosya/takvim verilerinizde aranır (RAG).");
        _dataModeButton.OnClicked += (_, _) =>
        {
            if (_viewModel != null) _viewModel.IsDataAwareMode = !_viewModel.IsDataAwareMode;
        };
        header.Append(_dataModeButton);
        Append(header);

        _chatView = Gtk.TextView.New();
        _chatView.SetEditable(false);
        _chatView.SetWrapMode(Gtk.WrapMode.Word);
        var scroll = Gtk.ScrolledWindow.New();
        scroll.SetChild(_chatView);
        scroll.SetVexpand(true);
        Append(scroll);

        var inputRow = Gtk.Box.New(Gtk.Orientation.Horizontal, 8);
        _entry = Gtk.Entry.New();
        _entry.SetHexpand(true);
        _entry.SetPlaceholderText("Mesajınızı yazın…");
        _entry.OnNotify += (_, args) =>
        {
            if (args.Pspec.GetName() == "text" && _viewModel != null)
                _viewModel.InputText = _entry.GetText();
        };
        _entry.OnActivate += (_, _) => Send();
        inputRow.Append(_entry);

        var btnRecord = Gtk.Button.NewWithLabel("🎤");
        btnRecord.SetTooltipText("Sesli mesaj (dikte)");
        btnRecord.OnClicked += (_, _) => _viewModel?.ToggleRecordingCommand.Execute(null);
        inputRow.Append(btnRecord);

        var btnSend = Gtk.Button.NewWithLabel("Gönder");
        btnSend.AddCssClass("suggested-action");
        btnSend.OnClicked += (_, _) => Send();
        inputRow.Append(btnSend);

        Append(inputRow);
    }

    private void Send()
    {
        if (_viewModel == null || _entry == null) return;
        _viewModel.InputText = _entry.GetText();
        if (_viewModel.SendCommand.CanExecute(null))
        {
            _viewModel.SendCommand.Execute(null);
            _entry.SetText(string.Empty);
        }
    }

    private void InitializeBindings()
    {
        if (_viewModel == null) return;

        _viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ChatViewModel.DataModeButtonText))
                GLib.Functions.IdleAdd(0, () => { _dataModeButton?.SetLabel(_viewModel!.DataModeButtonText); return false; });
        };

        _viewModel.Messages.CollectionChanged += (_, e) =>
        {
            if (e.NewItems != null)
            {
                foreach (ChatUIMessage msg in e.NewItems)
                    HookMessage(msg);
            }
            QueueRender();
        };

        foreach (var msg in _viewModel.Messages)
            HookMessage(msg);

        QueueRender();
    }

    private void HookMessage(ChatUIMessage msg)
    {
        // Yeni segmentler (daktilo canlı yazımı) ve segment metin değişikliklerini dinle.
        msg.Segments.CollectionChanged += (_, e) =>
        {
            if (e.NewItems != null)
            {
                foreach (MessageSegment seg in e.NewItems)
                    seg.PropertyChanged += OnSegmentChanged;
            }
            QueueRender();
        };
        foreach (var seg in msg.Segments)
            seg.PropertyChanged += OnSegmentChanged;
    }

    private void OnSegmentChanged(object? sender, PropertyChangedEventArgs e) => QueueRender();

    private void QueueRender()
    {
        if (_renderQueued) return;
        _renderQueued = true;
        GLib.Functions.IdleAdd(0, () =>
        {
            _renderQueued = false;
            RenderAll();
            return false;
        });
    }

    private void RenderAll()
    {
        if (_viewModel == null || _chatView?.Buffer == null) return;

        var sb = new StringBuilder();
        foreach (var msg in _viewModel.Messages)
        {
            var sender = msg.IsUser ? "Siz" : "Asistan";
            sb.Append(sender).Append(": ");
            foreach (var seg in msg.Segments)
            {
                sb.Append(seg switch
                {
                    TextSegment t => t.Text,
                    CodeSegment c => "\n" + c.Code + "\n",
                    _ => string.Empty
                });
            }
            sb.Append("\n\n");
        }
        _chatView.Buffer.Text = sb.ToString();
    }
}
