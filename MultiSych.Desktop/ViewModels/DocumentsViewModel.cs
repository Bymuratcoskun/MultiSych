using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using Microsoft.EntityFrameworkCore;
using MultiSych.Desktop.Services;
using MultiSych.Services.Data;
using MultiSych.Services.Interfaces;
using MultiSych.Services.Models;
using Serilog;
using IWindowService = MultiSych.Desktop.Services.IWindowService;
using MultiSych.Services.Configuration;

namespace MultiSych.Desktop.ViewModels
{
    public class DocumentChatMessage : ViewModelBase
    {
        private string _text = string.Empty;
        private bool _isUser;

        public string Text
        {
            get => _text;
            set => SetProperty(ref _text, value);
        }

        public bool IsUser
        {
            get => _isUser;
            set
            {
                if (SetProperty(ref _isUser, value))
                {
                    OnPropertyChanged(nameof(SenderName));
                }
            }
        }

        public string SenderName => IsUser ? "Siz" : "Yapay Zeka";
        private string _time = DateTime.Now.ToString("HH:mm");
        public string Time
        {
            get => _time;
            set => SetProperty(ref _time, value);
        }
    }

    public class DocumentsViewModel : ViewModelBase
    {
        private readonly IAccountStore _accountStore;
        private readonly IStorageService _storageService;
        private readonly IAIService _aiService;
        private readonly IWindowService _windowService;
        private readonly IAppStatusService _appStatusService;
        private readonly IDbContextFactory<LocalCacheDbContext> _dbContextFactory;
        private readonly MultiSychConfig _config;
        private readonly ILogger _logger;
        private readonly ICalendarService _calendarService;

        private AccountCredentials? _selectedAccount;
        private CloudFileEntity? _selectedFile;
        private string _searchQuery = string.Empty;
        private bool _isLoading;
        private string _selectedCategory = "Tümü";
        private string? _selectedFileSummary;
        private bool _isSummarizing;

        private string _chatInputText = string.Empty;
        private bool _isChatBusy;
        private string? _cachedDocumentText;
        private string? _cachedDocumentTextFileId;

        private bool _isCreatePanelVisible;
        private string _newFileName = "Yeni Döküman";
        private string _selectedNewDocumentType = "Word Belgesi (.docx)";

        private bool _isExtractingEvents;
        private bool _isCalendarSuggestionsPanelVisible;
        private ObservableCollection<CalendarEvent> _calendarSuggestions = new();

        private string _editableDocumentText = string.Empty;
        private bool _isEditing;
        private bool _hasUnsavedChanges;

        public ObservableCollection<AccountCredentials> Accounts { get; } = new();
        public ObservableCollection<CloudFileEntity> Documents { get; } = new();
        public ObservableCollection<string> Categories { get; } = new() { "Tümü", "Word", "Excel", "PowerPoint", "PDF", "Metin", "Görsel", "Ses" };

        public bool IsExtractingEvents
        {
            get => _isExtractingEvents;
            set => SetProperty(ref _isExtractingEvents, value);
        }

        public bool IsCalendarSuggestionsPanelVisible
        {
            get => _isCalendarSuggestionsPanelVisible;
            set => SetProperty(ref _isCalendarSuggestionsPanelVisible, value);
        }

        public ObservableCollection<CalendarEvent> CalendarSuggestions
        {
            get => _calendarSuggestions;
            set => SetProperty(ref _calendarSuggestions, value);
        }

        public string EditableDocumentText
        {
            get => _editableDocumentText;
            set
            {
                if (SetProperty(ref _editableDocumentText, value))
                {
                    HasUnsavedChanges = _editableDocumentText != (_cachedDocumentText ?? string.Empty);
                }
            }
        }

        public bool IsEditing
        {
            get => _isEditing;
            set => SetProperty(ref _isEditing, value);
        }

        public bool HasUnsavedChanges
        {
            get => _hasUnsavedChanges;
            set => SetProperty(ref _hasUnsavedChanges, value);
        }

        public ICommand RefreshCommand { get; }
        public ICommand OpenInWebCommand { get; }
        public ICommand DownloadFileCommand { get; }
        public ICommand DeleteFileCommand { get; }
        public ICommand SummarizeDocumentCommand { get; }
        public ICommand SelectCategoryCommand { get; }
        public ICommand SendChatMessageCommand { get; }
        public ICommand ClearChatCommand { get; }
        public ICommand ShowCreatePanelCommand { get; }
        public ICommand CancelCreateCommand { get; }
        public ICommand ConfirmCreateCommand { get; }
        public ICommand EditLocallyCommand { get; }
        public ICommand CreateEmailDraftCommand { get; }
        public ICommand ExtractEventsCommand { get; }
        public ICommand AddEventSuggestionCommand { get; }
        public ICommand CloseCalendarSuggestionsCommand { get; }
        public ICommand SaveDocumentTextCommand { get; }
        public ICommand CancelEditCommand { get; }

        public AccountCredentials? SelectedAccount
        {
            get => _selectedAccount;
            set
            {
                if (SetProperty(ref _selectedAccount, value))
                {
                    SelectedFile = null;
                    _ = LoadDocumentsAsync();
                    if (value != null)
                    {
                        _ = SyncAndReloadAsync();
                    }
                    (ConfirmCreateCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public CloudFileEntity? SelectedFile
        {
            get => _selectedFile;
            set
            {
                if (SetProperty(ref _selectedFile, value))
                {
                    SelectedFileSummary = null;
                    _cachedDocumentText = null;
                    _cachedDocumentTextFileId = null;
                    
                    if (value != null)
                    {
                        _ = LoadChatHistoryAsync(value);
                    }
                    else
                    {
                        ClearChat(false);
                    }

                    _editableDocumentText = string.Empty;
                    _hasUnsavedChanges = false;
                    OnPropertyChanged(nameof(EditableDocumentText));
                    OnPropertyChanged(nameof(HasUnsavedChanges));
                    OnPropertyChanged(nameof(IsSelectedFileEditableText));

                    if (value != null && IsEditableTextFile(value))
                    {
                        _ = LoadDocumentTextAsync();
                    }
                }
            }
        }

        public bool IsSelectedFileEditableText => IsEditableTextFile(SelectedFile);

        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                if (SetProperty(ref _searchQuery, value))
                {
                    _ = LoadDocumentsAsync();
                }
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public string SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (SetProperty(ref _selectedCategory, value))
                {
                    _ = LoadDocumentsAsync();
                }
            }
        }

        public string? SelectedFileSummary
        {
            get => _selectedFileSummary;
            set => SetProperty(ref _selectedFileSummary, value);
        }

        public bool IsSummarizing
        {
            get => _isSummarizing;
            set => SetProperty(ref _isSummarizing, value);
        }

        public string ChatInputText
        {
            get => _chatInputText;
            set => SetProperty(ref _chatInputText, value);
        }

        public bool IsChatBusy
        {
            get => _isChatBusy;
            set
            {
                if (SetProperty(ref _isChatBusy, value))
                {
                    (SendChatMessageCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public ObservableCollection<DocumentChatMessage> ChatMessages { get; } = new();

        public bool IsCreatePanelVisible
        {
            get => _isCreatePanelVisible;
            set
            {
                if (SetProperty(ref _isCreatePanelVisible, value))
                {
                    (ConfirmCreateCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public string NewFileName
        {
            get => _newFileName;
            set
            {
                if (SetProperty(ref _newFileName, value))
                {
                    (ConfirmCreateCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public string SelectedNewDocumentType
        {
            get => _selectedNewDocumentType;
            set => SetProperty(ref _selectedNewDocumentType, value);
        }

        public ObservableCollection<string> NewDocumentTypes { get; } = new()
        {
            "Word Belgesi (.docx)",
            "E-Tablo (.xlsx)",
            "Düz Metin (.txt)",
            "Markdown (.md)"
        };

        public DocumentsViewModel(
            IAccountStore accountStore,
            IStorageService storageService,
            IAIService aiService,
            IWindowService windowService,
            IAppStatusService appStatusService,
            IDbContextFactory<LocalCacheDbContext> dbContextFactory,
            MultiSychConfig config,
            ICalendarService calendarService)
        {
            _accountStore = accountStore;
            _storageService = storageService;
            _aiService = aiService;
            _windowService = windowService;
            _appStatusService = appStatusService;
            _dbContextFactory = dbContextFactory;
            _config = config;
            _calendarService = calendarService;
            _logger = Log.ForContext<DocumentsViewModel>();

            RefreshCommand = new RelayCommand(async _ => await SyncAndReloadAsync(), _ => !IsLoading);
            OpenInWebCommand = new RelayCommand<CloudFileEntity?>(file => OpenInWeb(file), file => file != null && !string.IsNullOrEmpty(file.WebEditUrl));
            DownloadFileCommand = new RelayCommand<CloudFileEntity?>(async file => await DownloadFileAsync(file));
            DeleteFileCommand = new RelayCommand<CloudFileEntity?>(async file => await DeleteFileAsync(file));
            SummarizeDocumentCommand = new RelayCommand<CloudFileEntity?>(async file => await SummarizeDocumentAsync(file), file => file != null && !IsSummarizing);
            SelectCategoryCommand = new RelayCommand<string>(cat => SelectedCategory = cat ?? "Tümü");
            SendChatMessageCommand = new RelayCommand(async _ => await SendChatMessageAsync(), _ => !string.IsNullOrWhiteSpace(ChatInputText) && !IsChatBusy);
            ClearChatCommand = new RelayCommand(_ => ClearChat(true));
            ShowCreatePanelCommand = new RelayCommand(_ => ShowCreatePanel());
            CancelCreateCommand = new RelayCommand(_ => IsCreatePanelVisible = false);
            ConfirmCreateCommand = new RelayCommand(async _ => await ConfirmCreateAsync(), _ => SelectedAccount != null && !string.IsNullOrWhiteSpace(NewFileName) && !IsLoading);
            EditLocallyCommand = new RelayCommand<CloudFileEntity?>(file => EditLocally(file), file => file != null && IsEditableTextFile(file));
            CreateEmailDraftCommand = new RelayCommand<CloudFileEntity?>(async file => await CreateEmailDraftAsync(file), file => file != null && !IsLoading);
            ExtractEventsCommand = new RelayCommand<CloudFileEntity?>(async file => await ExtractEventsAsync(file), file => file != null && !IsExtractingEvents);
            AddEventSuggestionCommand = new RelayCommand<CalendarEvent>(async ev => { if (ev != null) await AddEventSuggestionAsync(ev); });
            CloseCalendarSuggestionsCommand = new RelayCommand(_ => IsCalendarSuggestionsPanelVisible = false);
            SaveDocumentTextCommand = new RelayCommand(async _ => await SaveDocumentTextAsync(), _ => HasUnsavedChanges && !IsEditing);
            CancelEditCommand = new RelayCommand(_ => { EditableDocumentText = _cachedDocumentText ?? string.Empty; HasUnsavedChanges = false; });

            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            IsLoading = true;
            try
            {
                var accounts = await _accountStore.GetAccountsAsync();
                Dispatcher.UIThread.Post(() =>
                {
                    Accounts.Clear();
                    foreach (var acc in accounts)
                    {
                        Accounts.Add(acc);
                    }

                    if (Accounts.Any())
                    {
                        SelectedAccount = Accounts.First();
                    }
                    else
                    {
                        IsLoading = false;
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to load accounts for documents view.");
                IsLoading = false;
            }
        }

        private async Task LoadDocumentsAsync()
        {
            if (SelectedAccount == null)
            {
                Dispatcher.UIThread.Post(() => Documents.Clear());
                return;
            }

            try
            {
                await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

                var query = dbContext.CloudFiles
                    .Where(f => f.AccountId == SelectedAccount.AccountId && !f.IsDirectory);

                if (!string.IsNullOrWhiteSpace(SearchQuery))
                {
                    var lowerSearch = SearchQuery.ToLower();
                    query = query.Where(f => f.FileName.ToLower().Contains(lowerSearch));
                }

                // SQLite-level optimized category filtering to minimize GC pressure and memory consumption
                if (SelectedCategory != "Tümü")
                {
                    query = SelectedCategory switch
                    {
                        "Word" => query.Where(f => f.FileName.EndsWith(".docx") || f.FileName.EndsWith(".doc") || f.FileName.EndsWith(".odt") || f.MimeType.Contains("word") || f.MimeType.Contains("wordprocessingml") || f.MimeType.Contains("google-apps.document")),
                        "Excel" => query.Where(f => f.FileName.EndsWith(".xlsx") || f.FileName.EndsWith(".xls") || f.FileName.EndsWith(".csv") || f.FileName.EndsWith(".ods") || f.MimeType.Contains("excel") || f.MimeType.Contains("spreadsheet") || f.MimeType.Contains("csv") || f.MimeType.Contains("spreadsheetml") || f.MimeType.Contains("google-apps.spreadsheet")),
                        "PowerPoint" => query.Where(f => f.FileName.EndsWith(".pptx") || f.FileName.EndsWith(".ppt") || f.FileName.EndsWith(".odp") || f.MimeType.Contains("powerpoint") || f.MimeType.Contains("presentation") || f.MimeType.Contains("presentationml") || f.MimeType.Contains("google-apps.presentation")),
                        "PDF" => query.Where(f => f.FileName.EndsWith(".pdf") || f.MimeType.Contains("pdf")),
                        "Metin" => query.Where(f => f.FileName.EndsWith(".txt") || f.FileName.EndsWith(".md") || f.FileName.EndsWith(".log") || f.MimeType.Contains("text/plain") || f.MimeType.Contains("markdown")),
                        "Görsel" => query.Where(f => f.FileName.EndsWith(".png") || f.FileName.EndsWith(".jpg") || f.FileName.EndsWith(".jpeg") || f.FileName.EndsWith(".tiff") || f.FileName.EndsWith(".bmp") || f.FileName.EndsWith(".gif") || f.MimeType.Contains("image/")),
                        "Ses" => query.Where(f => f.FileName.EndsWith(".mp3") || f.FileName.EndsWith(".wav") || f.FileName.EndsWith(".m4a") || f.FileName.EndsWith(".ogg") || f.FileName.EndsWith(".flac") || f.MimeType.Contains("audio/")),
                        _ => query
                    };
                }
                else
                {
                    // "Tümü" - pre-filter non-document types at database level
                    query = query.Where(f => 
                        f.FileName.EndsWith(".docx") || f.FileName.EndsWith(".doc") || f.FileName.EndsWith(".odt") || f.MimeType.Contains("word") || f.MimeType.Contains("wordprocessingml") || f.MimeType.Contains("google-apps.document") ||
                        f.FileName.EndsWith(".xlsx") || f.FileName.EndsWith(".xls") || f.FileName.EndsWith(".csv") || f.FileName.EndsWith(".ods") || f.MimeType.Contains("excel") || f.MimeType.Contains("spreadsheet") || f.MimeType.Contains("csv") || f.MimeType.Contains("spreadsheetml") || f.MimeType.Contains("google-apps.spreadsheet") ||
                        f.FileName.EndsWith(".pptx") || f.FileName.EndsWith(".ppt") || f.FileName.EndsWith(".odp") || f.MimeType.Contains("powerpoint") || f.MimeType.Contains("presentation") || f.MimeType.Contains("presentationml") || f.MimeType.Contains("google-apps.presentation") ||
                        f.FileName.EndsWith(".pdf") || f.MimeType.Contains("pdf") ||
                        f.FileName.EndsWith(".txt") || f.FileName.EndsWith(".md") || f.FileName.EndsWith(".log") || f.MimeType.Contains("text/plain") || f.MimeType.Contains("markdown") ||
                        f.FileName.EndsWith(".png") || f.FileName.EndsWith(".jpg") || f.FileName.EndsWith(".jpeg") || f.FileName.EndsWith(".tiff") || f.FileName.EndsWith(".bmp") || f.FileName.EndsWith(".gif") || f.MimeType.Contains("image/") ||
                        f.FileName.EndsWith(".mp3") || f.FileName.EndsWith(".wav") || f.FileName.EndsWith(".m4a") || f.FileName.EndsWith(".ogg") || f.FileName.EndsWith(".flac") || f.MimeType.Contains("audio/"));
                }

                var filteredList = await query
                    .OrderByDescending(f => f.UpdatedAt)
                    .ToListAsync();

                Dispatcher.UIThread.Post(() =>
                {
                    Documents.Clear();
                    foreach (var doc in filteredList)
                    {
                        Documents.Add(doc);
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to load cached documents.");
            }
        }

        private async Task SyncAndReloadAsync()
        {
            if (SelectedAccount == null) return;

            IsLoading = true;
            _appStatusService.PostUpdate($"{SelectedAccount.Email} belgeleri eşitleniyor...", isSyncing: true);

            try
            {
                await _storageService.SyncStorageAsync(SelectedAccount);
                await LoadDocumentsAsync();
                _appStatusService.PostUpdate("Belge eşitlemesi tamamlandı.", isSyncing: false);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error syncing documents from server.");
                _appStatusService.PostUpdate($"Belge eşitleme hatası: {ex.Message}", isSyncing: false);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void OpenInWeb(CloudFileEntity? file)
        {
            if (file == null || string.IsNullOrEmpty(file.WebEditUrl)) return;
            try
            {
                _logger.Information("Opening document URL in web: {Url}", file.WebEditUrl);
                if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = file.WebEditUrl,
                        UseShellExecute = true
                    });
                }
                else if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux))
                {
                    Process.Start("xdg-open", file.WebEditUrl);
                }
                else if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX))
                {
                    Process.Start("open", file.WebEditUrl);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to open document URL in default browser.");
            }
        }

        private bool IsEditableTextFile(CloudFileEntity? file)
        {
            if (file == null) return false;
            var ext = Path.GetExtension(file.FileName).ToLower();
            return ext == ".txt" || ext == ".md" || ext == ".log" || ext == ".csv" || ext == ".json" || (file.MimeType != null && file.MimeType.Contains("text/"));
        }

        private void EditLocally(CloudFileEntity? file)
        {
            if (SelectedAccount == null || file == null) return;
            
            try
            {
                var targetFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "MultiSych_Drives", SelectedAccount.AccountId ?? string.Empty);
                var localFilePath = Path.Combine(targetFolder, file.Path.TrimStart('/')).Replace('\\', '/');
                
                var directory = Path.GetDirectoryName(localFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                if (!File.Exists(localFilePath))
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            _appStatusService.PostUpdate($"{file.FileName} indiriliyor...", isSyncing: true);
                            using var stream = await _storageService.DownloadFileAsync(SelectedAccount, file.FileId);
                            using var fileStream = File.Create(localFilePath);
                            await stream.CopyToAsync(fileStream);
                            _appStatusService.PostUpdate("Düzenleme için indirildi. Yerel düzenleyici açılıyor.", isSyncing: false);
                            
                            LaunchLocalEditor(localFilePath);
                        }
                        catch (Exception ex)
                        {
                            _logger.Error(ex, "Failed to download file for local editing, creating empty file instead.");
                            try { await File.WriteAllBytesAsync(localFilePath, Array.Empty<byte>()); } catch { }
                            LaunchLocalEditor(localFilePath);
                        }
                    });
                }
                else
                {
                    LaunchLocalEditor(localFilePath);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to initiate local editing.");
            }
        }

        private void LaunchLocalEditor(string filePath)
        {
            try
            {
                _logger.Information("Launching local editor for file: {Path}", filePath);
                if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = filePath,
                        UseShellExecute = true
                    });
                }
                else if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux))
                {
                    Process.Start("xdg-open", filePath);
                }
                else if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX))
                {
                    Process.Start("open", filePath);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to launch local editor for {Path}", filePath);
            }
        }

        private async Task CreateEmailDraftAsync(CloudFileEntity? file)
        {
            if (SelectedAccount == null || file == null || string.IsNullOrEmpty(file.FileId)) return;

            IsLoading = true;
            _appStatusService.PostUpdate($"{file.FileName} belgesinden e-posta taslağı hazırlanıyor...", isSyncing: true);

            try
            {
                if (_cachedDocumentTextFileId != file.FileId)
                {
                    _cachedDocumentText = await ExtractDocumentTextAsync(file);
                    _cachedDocumentTextFileId = file.FileId;
                }

                if (string.IsNullOrWhiteSpace(_cachedDocumentText))
                {
                    _appStatusService.PostUpdate("Belge içeriği okunamadı (Boş dosya veya uyumsuz format).", isSyncing: false);
                    return;
                }

                var prompt = $@"Aşağıdaki belge içeriğine dayanarak profesyonel bir e-posta taslağı (Konu ve Gövde) hazırla.
E-posta, belgeyi özetlemeli veya belgedeki ana fikirleri aktarmalıdır.
Lütfen yanıtı tam olarak aşağıdaki biçimde ver:
SUBJECT: [E-posta Konusu]
BODY:
[E-posta Gövde Metni]

BELGE İÇERİĞİ:
---
{_cachedDocumentText}
---";

                var provider = _config.AI?.DefaultProvider ?? "hybrid";
                var rawDraft = await _aiService.GetResponseAsync(prompt, provider);

                string subject = $"Belge Hakkında: {file.FileName}";
                string body = string.Empty;

                if (!string.IsNullOrWhiteSpace(rawDraft))
                {
                    var lines = rawDraft.Split(new[] { "BODY:" }, StringSplitOptions.None);
                    if (lines.Length >= 2)
                    {
                        var subjectPart = lines[0].Replace("SUBJECT:", "").Trim();
                        if (!string.IsNullOrWhiteSpace(subjectPart))
                        {
                            subject = subjectPart;
                        }
                        body = lines[1].Trim();
                    }
                    else
                    {
                        body = rawDraft;
                    }
                }

                _appStatusService.PostUpdate("E-posta taslağı hazırlandı. Taslak oluşturuluyor...", isSyncing: false);

                var initialAttachments = new List<CloudFileEntity> { file };
                _windowService.ShowNewEmailDialog(SelectedAccount.AccountId, to: "", subject: subject, body: body, initialAttachments: initialAttachments);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to create email draft from document.");
                _appStatusService.PostUpdate($"Taslak oluşturulamadı: {ex.Message}", isSyncing: false);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ExtractEventsAsync(CloudFileEntity? file)
        {
            if (SelectedAccount == null || file == null || string.IsNullOrEmpty(file.FileId)) return;

            IsExtractingEvents = true;
            _appStatusService.PostUpdate($"{file.FileName} belgesinden takvim etkinlikleri çıkarılıyor...", isSyncing: true);
            CalendarSuggestions.Clear();

            try
            {
                if (_cachedDocumentTextFileId != file.FileId)
                {
                    _cachedDocumentText = await ExtractDocumentTextAsync(file);
                    _cachedDocumentTextFileId = file.FileId;
                }

                if (string.IsNullOrWhiteSpace(_cachedDocumentText))
                {
                    _appStatusService.PostUpdate("Belge içeriği okunamadı (Boş dosya veya uyumsuz format).", isSyncing: false);
                    return;
                }

                var provider = _config.AI?.DefaultProvider ?? "hybrid";
                var suggestions = await _aiService.ExtractEventsFromDocumentAsync(_cachedDocumentText, provider);

                if (suggestions != null && suggestions.Any())
                {
                    foreach (var suggestion in suggestions)
                    {
                        CalendarSuggestions.Add(suggestion);
                    }
                    IsCalendarSuggestionsPanelVisible = true;
                    _appStatusService.PostUpdate($"{suggestions.Count} adet takvim etkinliği önerisi bulundu.", isSyncing: false);
                }
                else
                {
                    _appStatusService.PostUpdate("Herhangi bir takvim etkinliği önerisi bulunamadı.", isSyncing: false);
                    await _windowService.ShowMessageDialogAsync("Takvim Önerisi", "Belgede herhangi bir tarih/saat veya etkinlik referansı bulunamadı.");
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to extract calendar events from document.");
                _appStatusService.PostUpdate($"Etkinlikler çıkarılamadı: {ex.Message}", isSyncing: false);
            }
            finally
            {
                IsExtractingEvents = false;
            }
        }

        private async Task AddEventSuggestionAsync(CalendarEvent @event)
        {
            if (SelectedAccount == null || @event == null) return;

            try
            {
                _appStatusService.PostUpdate($"'{@event.Title}' takvime ekleniyor...", isSyncing: true);
                
                var eventId = await _calendarService.CreateEventAsync(SelectedAccount, @event);
                
                if (!string.IsNullOrEmpty(eventId))
                {
                    CalendarSuggestions.Remove(@event);
                    if (!CalendarSuggestions.Any())
                    {
                        IsCalendarSuggestionsPanelVisible = false;
                    }
                    _appStatusService.PostUpdate("Etkinlik takvime başarıyla eklendi.", isSyncing: false);
                    _windowService.ShowNotification("Takvim", $"'{@event.Title}' etkinliği takviminize eklendi.");
                }
                else
                {
                    _appStatusService.PostUpdate("Etkinlik takvime eklenemedi.", isSyncing: false);
                    await _windowService.ShowMessageDialogAsync("Hata", "Etkinlik oluşturulurken bir hata oluştu.");
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to add calendar event suggestion.");
                _appStatusService.PostUpdate($"Hata: {ex.Message}", isSyncing: false);
                await _windowService.ShowMessageDialogAsync("Hata", $"Etkinlik eklenemedi: {ex.Message}");
            }
        }

        private async Task LoadDocumentTextAsync()
        {
            if (SelectedAccount == null || SelectedFile == null) return;

            IsEditing = true;
            _appStatusService.PostUpdate("Belge metni yükleniyor...", isSyncing: true);

            try
            {
                if (_cachedDocumentTextFileId != SelectedFile.FileId)
                {
                    _cachedDocumentText = await ExtractDocumentTextAsync(SelectedFile);
                    _cachedDocumentTextFileId = SelectedFile.FileId;
                }

                EditableDocumentText = _cachedDocumentText ?? string.Empty;
                HasUnsavedChanges = false;
                _appStatusService.PostUpdate("Belge metni yüklendi.", isSyncing: false);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to load document text for editing.");
                _appStatusService.PostUpdate($"Yüklenemedi: {ex.Message}", isSyncing: false);
            }
            finally
            {
                IsEditing = false;
            }
        }

        private async Task SaveDocumentTextAsync()
        {
            if (SelectedAccount == null || SelectedFile == null) return;

            IsEditing = true;
            _appStatusService.PostUpdate("Değişiklikler kaydediliyor...", isSyncing: true);

            try
            {
                var targetFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "MultiSych_Drives", SelectedAccount.AccountId ?? string.Empty);
                var localFilePath = Path.Combine(targetFolder, SelectedFile.Path.TrimStart('/')).Replace('\\', '/');

                var directory = Path.GetDirectoryName(localFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                await File.WriteAllTextAsync(localFilePath, EditableDocumentText);

                _cachedDocumentText = EditableDocumentText;
                _cachedDocumentTextFileId = SelectedFile.FileId;
                HasUnsavedChanges = false;

                (SaveDocumentTextCommand as RelayCommand)?.RaiseCanExecuteChanged();

                _appStatusService.PostUpdate("Değişiklikler kaydedildi. Otomatik senkronizasyon tetiklendi.", isSyncing: false);
                _windowService.ShowNotification("Belge Düzenleyici", $"{SelectedFile.FileName} güncellendi ve senkronize ediliyor.");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to save document inline edits.");
                _appStatusService.PostUpdate($"Kaydedilemedi: {ex.Message}", isSyncing: false);
                await _windowService.ShowMessageDialogAsync("Hata", $"Belge kaydedilemedi: {ex.Message}");
            }
            finally
            {
                IsEditing = false;
            }
        }

        private async Task DownloadFileAsync(CloudFileEntity? file)
        {
            if (SelectedAccount == null || file == null || string.IsNullOrEmpty(file.FileId)) return;

            var ext = Path.GetExtension(file.FileName).Replace(".", "");
            var savePath = await _windowService.SaveFileDialogAsync("Belgeyi Kaydet", ext);
            if (string.IsNullOrEmpty(savePath)) return;

            IsLoading = true;
            _appStatusService.PostUpdate($"{file.FileName} indiriliyor...", isSyncing: true);

            try
            {
                using var stream = await _storageService.DownloadFileAsync(SelectedAccount, file.FileId);
                using var fileStream = File.Create(savePath);
                await stream.CopyToAsync(fileStream);

                _appStatusService.PostUpdate($"{file.FileName} başarıyla indirildi.", isSyncing: false);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to download file.");
                _appStatusService.PostUpdate($"Belge indirilemedi: {ex.Message}", isSyncing: false);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task DeleteFileAsync(CloudFileEntity? file)
        {
            if (SelectedAccount == null || file == null || string.IsNullOrEmpty(file.FileId)) return;

            var confirmed = await _windowService.ShowConfirmationDialogAsync($"'{file.FileName}' belgesini buluttan kalıcı olarak silmek istediğinize emin misiniz?");
            if (!confirmed) return;

            IsLoading = true;
            _appStatusService.PostUpdate($"{file.FileName} siliniyor...", isSyncing: true);

            try
            {
                await _storageService.DeleteFileAsync(SelectedAccount, file.FileId);

                await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
                var entity = await dbContext.CloudFiles.FirstOrDefaultAsync(f => f.AccountId == SelectedAccount.AccountId && f.FileId == file.FileId);
                if (entity != null)
                {
                    dbContext.CloudFiles.Remove(entity);
                    await dbContext.SaveChangesAsync();
                }

                if (SelectedFile == file)
                {
                    SelectedFile = null;
                }

                await LoadDocumentsAsync();
                _appStatusService.PostUpdate("Belge silindi.", isSyncing: false);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to delete file.");
                _appStatusService.PostUpdate($"Belge silinemedi: {ex.Message}", isSyncing: false);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task<string?> ExtractDocumentTextAsync(CloudFileEntity file)
        {
            if (SelectedAccount == null) return null;
            
            using var stream = await _storageService.DownloadFileAsync(SelectedAccount, file.FileId);
            var extension = Path.GetExtension(file.FileName).ToLower();
            
            // 1. Düz metin dosyaları
            if (extension == ".txt" || extension == ".md" || extension == ".csv" || extension == ".json" || extension == ".log" || file.MimeType.Contains("text/"))
            {
                using var reader = new StreamReader(stream);
                return await reader.ReadToEndAsync();
            }
            
            // 2. Multimodal dosyalar (Görsel, PDF ve Ses)
            var isImage = extension == ".png" || extension == ".jpg" || extension == ".jpeg" || extension == ".tiff" || extension == ".bmp" || extension == ".gif" || file.MimeType.Contains("image/");
            var isPdf = extension == ".pdf" || file.MimeType.Contains("pdf");
            var isAudio = extension == ".mp3" || extension == ".wav" || extension == ".m4a" || extension == ".ogg" || extension == ".flac" || file.MimeType.Contains("audio/");
            
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);
            var bytes = memoryStream.ToArray();
            
            if (isImage || isPdf || isAudio)
            {
                try
                {
                    var mimeType = file.MimeType;
                    if (string.IsNullOrWhiteSpace(mimeType) || mimeType == "application/octet-stream")
                    {
                        mimeType = extension switch
                        {
                            ".png" => "image/png",
                            ".jpg" or ".jpeg" => "image/jpeg",
                            ".gif" => "image/gif",
                            ".bmp" => "image/bmp",
                            ".tiff" => "image/tiff",
                            ".pdf" => "application/pdf",
                            ".mp3" => "audio/mp3",
                            ".wav" => "audio/wav",
                            ".m4a" => "audio/m4a",
                            ".ogg" => "audio/ogg",
                            ".flac" => "audio/flac",
                            _ => isAudio ? "audio/mp3" : isImage ? "image/jpeg" : "application/pdf"
                        };
                    }
                    
                    var provider = _config.AI?.DefaultProvider ?? "hybrid";
                    var ocrText = await _aiService.ExtractTextFromMultimodalAsync(bytes, mimeType, provider);
                    if (!string.IsNullOrWhiteSpace(ocrText))
                    {
                        return ocrText;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Warning(ex, "Multimodal text/audio extraction failed, falling back to ASCII extractor.");
                }
            }
            
            // 3. Fallback: İkili dosyalardan okunabilir ASCII/UTF-8 karakterlerini çıkar
            var charList = new List<char>();
            for (int i = 0; i < bytes.Length && charList.Count < 10000; i++)
            {
                var b = bytes[i];
                if (b == 10 || b == 13 || (b >= 32 && b <= 126) || (b >= 192 && b <= 255))
                {
                    charList.Add((char)b);
                }
            }
            return new string(charList.ToArray());
        }

        private async Task SummarizeDocumentAsync(CloudFileEntity? file)
        {
            if (SelectedAccount == null || file == null || string.IsNullOrEmpty(file.FileId)) return;

            IsSummarizing = true;
            _appStatusService.PostUpdate($"{file.FileName} yapay zeka tarafından özetleniyor...", isSyncing: true);
            SelectedFileSummary = null;

            try
            {
                if (_cachedDocumentTextFileId != file.FileId)
                {
                    _cachedDocumentText = await ExtractDocumentTextAsync(file);
                    _cachedDocumentTextFileId = file.FileId;
                }

                if (string.IsNullOrWhiteSpace(_cachedDocumentText))
                {
                    _appStatusService.PostUpdate("Belge içeriği okunamadı (Boş dosya veya uyumsuz format).", isSyncing: false);
                    return;
                }

                var provider = _config.AI?.DefaultProvider ?? "hybrid";
                var summary = await _aiService.SummarizeDocumentAsync(_cachedDocumentText, provider);

                SelectedFileSummary = summary;
                _appStatusService.PostUpdate("Belge özeti oluşturuldu.", isSyncing: false);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to summarize document.");
                _appStatusService.PostUpdate($"Özet oluşturulamadı: {ex.Message}", isSyncing: false);
            }
            finally
            {
                IsSummarizing = false;
            }
        }

        private void ClearChat(bool deleteFromDb = false)
        {
            var selectedFile = SelectedFile;
            Dispatcher.UIThread.Post(() =>
            {
                ChatMessages.Clear();
                ChatMessages.Add(new DocumentChatMessage
                {
                    IsUser = false,
                    Text = "Merhaba! Bu belge hakkında ne öğrenmek istersiniz? Bana belgeyle ilgili sorular sorabilirsiniz."
                });
            });

            if (deleteFromDb && selectedFile != null)
            {
                _ = DeleteChatHistoryFromDatabaseAsync(selectedFile.AccountId, selectedFile.FileId);
            }
        }

        private async Task DeleteChatHistoryFromDatabaseAsync(string accountId, string fileId)
        {
            try
            {
                await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
                var messages = await dbContext.DocumentChatMessages
                    .Where(m => m.AccountId == accountId && m.FileId == fileId)
                    .ToListAsync();
                if (messages.Any())
                {
                    dbContext.DocumentChatMessages.RemoveRange(messages);
                    await dbContext.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to delete chat history from database.");
            }
        }

        private async Task LoadChatHistoryAsync(CloudFileEntity file)
        {
            Dispatcher.UIThread.Post(() =>
            {
                ChatMessages.Clear();
                ChatMessages.Add(new DocumentChatMessage
                {
                    IsUser = false,
                    Text = "Merhaba! Bu belge hakkında ne öğrenmek istersiniz? Bana belgeyle ilgili sorular sorabilirsiniz."
                });
            });

            try
            {
                await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
                var savedMessages = await dbContext.DocumentChatMessages
                    .Where(m => m.AccountId == file.AccountId && m.FileId == file.FileId)
                    .OrderBy(m => m.CreatedAt)
                    .ToListAsync();

                if (savedMessages.Any())
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        ChatMessages.Clear();
                        foreach (var msg in savedMessages)
                        {
                            ChatMessages.Add(new DocumentChatMessage
                            {
                                IsUser = msg.IsUser,
                                Text = msg.Text,
                                Time = msg.Time
                            });
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to load chat history for file {FileId}", file.FileId);
            }
        }

        private async Task SaveChatMessageToDatabaseAsync(string accountId, string fileId, string text, bool isUser, string time)
        {
            try
            {
                await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
                var entity = new DocumentChatMessageEntity
                {
                    AccountId = accountId,
                    FileId = fileId,
                    Text = text,
                    IsUser = isUser,
                    Time = time,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                dbContext.DocumentChatMessages.Add(entity);
                await dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to save chat message to database.");
            }
        }

        private async Task SendChatMessageAsync()
        {
            if (SelectedAccount == null || SelectedFile == null || string.IsNullOrWhiteSpace(ChatInputText)) return;

            var userQuestion = ChatInputText;
            ChatInputText = string.Empty;

            var userMsg = new DocumentChatMessage { IsUser = true, Text = userQuestion };
            ChatMessages.Add(userMsg);
            _ = SaveChatMessageToDatabaseAsync(SelectedFile.AccountId, SelectedFile.FileId, userQuestion, true, userMsg.Time);

            IsChatBusy = true;
            _appStatusService.PostUpdate("Yapay zeka yanıt hazırlıyor...", isSyncing: true);

            try
            {
                if (_cachedDocumentTextFileId != SelectedFile.FileId)
                {
                    _cachedDocumentText = await ExtractDocumentTextAsync(SelectedFile);
                    _cachedDocumentTextFileId = SelectedFile.FileId;
                }

                if (string.IsNullOrWhiteSpace(_cachedDocumentText))
                {
                    var errorMsg = new DocumentChatMessage { IsUser = false, Text = "Hata: Belge içeriği okunamadı veya boş." };
                    ChatMessages.Add(errorMsg);
                    _appStatusService.PostUpdate("Belge içeriği boş veya okunamadı.", isSyncing: false);
                    return;
                }

                var provider = _config.AI?.DefaultProvider ?? "hybrid";
                var historyList = new List<string>();
                var lastMessages = ChatMessages.SkipLast(1).TakeLast(6).ToList();
                foreach (var msg in lastMessages)
                {
                    historyList.Add(msg.Text);
                }

                var systemPrompt = $@"Sen MultiSych platformundaki bir AI belge yardımcısısın. 
Aşağıdaki belgenin içeriğini kaynak alarak kullanıcının sorularını yanıtla. 
Verilen belgede geçmeyen bilgileri tahmin etme, sadece belgedeki verilere dayanarak doğru ve net yanıtlar ver. 
Eğer soru belgede geçmeyen bir konu hakkındaysa, bunu açıkça belirt.

BELGE İÇERİĞİ:
---
{_cachedDocumentText}
---

Sohbet Geçmişi ve Yeni Soru:";

                var promptWithContext = $"{systemPrompt}\n" + string.Join("\n", historyList) + $"\nKullanıcı: {userQuestion}";
                var response = await _aiService.GetResponseAsync(promptWithContext, provider);

                var aiMsg = new DocumentChatMessage { IsUser = false, Text = response };
                ChatMessages.Add(aiMsg);
                _ = SaveChatMessageToDatabaseAsync(SelectedFile.AccountId, SelectedFile.FileId, response, false, aiMsg.Time);
                _appStatusService.PostUpdate("Yapay zeka yanıtladı.", isSyncing: false);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get document chat response.");
                var errorMsg = new DocumentChatMessage { IsUser = false, Text = $"Hata oluştu: {ex.Message}" };
                ChatMessages.Add(errorMsg);
                _appStatusService.PostUpdate($"Hata: {ex.Message}", isSyncing: false);
            }
            finally
            {
                IsChatBusy = false;
            }
        }

        private void ShowCreatePanel()
        {
            NewFileName = "Yeni Döküman";
            SelectedNewDocumentType = "Word Belgesi (.docx)";
            IsCreatePanelVisible = true;
        }

        private async Task ConfirmCreateAsync()
        {
            if (SelectedAccount == null || string.IsNullOrWhiteSpace(NewFileName)) return;

            var ext = SelectedNewDocumentType switch
            {
                "Word Belgesi (.docx)" => ".docx",
                "E-Tablo (.xlsx)" => ".xlsx",
                "Düz Metin (.txt)" => ".txt",
                "Markdown (.md)" => ".md",
                _ => ".txt"
            };

            var finalFileName = NewFileName;
            if (!finalFileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
            {
                finalFileName += ext;
            }

            IsCreatePanelVisible = false;
            IsLoading = true;
            _appStatusService.PostUpdate($"{finalFileName} oluşturuluyor...", isSyncing: true);

            var tempFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}_{finalFileName}");

            try
            {
                if (ext == ".docx")
                {
                    var docxBase64 = "UEsDBBQAAAAIAFyY2VzXeYTq8gAAALgBAAATAAAAW0NvbnRlbnRfVHlwZXNdLnhtbH2Qy07DMBBF9/0Ka7aodmCBEIrTBY8lsCgfYNmTxKo9tjxuSP8epYUiIcr6Ps6daTdzDGLCwj6RhmvZgECyyXkaNLxvn9d3ILgaciYkQg0HZNh0q3Z7yMhijoFYw1hrvleK7YjRsEwZaY6hTyWayjKVQWVjd2ZAddM0t8omqkh1XZcO6FZCtI/Ym32o4mmuSKctBQODeDh5F5wGk3Pw1lSfSE3kfoHWXxBZMBw9PPrMV3MMoC5BFvEy4yf6OmEp3qF4M6W+mIga1EcqTrlk9xGpyv+b/lib+t5bPOeXtlySRWZPQwzyrETj6fuKVh0f330CUEsDBBQAAAAIAFyY2VwgG4bqtgAAAC4BAAALAAAAX3JlbHMvLnJlbHONz7FOxDAQBNA+X7Ha/uIcBUIozjUnpGtR+ADL3iQW9q7l9UHu72koOERBOxq90YynPSf4oKpR2OKxHxCIvYTIq8W3+eXwhKDNcXBJmCzeSPE0deMrJdeisG6xKOw5sVrcWivPxqjfKDvtpRDvOS1Ss2vaS11Ncf7drWQehuHR1J8GTh3AHQuXYLFewhFhvhX6Dy/LEjy9iv9IxPWPFfMl5aobUUWYXFmpWvyp1Nyjb/cU0YzNYB5ejt9QSwDBBQAAAAIAFyY2Vz/jFiNpHAAAAPIAAAAEQAAAHdvcmQvZG9jdW1lbnQueG1sPc5LDoMgEAbgvacg7Cu2i6YxorueoD0AFaokMEMYWvX2DRi7mXx/5pHphtU79jWRLILk57rhzMCI2sIk+fNxP904o6RAK4dgJN8M8aGvuqXVOH68gcRW74DaRfI5pdAKQeNsvKIag4HVuzdGrxLVGCexYNQh4miILEzeiUvTXIVXFnhfMdYt7Qv1lllC2FUcD5eU+k7kevTFfyCz7GXs17KOb/sfUEsBAhQDFAAAAAgAXJjZXNd5hOryAAAAuAEAABMAAAAAAAAAAAAAAIABAAAAAFtDb250ZW50X1R5cGVzXS54bWxQSwECFAMUAAAACABcmNlcIBuG6rYAAAAuAQAACwAAAAAAAAAAAAAAgAEjAQAAX3JlbHMvLnJlbHNQSwECFAMUAAAACABcmNlc/4xYjaUAAADyAAAAEQAAAAAAAAAAAAAAgAECAgAAd29yZC9kb2N1bWVudC54bWxQSwUGAAAAAAMAAwC5AAAA1gIAAAAA";
                    var bytes = Convert.FromBase64String(docxBase64);
                    await File.WriteAllBytesAsync(tempFilePath, bytes);
                }
                else if (ext == ".xlsx")
                {
                    var xlsxBase64 = "UEsDBBQAAAAIAFyY2Vy5mqGQBgEAADsCAAATAAAAW0NvbnRlbnRfVHlwZXNdLnhtbK2Ru27DMAxF93yFoDWwlHQoisJOhj7GtkP6AapMx0IkUhAZ1/n7Ik4fQNEUHTpx4OU9B2C9HlNUAxQOhI1emoVWgJ7agNtGP2/uqyutWBy2LhJCow/Aer2a1ZtDBlZjisiN7kXytbXse0iODWXAMcWOSnLChsrWZud3bgv2YrG4tJ5QAKWSY4dezZSqb6Fz+yjqbhTAk0uByFrdnLJHXKNdzjF4J4HQDth+A1XvEFMgThnuQ+b5mKK25yDH5XnG1+njAKWEFtSTK/LgEjTajtG+Utm9EO3M7z0/uFLXBQ8t+X0CFMO5gGu5B5AUzTRNcgHnf1KY8mynsfxnl8/FDTb6fesNgFLAwQUAAAACABcmNlcXYf0LrYAAAAsBAAACwAAAF9yZWxzLy5yZWxzhc8xTsQxEBXhPqcYTU+cpUAIxdkGIW2LwgGMM0ms2DOWx4D39rQsoqB/+p7+8dxShE8qGoQtnvoBgdjLEnizeDb/3D0iaHW8uChMFq+keJ668ZWiS0FYd5AVWoqsFvda85Mx6ndKTnvJxC3FVUpyVXspp8nOH24jcz8MD6b8NHDqAG5YuCwWy2U5IczXTP/hZV2Dp2fxH4m4/vHya4Ewu7JRtdii+ZJyfIscfUsRzdSN5iZy+gZQSwMEFAAAAAgAXJjZXNXDBk3CAAAAKAEAAP8AAAB4bC93b3JrYm9vay54bWxsb3y5nK2RiAMBAAA7AgAAEwAAAAAAAAAAAAAAgAEAAAAAW0NvbnRlbnRfVHlwZXNdLnhtbFBLAQIUAxQAAAAIAFyY2Vxdh/QutgAAACwBAAALAAAAAAAAAAAAAACAATcBAABfcmVscy8ucmVsc1BLAQIUAxQAAAAIAFyY2VzVwwZNwgAAACgBAAAPAAAAAAAAAAAAAACAARYCAAB4bC93b3JrYm9vay54bWxQSwECFAMUAAAACABcmNlc9WADgrsAAAAtAQAAGgAAAAAAAAAAAAAAgAEFAwAAeGwvX3JlbHMvd29ya2Jvb2sueG1sLnJlbHNQSwECFAMUAAAACABcmNlch5PdQocAAAChAAAAGAAAAAAAAAAAAAAAgAH4AwAAeGwvd29ya3NoZWV0cy9zaGVldDEueG1sUEsFBgAAAAAFAAUARQEAALUEAAAAAA==";
                    var bytes = Convert.FromBase64String(xlsxBase64);
                    await File.WriteAllBytesAsync(tempFilePath, bytes);
                }
                else
                {
                    await File.WriteAllTextAsync(tempFilePath, string.Empty);
                }

                var fileId = await _storageService.UploadFileAsync(SelectedAccount, tempFilePath, "root");

                await _storageService.SyncStorageAsync(SelectedAccount);
                await LoadDocumentsAsync();

                _appStatusService.PostUpdate("Belge oluşturuldu.", isSyncing: false);

                var newFile = Documents.FirstOrDefault(d => d.FileId == fileId || d.FileName.Equals(finalFileName, StringComparison.OrdinalIgnoreCase));
                if (newFile != null)
                {
                    SelectedFile = newFile;
                    OpenInWeb(newFile);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to create document.");
                _appStatusService.PostUpdate($"Belge oluşturulamadı: {ex.Message}", isSyncing: false);
            }
            finally
            {
                try { if (File.Exists(tempFilePath)) File.Delete(tempFilePath); } catch { }
                IsLoading = false;
            }
        }
    }
}
