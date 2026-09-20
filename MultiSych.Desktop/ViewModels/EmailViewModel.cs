using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using Microsoft.EntityFrameworkCore;
using MultiSych.Desktop.Services;
using IWindowService = MultiSych.Desktop.Services.IWindowService;
using MultiSych.Services.Data;
using MultiSych.Services.Interfaces;
using MultiSych.Services.Models;
using Serilog;

namespace MultiSych.Desktop.ViewModels;

public class EmailViewModel : ViewModelBase
{
    private readonly IAccountStore _accountStore;
    private readonly IEmailService _emailService;
    private readonly IAIService _aiService;
    private readonly IWindowService _windowService;
    private readonly IAppStatusService _appStatusService;
    private readonly IDbContextFactory<LocalCacheDbContext> _dbContextFactory;
    private readonly ILogger _logger;

    private AccountCredentials? _selectedAccount;
    private EmailMessage? _selectedEmail;
    private string _searchQuery = string.Empty;
    private bool _isLoading;
    private int _unreadCount;
    private string? _selectedEmailSummary;
    private bool _isSummarizing;
    private bool _isGeneratingReply;
    private string _selectedFolder = "Inbox";
    private string _customReplyInstruction = string.Empty;

    public ObservableCollection<AccountCredentials> Accounts { get; } = new();
    public ObservableCollection<EmailMessage> Emails { get; } = new();

    public ICommand RefreshCommand { get; }
    public ICommand DeleteEmailCommand { get; }
    public ICommand ToggleReadStatusCommand { get; }
    public ICommand ComposeEmailCommand { get; }
    public ICommand SummarizeEmailCommand { get; }
    public ICommand GenerateSmartReplyCommand { get; }
    public ICommand SelectFolderCommand { get; }
    public ICommand ArchiveEmailCommand { get; }
    public ICommand GenerateCustomReplyCommand { get; }

    public string CustomReplyInstruction
    {
        get => _customReplyInstruction;
        set
        {
            if (SetProperty(ref _customReplyInstruction, value))
            {
                (GenerateCustomReplyCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public string SelectedFolder
    {
        get => _selectedFolder;
        set
        {
            if (SetProperty(ref _selectedFolder, value))
            {
                OnPropertyChanged(nameof(IsInboxSelected));
                OnPropertyChanged(nameof(IsSentSelected));
                OnPropertyChanged(nameof(IsTrashSelected));
                OnPropertyChanged(nameof(IsVaultSelected));
                _ = LoadCachedEmailsAsync();
            }
        }
    }

    public bool IsInboxSelected => SelectedFolder == "Inbox";
    public bool IsSentSelected => SelectedFolder == "Sent";
    public bool IsTrashSelected => SelectedFolder == "Trash";
    public bool IsVaultSelected => SelectedFolder == "Vault";

    public string? SelectedEmailSummary
    {
        get => _selectedEmailSummary;
        set
        {
            if (SetProperty(ref _selectedEmailSummary, value))
            {
                OnPropertyChanged(nameof(ShowSummarizeButton));
            }
        }
    }

    public bool IsSummarizing
    {
        get => _isSummarizing;
        set
        {
            if (SetProperty(ref _isSummarizing, value))
            {
                OnPropertyChanged(nameof(ShowSummarizeButton));
            }
        }
    }

    public bool IsGeneratingReply
    {
        get => _isGeneratingReply;
        set
        {
            if (SetProperty(ref _isGeneratingReply, value))
            {
                (GenerateSmartReplyCommand as RelayCommand<string>)?.RaiseCanExecuteChanged();
                (GenerateCustomReplyCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public bool ShowSummarizeButton => SelectedEmail != null && string.IsNullOrEmpty(SelectedEmailSummary) && !IsSummarizing;

    private string _selectedCategoryFilter = "Tümü";
    public string SelectedCategoryFilter
    {
        get => _selectedCategoryFilter;
        set
        {
            if (SetProperty(ref _selectedCategoryFilter, value))
            {
                _ = LoadCachedEmailsAsync();
            }
        }
    }

    public ObservableCollection<string> CategoryFilters { get; } = new() { "Tümü", "İş", "Fatura", "Toplantı", "Sosyal", "Kişisel" };

    private bool _isAdvancedFilterVisible;
    public bool IsAdvancedFilterVisible
    {
        get => _isAdvancedFilterVisible;
        set => SetProperty(ref _isAdvancedFilterVisible, value);
    }

    private string _selectedDateFilter = "Tüm Zamanlar";
    public string SelectedDateFilter
    {
        get => _selectedDateFilter;
        set
        {
            if (SetProperty(ref _selectedDateFilter, value))
            {
                _ = LoadCachedEmailsAsync();
            }
        }
    }

    public ObservableCollection<string> DateFilters { get; } = new() { "Tüm Zamanlar", "Bugün", "Bu Hafta", "Bu Ay" };

    private string _selectedReadStatusFilter = "Tümü";
    public string SelectedReadStatusFilter
    {
        get => _selectedReadStatusFilter;
        set
        {
            if (SetProperty(ref _selectedReadStatusFilter, value))
            {
                _ = LoadCachedEmailsAsync();
            }
        }
    }

    public ObservableCollection<string> ReadStatusFilters { get; } = new() { "Tümü", "Okunmamışlar", "Okunmuşlar" };

    private string _senderFilter = string.Empty;
    public string SenderFilter
    {
        get => _senderFilter;
        set
        {
            if (SetProperty(ref _senderFilter, value))
            {
                _ = LoadCachedEmailsAsync();
            }
        }
    }


    public AccountCredentials? SelectedAccount
    {
        get => _selectedAccount;
        set
        {
            if (SetProperty(ref _selectedAccount, value))
            {
                _ = LoadCachedEmailsAsync();
                if (value != null)
                {
                    _ = SyncAndReloadAsync();
                }
            }
        }
    }

    public EmailMessage? SelectedEmail
    {
        get => _selectedEmail;
        set
        {
            if (SetProperty(ref _selectedEmail, value))
            {
                SelectedEmailSummary = value?.AiSummary;
                OnPropertyChanged(nameof(ShowSummarizeButton));
                if (value != null && !value.IsRead)
                {
                    _ = MarkAsReadAsync(value);
                }
                (GenerateSmartReplyCommand as RelayCommand<string>)?.RaiseCanExecuteChanged();
                (GenerateCustomReplyCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
            {
                _ = LoadCachedEmailsAsync();
            }
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public int UnreadCount
    {
        get => _unreadCount;
        set
        {
            if (SetProperty(ref _unreadCount, value))
            {
                OnPropertyChanged(nameof(HasUnread));
            }
        }
    }

    public bool HasUnread => UnreadCount > 0;

    public EmailViewModel(
        IAccountStore accountStore,
        IEmailService emailService,
        IAIService aiService,
        IWindowService windowService,
        IAppStatusService appStatusService,
        IDbContextFactory<LocalCacheDbContext> dbContextFactory)
    {
        _accountStore = accountStore;
        _emailService = emailService;
        _aiService = aiService;
        _windowService = windowService;
        _appStatusService = appStatusService;
        _dbContextFactory = dbContextFactory;
        _logger = Log.ForContext<EmailViewModel>();

        RefreshCommand = new RelayCommand(async _ => await SyncAndReloadAsync(), _ => !IsLoading);
        DeleteEmailCommand = new RelayCommand<EmailMessage?>(async msg => await DeleteEmailAsync(msg));
        ToggleReadStatusCommand = new RelayCommand<EmailMessage?>(async msg => await ToggleReadStatusAsync(msg));
        ComposeEmailCommand = new RelayCommand(_ => ComposeEmail());
        SummarizeEmailCommand = new RelayCommand(async _ => await SummarizeEmailAsync(), _ => SelectedEmail != null && !IsSummarizing);
        GenerateSmartReplyCommand = new RelayCommand<string>(async tone => await GenerateSmartReplyAsync(tone ?? "resmi"), tone => SelectedEmail != null && !IsGeneratingReply);
        SelectFolderCommand = new RelayCommand<string>(folder => SelectedFolder = folder ?? "Inbox");
        ArchiveEmailCommand = new RelayCommand<EmailMessage?>(async msg => await ArchiveEmailAsync(msg));
        GenerateCustomReplyCommand = new RelayCommand(async _ => await GenerateCustomReplyAsync(), _ => SelectedEmail != null && !IsGeneratingReply && !string.IsNullOrWhiteSpace(CustomReplyInstruction));

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
            _logger.Error(ex, "Failed to load connected accounts for mail view.");
            IsLoading = false;
        }
    }

    private async Task LoadCachedEmailsAsync()
    {
        if (SelectedAccount == null)
        {
            Dispatcher.UIThread.Post(() => Emails.Clear());
            return;
        }

        try
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            
            var query = dbContext.CachedEmails
                .Where(e => e.AccountId == SelectedAccount.AccountId);

            if (SelectedFolder == "Vault")
            {
                query = query.Where(e => e.IsArchived);
            }
            else
            {
                query = query.Where(e => !e.IsArchived);
            }

            if (SelectedCategoryFilter != "Tümü")
            {
                query = query.Where(e => e.AiCategory == SelectedCategoryFilter);
            }

            if (SelectedReadStatusFilter == "Okunmamışlar")
            {
                query = query.Where(e => !e.IsRead);
            }
            else if (SelectedReadStatusFilter == "Okunmuşlar")
            {
                query = query.Where(e => e.IsRead);
            }

            if (SelectedDateFilter == "Bugün")
            {
                var today = DateTime.UtcNow.Date;
                query = query.Where(e => e.ReceivedDate >= today);
            }
            else if (SelectedDateFilter == "Bu Hafta")
            {
                var weekAgo = DateTime.UtcNow.Date.AddDays(-7);
                query = query.Where(e => e.ReceivedDate >= weekAgo);
            }
            else if (SelectedDateFilter == "Bu Ay")
            {
                var monthAgo = DateTime.UtcNow.Date.AddDays(-30);
                query = query.Where(e => e.ReceivedDate >= monthAgo);
            }

            if (!string.IsNullOrWhiteSpace(SenderFilter))
            {
                var lowerSender = SenderFilter.ToLower();
                query = query.Where(e => e.From.ToLower().Contains(lowerSender));
            }

            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                var lowerSearch = SearchQuery.ToLower();
                query = query.Where(e => 
                    e.Subject.ToLower().Contains(lowerSearch) || 
                    e.From.ToLower().Contains(lowerSearch) || 
                    e.Body.ToLower().Contains(lowerSearch));
            }

            var cachedList = await query
                .OrderByDescending(e => e.ReceivedDate)
                .ToListAsync();

            Dispatcher.UIThread.Post(() =>
            {
                Emails.Clear();
                int unreads = 0;
                foreach (var entity in cachedList)
                {
                    if (!entity.IsRead) unreads++;
                    Emails.Add(new EmailMessage
                    {
                        MessageId = entity.MessageId,
                        Subject = entity.Subject,
                        From = entity.From,
                        To = entity.To.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList(),
                        Body = entity.Body,
                        ReceivedDate = entity.ReceivedDate,
                        Provider = entity.Provider,
                        AccountId = entity.AccountId,
                        IsRead = entity.IsRead,
                        AiSummary = entity.AiSummary,
                        AiCategory = entity.AiCategory,
                        IsArchived = entity.IsArchived
                    });
                }
                UnreadCount = unreads;
            });
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to load cached emails.");
        }
    }

    private async Task SyncAndReloadAsync()
    {
        if (SelectedAccount == null) return;

        IsLoading = true;
        _appStatusService.PostUpdate($"{SelectedAccount.Email} e-postaları senkronize ediliyor...", isSyncing: true);

        try
        {
            await _emailService.SyncEmailsAsync(SelectedAccount);
            await LoadCachedEmailsAsync();
            _ = CategorizeEmailsAsync();
            _appStatusService.PostUpdate("E-posta senkronizasyonu tamamlandı.", isSyncing: false);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error syncing emails from server.");
            _appStatusService.PostUpdate($"E-posta senkronizasyon hatası: {ex.Message}", isSyncing: false);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task MarkAsReadAsync(EmailMessage msg)
    {
        if (SelectedAccount == null || msg.MessageId == null) return;
        
        try
        {
            msg.IsRead = true;
            await _emailService.MarkAsReadAsync(SelectedAccount, msg.MessageId);
            
            // Unread sayacını güncelle
            UnreadCount = Emails.Count(e => !e.IsRead);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to mark message as read.");
        }
    }

    private async Task ToggleReadStatusAsync(EmailMessage? msg)
    {
        if (SelectedAccount == null || msg == null || string.IsNullOrEmpty(msg.MessageId)) return;

        try
        {
            if (msg.IsRead)
            {
                await _emailService.MarkAsUnreadAsync(SelectedAccount, msg.MessageId);
                msg.IsRead = false;
            }
            else
            {
                await _emailService.MarkAsReadAsync(SelectedAccount, msg.MessageId);
                msg.IsRead = true;
            }

            // Arayüze bildirmek için listeyi tazele
            await LoadCachedEmailsAsync();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to toggle read status.");
        }
    }

    private async Task DeleteEmailAsync(EmailMessage? msg)
    {
        if (SelectedAccount == null || msg == null || string.IsNullOrEmpty(msg.MessageId)) return;

        var confirmed = await _windowService.ShowConfirmationDialogAsync("Bu e-postayı kalıcı olarak silmek istediğinize emin misiniz?");
        if (!confirmed) return;

        IsLoading = true;
        _appStatusService.PostUpdate("E-posta siliniyor...", isSyncing: true);

        try
        {
            await _emailService.DeleteEmailAsync(SelectedAccount, msg.MessageId);
            if (SelectedEmail == msg)
            {
                SelectedEmail = null;
            }
            await LoadCachedEmailsAsync();
            _appStatusService.PostUpdate("E-posta silindi.", isSyncing: false);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to delete email.");
            _appStatusService.PostUpdate($"E-posta silinemedi: {ex.Message}", isSyncing: false);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ArchiveEmailAsync(EmailMessage? msg)
    {
        if (SelectedAccount == null || msg == null || string.IsNullOrEmpty(msg.MessageId)) return;

        var confirmed = await _windowService.ShowConfirmationDialogAsync("Bu e-postayı sunucudan silip yerel güvenli arşive taşımak istediğinize emin misiniz?\n(E-posta sunucudan silinecek ancak yerel veritabanında saklanacaktır.)");
        if (!confirmed) return;

        IsLoading = true;
        _appStatusService.PostUpdate("E-posta yerel arşive taşınıyor...", isSyncing: true);

        try
        {
            await _emailService.DeleteEmailFromServerOnlyAsync(SelectedAccount, msg.MessageId);

            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            var entity = await dbContext.CachedEmails.FindAsync(SelectedAccount.AccountId, msg.MessageId);
            if (entity != null)
            {
                entity.IsArchived = true;
                entity.UpdatedAt = DateTime.UtcNow;
                await dbContext.SaveChangesAsync();
            }

            if (SelectedEmail == msg)
            {
                SelectedEmail = null;
            }

            await LoadCachedEmailsAsync();
            _appStatusService.PostUpdate("E-posta yerel arşive taşındı.", isSyncing: false);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to archive email.");
            _appStatusService.PostUpdate($"E-posta arşivlenemedi: {ex.Message}", isSyncing: false);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ComposeEmail()
    {
        _windowService.ShowNewEmailDialog(SelectedAccount?.AccountId);
    }

    private async Task SummarizeEmailAsync()
    {
        if (SelectedEmail == null || SelectedAccount == null || string.IsNullOrEmpty(SelectedEmail.MessageId)) return;

        IsSummarizing = true;
        _appStatusService.PostUpdate("Yapay zeka e-postayı özetliyor...", isSyncing: true);

        try
        {
            var summary = await _aiService.AnalyzeEmailAsync(SelectedEmail, "hybrid");

            if (!string.IsNullOrWhiteSpace(summary))
            {
                await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
                var entity = await dbContext.CachedEmails.FindAsync(SelectedEmail.AccountId, SelectedEmail.MessageId);
                if (entity != null)
                {
                    entity.AiSummary = summary;
                    entity.UpdatedAt = DateTime.UtcNow;
                    await dbContext.SaveChangesAsync();
                }

                SelectedEmail.AiSummary = summary;
                SelectedEmailSummary = summary;
                _appStatusService.PostUpdate("Özet oluşturuldu.", isSyncing: false);
            }
            else
            {
                _appStatusService.PostUpdate("Özet oluşturulamadı: Yapay zeka boş yanıt döndü.", isSyncing: false);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to summarize email via AI.");
            _appStatusService.PostUpdate($"Özet oluşturulamadı: {ex.Message}", isSyncing: false);
        }
        finally
        {
            IsSummarizing = false;
        }
    }

    private async Task GenerateSmartReplyAsync(string tone)
    {
        if (SelectedEmail == null || SelectedAccount == null) return;

        IsGeneratingReply = true;
        _appStatusService.PostUpdate($"Akıllı yanıt taslağı oluşturuluyor ({tone})...", isSyncing: true);

        try
        {
            var prompt = $@"
Lütfen aşağıdaki e-postaya yanıt olarak yazılacak, {tone} tonda profesyonel/samimi/kısa bir Türkçe e-posta yanıt taslağı oluştur.
SADECE yanıt taslağının gövde metnini döndür. Giriş (Örn: 'Tabii, işte resmi yanıt:') veya çıkış açıklamaları ekleme. Doğrudan e-posta metnini döndür.

Gelen E-posta Konusu: {SelectedEmail.Subject}
Gelen E-posta Göndereni: {SelectedEmail.From}
Gelen E-posta İçeriği:
{SelectedEmail.Body}";

            var replyText = await _aiService.GetResponseAsync(prompt, "hybrid");

            _appStatusService.PostUpdate("Akıllı yanıt taslağı hazırlandı.", isSyncing: false);

            if (!string.IsNullOrWhiteSpace(replyText))
            {
                var reSubject = SelectedEmail.Subject ?? string.Empty;
                if (!reSubject.StartsWith("Re:", StringComparison.OrdinalIgnoreCase) && !reSubject.StartsWith("Yanıt:", StringComparison.OrdinalIgnoreCase))
                {
                    reSubject = "Re: " + reSubject;
                }

                var toAddress = SelectedEmail.From ?? string.Empty;
                var emailStartIndex = toAddress.IndexOf('<');
                var emailEndIndex = toAddress.IndexOf('>');
                if (emailStartIndex != -1 && emailEndIndex != -1 && emailEndIndex > emailStartIndex)
                {
                    toAddress = toAddress.Substring(emailStartIndex + 1, emailEndIndex - emailStartIndex - 1);
                }

                _windowService.ShowNewEmailDialog(SelectedAccount.AccountId, toAddress, reSubject, replyText.Trim());
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to generate smart reply via AI.");
            _appStatusService.PostUpdate($"Akıllı yanıt oluşturulamadı: {ex.Message}", isSyncing: false);
        }
        finally
        {
            IsGeneratingReply = false;
        }
    }

    private async Task GenerateCustomReplyAsync()
    {
        if (SelectedEmail == null || SelectedAccount == null) return;
        if (string.IsNullOrWhiteSpace(CustomReplyInstruction)) return;

        IsGeneratingReply = true;
        _appStatusService.PostUpdate("Özel talimatla akıllı yanıt taslağı oluşturuluyor...", isSyncing: true);

        try
        {
            var prompt = $@"
Lütfen aşağıdaki e-postaya yanıt olarak yazılacak bir Türkçe e-posta yanıt taslağı oluştur.
Yanıt oluşturulurken şu özel talimata/kriterlere kesinlikle uyulmalıdır:
{CustomReplyInstruction}

SADECE yanıt taslağının gövde metnini döndür. Giriş (Örn: 'Tabii, işte yanıtınız:') veya çıkış açıklamaları ekleme. Doğrudan e-posta metnini döndür.

Gelen E-posta Konusu: {SelectedEmail.Subject}
Gelen E-posta Göndereni: {SelectedEmail.From}
Gelen E-posta İçeriği:
{SelectedEmail.Body}";

            var replyText = await _aiService.GetResponseAsync(prompt, "hybrid");

            _appStatusService.PostUpdate("Akıllı yanıt taslağı hazırlandı.", isSyncing: false);

            if (!string.IsNullOrWhiteSpace(replyText))
            {
                var reSubject = SelectedEmail.Subject ?? string.Empty;
                if (!reSubject.StartsWith("Re:", StringComparison.OrdinalIgnoreCase) && !reSubject.StartsWith("Yanıt:", StringComparison.OrdinalIgnoreCase))
                {
                    reSubject = "Re: " + reSubject;
                }

                var toAddress = SelectedEmail.From ?? string.Empty;
                var emailStartIndex = toAddress.IndexOf('<');
                var emailEndIndex = toAddress.IndexOf('>');
                if (emailStartIndex != -1 && emailEndIndex != -1 && emailEndIndex > emailStartIndex)
                {
                    toAddress = toAddress.Substring(emailStartIndex + 1, emailEndIndex - emailStartIndex - 1);
                }

                _windowService.ShowNewEmailDialog(SelectedAccount.AccountId, toAddress, reSubject, replyText.Trim());
                CustomReplyInstruction = string.Empty;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to generate custom smart reply via AI.");
            _appStatusService.PostUpdate($"Akıllı yanıt oluşturulamadı: {ex.Message}", isSyncing: false);
        }
        finally
        {
            IsGeneratingReply = false;
        }
    }

    private async Task CategorizeEmailsAsync()
    {
        if (SelectedAccount == null) return;

        List<EmailMessage> uncategorized;
        lock (Emails)
        {
            uncategorized = Emails.Where(e => string.IsNullOrEmpty(e.AiCategory)).ToList();
        }

        if (!uncategorized.Any()) return;

        _ = Task.Run(async () =>
        {
            foreach (var email in uncategorized)
            {
                try
                {
                    var prompt = $@"
Lütfen aşağıdaki e-posta konu ve içeriğine dayanarak bunu şu kategorilerden tam olarak BİRİ ile sınıflandır: İş, Fatura, Toplantı, Sosyal, Kişisel.
SADECE kategorinin adını tek bir kelime olarak yaz. Açıklama ekleme.

Konu: {email.Subject}
İçerik: {email.Body}";

                    var category = await _aiService.GetResponseAsync(prompt, "hybrid");
                    category = category?.Trim().Replace(".", "").Replace("*", "") ?? "Kişisel";

                    var validCategories = new[] { "İş", "Fatura", "Toplantı", "Sosyal", "Kişisel" };
                    var matchedCategory = validCategories.FirstOrDefault(c => string.Equals(c, category, StringComparison.OrdinalIgnoreCase)) ?? "Kişisel";

                    await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
                    var entity = await dbContext.CachedEmails.FindAsync(email.AccountId, email.MessageId);
                    if (entity != null)
                    {
                        entity.AiCategory = matchedCategory;
                        await dbContext.SaveChangesAsync();
                    }

                    Dispatcher.UIThread.Post(() =>
                    {
                        email.AiCategory = matchedCategory;
                    });
                }
                catch (Exception ex)
                {
                    _logger.Warning(ex, "Failed to categorize email {MessageId}", email.MessageId);
                }
            }

            Dispatcher.UIThread.Post(() =>
            {
                _ = LoadCachedEmailsAsync();
            });
        });
    }
}
