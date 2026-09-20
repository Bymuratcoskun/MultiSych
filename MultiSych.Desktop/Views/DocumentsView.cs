using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gtk;
using MultiSych.Desktop.ViewModels;
using MultiSych.Services.Data;
using MultiSych.Services.Models;

namespace MultiSych.Desktop.Views;

public class DocumentsView : Gtk.Box
{
    private DocumentsViewModel? _viewModel;
    private readonly List<CloudFileEntity> _renderedDocuments = new();
    private Gtk.DropDown? _accountDropDown;
    private Gtk.DropDown? _categoryDropDown;
    private Gtk.Entry? _searchEntry;
    private Gtk.ListBox? _documentsList;
    private Gtk.Label? _selectedFileLabel;
    private Gtk.Label? _summaryLabel;
    private Gtk.Label? _stateLabel;
    private Gtk.TextView? _editorView;
    private Gtk.Button? _saveButton;
    private Gtk.TextView? _chatView;
    private Gtk.Entry? _chatEntry;
    private Gtk.Button? _sendChatButton;
    private Gtk.Frame? _createFrame;
    private Gtk.Entry? _newFileNameEntry;
    private Gtk.DropDown? _newDocumentTypeDropDown;
    private Gtk.Button? _confirmCreateButton;
    private Gtk.Frame? _calendarFrame;
    private Gtk.ListBox? _calendarList;

    public DocumentsViewModel? DataContext
    {
        get => _viewModel;
        set
        {
            if (ReferenceEquals(_viewModel, value)) return;
            _viewModel = value;
            if (_viewModel != null) InitializeBindings();
        }
    }

    public DocumentsView() : base()
    {
        ((Gtk.Orientable)this).Orientation = Gtk.Orientation.Vertical;
        Spacing = 0;
        BuildUi();
    }

    private void BuildUi()
    {
        var content = Gtk.Box.New(Gtk.Orientation.Vertical, 12);
        content.SetMarginStart(15);
        content.SetMarginEnd(15);
        content.SetMarginTop(15);
        content.SetMarginBottom(15);

        content.Append(BuildHeader());
        content.Append(BuildFilters());

        var main = Gtk.Box.New(Gtk.Orientation.Horizontal, 12);
        main.SetVexpand(true);
        main.Append(BuildDocumentList());
        main.Append(BuildDetails());
        content.Append(main);

        _createFrame = Gtk.Frame.New("Yeni Belge");
        _createFrame.SetChild(BuildCreatePanel());
        _createFrame.SetVisible(false);
        content.Append(_createFrame);

        _calendarFrame = Gtk.Frame.New("Takvim Etkinliği Önerileri");
        _calendarFrame.SetChild(BuildCalendarPanel());
        _calendarFrame.SetVisible(false);
        content.Append(_calendarFrame);

        var scroll = Gtk.ScrolledWindow.New();
        scroll.SetChild(content);
        scroll.SetVexpand(true);
        scroll.SetHexpand(true);
        Append(scroll);
    }

    private Gtk.Widget BuildHeader()
    {
        var header = Gtk.Box.New(Gtk.Orientation.Horizontal, 10);
        var title = Gtk.Label.New("Belgeler");
        title.SetHalign(Gtk.Align.Start);
        title.SetHexpand(true);
        title.SetFontSize(24);
        title.SetFontWeight(Pango.Weight.Bold);
        header.Append(title);

        _stateLabel = Gtk.Label.New(string.Empty);
        _stateLabel.AddCssClass("dim-label");
        header.Append(_stateLabel);

        var createButton = Gtk.Button.NewWithLabel("Yeni Belge");
        createButton.OnClicked += (_, _) => Execute(_viewModel?.ShowCreatePanelCommand, null);
        header.Append(createButton);

        var refreshButton = Gtk.Button.NewWithLabel("Yenile 🔄");
        refreshButton.OnClicked += (_, _) => Execute(_viewModel?.RefreshCommand, null);
        header.Append(refreshButton);
        return header;
    }

    private Gtk.Widget BuildFilters()
    {
        var filters = Gtk.Box.New(Gtk.Orientation.Horizontal, 10);

        _accountDropDown = Gtk.DropDown.NewFromStrings(["Hesap yok"]);
        _accountDropDown.SetSizeRequest(240, -1);
        _accountDropDown.OnNotify += (_, args) =>
        {
            if (args.Pspec.GetName() != "selected" || _viewModel == null) return;
            var index = (int)_accountDropDown.GetSelected();
            if (index >= 0 && index < _viewModel.Accounts.Count)
                _viewModel.SelectedAccount = _viewModel.Accounts[index];
        };
        filters.Append(_accountDropDown);

        _categoryDropDown = Gtk.DropDown.NewFromStrings(["Tümü"]);
        _categoryDropDown.OnNotify += (_, args) =>
        {
            if (args.Pspec.GetName() != "selected" || _viewModel == null) return;
            var index = (int)_categoryDropDown.GetSelected();
            if (index >= 0 && index < _viewModel.Categories.Count)
                Execute(_viewModel.SelectCategoryCommand, _viewModel.Categories[index]);
        };
        filters.Append(_categoryDropDown);

        _searchEntry = Gtk.Entry.New();
        _searchEntry.SetPlaceholderText("Belgelerde ara…");
        _searchEntry.SetHexpand(true);
        _searchEntry.OnNotify += (_, args) =>
        {
            if (args.Pspec.GetName() == "text" && _viewModel != null)
                _viewModel.SearchQuery = _searchEntry.GetText();
        };
        filters.Append(_searchEntry);
        return filters;
    }

    private Gtk.Widget BuildDocumentList()
    {
        var column = Gtk.Box.New(Gtk.Orientation.Vertical, 8);
        column.SetSizeRequest(330, 420);
        var label = Gtk.Label.New("Dosya Listesi");
        label.SetHalign(Gtk.Align.Start);
        label.SetFontWeight(Pango.Weight.Bold);
        column.Append(label);

        _documentsList = Gtk.ListBox.New();
        _documentsList.SetSelectionMode(Gtk.SelectionMode.Single);
        _documentsList.OnRowSelected += (_, args) =>
        {
            var index = args.Row?.GetIndex() ?? -1;
            if (_viewModel != null && index >= 0 && index < _renderedDocuments.Count)
                _viewModel.SelectedFile = _renderedDocuments[index];
        };
        var scroll = Gtk.ScrolledWindow.New();
        scroll.SetChild(_documentsList);
        scroll.SetVexpand(true);
        column.Append(scroll);
        return column;
    }

    private Gtk.Widget BuildDetails()
    {
        var details = Gtk.Box.New(Gtk.Orientation.Vertical, 10);
        details.SetHexpand(true);
        details.SetVexpand(true);

        _selectedFileLabel = Gtk.Label.New("Bir belge seçin");
        _selectedFileLabel.SetHalign(Gtk.Align.Start);
        _selectedFileLabel.SetFontSize(18);
        _selectedFileLabel.SetFontWeight(Pango.Weight.Bold);
        details.Append(_selectedFileLabel);

        var actions = Gtk.Box.New(Gtk.Orientation.Horizontal, 6);
        AddFileAction(actions, "Web'de Aç", vm => vm.OpenInWebCommand);
        AddFileAction(actions, "İndir", vm => vm.DownloadFileCommand);
        AddFileAction(actions, "Sil", vm => vm.DeleteFileCommand, destructive: true);
        AddFileAction(actions, "Özetle", vm => vm.SummarizeDocumentCommand);
        AddFileAction(actions, "Yerelde Düzenle", vm => vm.EditLocallyCommand);
        AddFileAction(actions, "E-posta Taslağı", vm => vm.CreateEmailDraftCommand);
        AddFileAction(actions, "Etkinlikleri Çıkar", vm => vm.ExtractEventsCommand);
        details.Append(actions);

        _summaryLabel = Gtk.Label.New(string.Empty);
        _summaryLabel.SetHalign(Gtk.Align.Start);
        _summaryLabel.SetWrap(true);
        details.Append(CreateFrame("AI Özeti", _summaryLabel));

        _editorView = Gtk.TextView.New();
        _editorView.SetWrapMode(Gtk.WrapMode.Word);
        _editorView.SetSizeRequest(-1, 150);
        _editorView.Buffer!.OnNotify += (_, args) =>
        {
            if (args.Pspec.GetName() == "text" && _viewModel != null)
                _viewModel.EditableDocumentText = _editorView.Buffer!.Text ?? string.Empty;
        };
        var editorBox = Gtk.Box.New(Gtk.Orientation.Vertical, 6);
        var editorScroll = Gtk.ScrolledWindow.New();
        editorScroll.SetChild(_editorView);
        editorBox.Append(editorScroll);
        var editorButtons = Gtk.Box.New(Gtk.Orientation.Horizontal, 6);
        _saveButton = Gtk.Button.NewWithLabel("Metni Kaydet");
        _saveButton.OnClicked += (_, _) => Execute(_viewModel?.SaveDocumentTextCommand, null);
        editorButtons.Append(_saveButton);
        var cancelEditButton = Gtk.Button.NewWithLabel("Değişiklikleri Geri Al");
        cancelEditButton.OnClicked += (_, _) => Execute(_viewModel?.CancelEditCommand, null);
        editorButtons.Append(cancelEditButton);
        editorBox.Append(editorButtons);
        details.Append(CreateFrame("Metin Düzenleyici", editorBox));

        _chatView = Gtk.TextView.New();
        _chatView.SetEditable(false);
        _chatView.SetWrapMode(Gtk.WrapMode.Word);
        _chatView.SetSizeRequest(-1, 150);
        var chatBox = Gtk.Box.New(Gtk.Orientation.Vertical, 6);
        var chatScroll = Gtk.ScrolledWindow.New();
        chatScroll.SetChild(_chatView);
        chatBox.Append(chatScroll);
        var chatControls = Gtk.Box.New(Gtk.Orientation.Horizontal, 6);
        _chatEntry = Gtk.Entry.New();
        _chatEntry.SetPlaceholderText("Belge hakkında soru sorun…");
        _chatEntry.SetHexpand(true);
        _chatEntry.OnNotify += (_, args) =>
        {
            if (args.Pspec.GetName() == "text" && _viewModel != null)
                _viewModel.ChatInputText = _chatEntry.GetText();
        };
        _chatEntry.OnActivate += (_, _) => SendChat();
        chatControls.Append(_chatEntry);
        _sendChatButton = Gtk.Button.NewWithLabel("Gönder");
        _sendChatButton.OnClicked += (_, _) => SendChat();
        chatControls.Append(_sendChatButton);
        var clearChatButton = Gtk.Button.NewWithLabel("Sohbeti Temizle");
        clearChatButton.OnClicked += (_, _) => Execute(_viewModel?.ClearChatCommand, null);
        chatControls.Append(clearChatButton);
        chatBox.Append(chatControls);
        details.Append(CreateFrame("Belgeyle Sohbet", chatBox));

        return details;
    }

    private Gtk.Widget BuildCreatePanel()
    {
        var panel = Gtk.Box.New(Gtk.Orientation.Horizontal, 8);
        panel.SetMarginStart(10);
        panel.SetMarginEnd(10);
        panel.SetMarginTop(10);
        panel.SetMarginBottom(10);
        _newFileNameEntry = Gtk.Entry.New();
        _newFileNameEntry.SetPlaceholderText("Dosya adı");
        _newFileNameEntry.SetHexpand(true);
        _newFileNameEntry.OnNotify += (_, args) =>
        {
            if (args.Pspec.GetName() == "text" && _viewModel != null)
                _viewModel.NewFileName = _newFileNameEntry.GetText();
        };
        panel.Append(_newFileNameEntry);

        _newDocumentTypeDropDown = Gtk.DropDown.NewFromStrings(["Düz Metin (.txt)"]);
        _newDocumentTypeDropDown.OnNotify += (_, args) =>
        {
            if (args.Pspec.GetName() != "selected" || _viewModel == null) return;
            var index = (int)_newDocumentTypeDropDown.GetSelected();
            if (index >= 0 && index < _viewModel.NewDocumentTypes.Count)
                _viewModel.SelectedNewDocumentType = _viewModel.NewDocumentTypes[index];
        };
        panel.Append(_newDocumentTypeDropDown);

        _confirmCreateButton = Gtk.Button.NewWithLabel("Oluştur");
        _confirmCreateButton.AddCssClass("suggested-action");
        _confirmCreateButton.OnClicked += (_, _) => Execute(_viewModel?.ConfirmCreateCommand, null);
        panel.Append(_confirmCreateButton);
        var cancelButton = Gtk.Button.NewWithLabel("İptal");
        cancelButton.OnClicked += (_, _) => Execute(_viewModel?.CancelCreateCommand, null);
        panel.Append(cancelButton);
        return panel;
    }

    private Gtk.Widget BuildCalendarPanel()
    {
        var panel = Gtk.Box.New(Gtk.Orientation.Vertical, 8);
        panel.SetMarginStart(10);
        panel.SetMarginEnd(10);
        panel.SetMarginTop(10);
        panel.SetMarginBottom(10);
        _calendarList = Gtk.ListBox.New();
        _calendarList.SetSelectionMode(Gtk.SelectionMode.None);
        panel.Append(_calendarList);
        var closeButton = Gtk.Button.NewWithLabel("Önerileri Kapat");
        closeButton.SetHalign(Gtk.Align.End);
        closeButton.OnClicked += (_, _) => Execute(_viewModel?.CloseCalendarSuggestionsCommand, null);
        panel.Append(closeButton);
        return panel;
    }

    private void InitializeBindings()
    {
        if (_viewModel == null) return;

        _viewModel.PropertyChanged += (_, _) =>
            GLib.Functions.IdleAdd(0, () => { UpdateValues(); return false; });
        _viewModel.Accounts.CollectionChanged += (_, _) =>
            GLib.Functions.IdleAdd(0, () => { PopulateAccounts(); return false; });
        _viewModel.Documents.CollectionChanged += (_, _) =>
            GLib.Functions.IdleAdd(0, () => { PopulateDocuments(); return false; });
        _viewModel.ChatMessages.CollectionChanged += (_, _) =>
            GLib.Functions.IdleAdd(0, () => { PopulateChat(); return false; });
        _viewModel.CalendarSuggestions.CollectionChanged += (_, _) =>
            GLib.Functions.IdleAdd(0, () => { PopulateCalendarSuggestions(); return false; });

        _categoryDropDown?.SetModel(Gtk.StringList.New(_viewModel.Categories.ToArray()));
        _newDocumentTypeDropDown?.SetModel(Gtk.StringList.New(_viewModel.NewDocumentTypes.ToArray()));
        PopulateAccounts();
        PopulateDocuments();
        PopulateChat();
        PopulateCalendarSuggestions();
        UpdateValues();
    }

    private void PopulateAccounts()
    {
        if (_viewModel == null || _accountDropDown == null) return;
        var names = _viewModel.Accounts.Select(a => $"{a.Provider}: {a.Email}").ToArray();
        _accountDropDown.SetModel(Gtk.StringList.New(names.Length == 0 ? ["Hesap yok"] : names));
    }

    private void PopulateDocuments()
    {
        if (_viewModel == null || _documentsList == null) return;
        ClearList(_documentsList);
        _renderedDocuments.Clear();
        foreach (var document in _viewModel.Documents)
        {
            _renderedDocuments.Add(document);
            var row = Gtk.ListBoxRow.New();
            var box = Gtk.Box.New(Gtk.Orientation.Vertical, 2);
            box.SetMarginStart(8);
            box.SetMarginEnd(8);
            box.SetMarginTop(6);
            box.SetMarginBottom(6);
            var name = Gtk.Label.New(document.FileName);
            name.SetHalign(Gtk.Align.Start);
            name.SetEllipsize(Pango.EllipsizeMode.End);
            box.Append(name);
            var meta = Gtk.Label.New($"{document.Provider} · {document.FileSize / 1024d:0.#} KB");
            meta.SetHalign(Gtk.Align.Start);
            meta.AddCssClass("dim-label");
            box.Append(meta);
            row.SetChild(box);
            _documentsList.Append(row);
        }
    }

    private void PopulateChat()
    {
        if (_viewModel == null || _chatView?.Buffer == null) return;
        var text = new StringBuilder();
        foreach (var message in _viewModel.ChatMessages)
            text.Append(message.SenderName).Append(" · ").Append(message.Time).Append("\n").Append(message.Text).Append("\n\n");
        _chatView.Buffer.Text = text.ToString();
    }

    private void PopulateCalendarSuggestions()
    {
        if (_viewModel == null || _calendarList == null) return;
        ClearList(_calendarList);
        foreach (var suggestion in _viewModel.CalendarSuggestions)
        {
            var row = Gtk.ListBoxRow.New();
            var box = Gtk.Box.New(Gtk.Orientation.Horizontal, 8);
            box.SetMarginStart(8);
            box.SetMarginEnd(8);
            box.SetMarginTop(6);
            box.SetMarginBottom(6);
            var label = Gtk.Label.New($"{suggestion.Title} · {suggestion.StartTime:dd.MM.yyyy HH:mm}");
            label.SetHalign(Gtk.Align.Start);
            label.SetHexpand(true);
            box.Append(label);
            var addButton = Gtk.Button.NewWithLabel("Takvime Ekle");
            addButton.OnClicked += (_, _) => Execute(_viewModel.AddEventSuggestionCommand, suggestion);
            box.Append(addButton);
            row.SetChild(box);
            _calendarList.Append(row);
        }
    }

    private void UpdateValues()
    {
        if (_viewModel == null) return;
        var selected = _viewModel.SelectedFile;
        _selectedFileLabel?.SetText(selected?.FileName ?? "Bir belge seçin");
        _summaryLabel?.SetText(_viewModel.SelectedFileSummary ?? string.Empty);
        if (_searchEntry != null && _searchEntry.GetText() != _viewModel.SearchQuery)
            _searchEntry.SetText(_viewModel.SearchQuery);
        if (_editorView?.Buffer != null && _editorView.Buffer.Text != _viewModel.EditableDocumentText)
            _editorView.Buffer.Text = _viewModel.EditableDocumentText;
        _editorView?.SetSensitive(_viewModel.IsSelectedFileEditableText && !_viewModel.IsEditing);
        _saveButton?.SetSensitive(_viewModel.SaveDocumentTextCommand.CanExecute(null));
        if (_chatEntry != null && _chatEntry.GetText() != _viewModel.ChatInputText)
            _chatEntry.SetText(_viewModel.ChatInputText);
        _sendChatButton?.SetSensitive(_viewModel.SendChatMessageCommand.CanExecute(null));
        _createFrame?.SetVisible(_viewModel.IsCreatePanelVisible);
        if (_newFileNameEntry != null && _newFileNameEntry.GetText() != _viewModel.NewFileName)
            _newFileNameEntry.SetText(_viewModel.NewFileName);
        _confirmCreateButton?.SetSensitive(_viewModel.ConfirmCreateCommand.CanExecute(null));
        _calendarFrame?.SetVisible(_viewModel.IsCalendarSuggestionsPanelVisible);

        var states = new List<string>();
        if (_viewModel.IsLoading) states.Add("Yükleniyor");
        if (_viewModel.IsSummarizing) states.Add("Özetleniyor");
        if (_viewModel.IsChatBusy) states.Add("AI yanıtlıyor");
        if (_viewModel.IsExtractingEvents) states.Add("Etkinlikler çıkarılıyor");
        if (_viewModel.IsEditing) states.Add("Belge işleniyor");
        if (_viewModel.HasUnsavedChanges) states.Add("Kaydedilmemiş değişiklik var");
        _stateLabel?.SetText(states.Count == 0 ? "Hazır" : string.Join(" · ", states));
    }

    private void AddFileAction(Gtk.Box parent, string label, Func<DocumentsViewModel, System.Windows.Input.ICommand> command, bool destructive = false)
    {
        var button = Gtk.Button.NewWithLabel(label);
        if (destructive) button.AddCssClass("destructive-action");
        button.OnClicked += (_, _) =>
        {
            if (_viewModel != null) Execute(command(_viewModel), _viewModel.SelectedFile);
        };
        parent.Append(button);
    }

    private void SendChat()
    {
        if (_viewModel == null || _chatEntry == null) return;
        _viewModel.ChatInputText = _chatEntry.GetText();
        Execute(_viewModel.SendChatMessageCommand, null);
    }

    private static void Execute(System.Windows.Input.ICommand? command, object? parameter)
    {
        if (command?.CanExecute(parameter) == true) command.Execute(parameter);
    }

    private static Gtk.Widget CreateFrame(string title, Gtk.Widget child)
    {
        var frame = Gtk.Frame.New(title);
        frame.SetChild(child);
        return frame;
    }

    private static void ClearList(Gtk.ListBox list)
    {
        var child = list.GetFirstChild();
        while (child != null)
        {
            list.Remove(child);
            child = list.GetFirstChild();
        }
    }
}
