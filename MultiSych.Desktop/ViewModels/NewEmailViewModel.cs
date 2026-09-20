using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

namespace MultiSych.Desktop.ViewModels;

public class EmailAttachmentViewModel : ViewModelBase
{
    public string FileName { get; set; } = string.Empty;
    public string MimeType { get; set; } = "application/octet-stream";
    public string FileId { get; set; } = string.Empty;
    public long Size { get; set; }
}

public class NewEmailViewModel : ViewModelBase
{
    private readonly IAccountStore _accountStore;
    private readonly IEmailService _emailService;
    private readonly IAppStatusService _appStatusService;
    private readonly ILogger _logger;

    private AccountCredentials? _selectedAccount;
    private string _selectedAccountId = string.Empty;
    private string _toAddress = string.Empty;
    private string _subject = string.Empty;
    private string _bodyText = string.Empty;
    private bool _isSending;
    private string _statusMessage = "Ready";

    public ObservableCollection<AccountCredentials> Accounts { get; } = new();
    public Action? CloseAction { get; set; }

    public ICommand SendEmailCommand { get; }
    public ICommand CancelCommand { get; }

    public AccountCredentials? SelectedAccount
    {
        get => _selectedAccount;
        set => SetProperty(ref _selectedAccount, value);
    }

    public string SelectedAccountId
    {
        get => _selectedAccountId;
        set
        {
            if (SetProperty(ref _selectedAccountId, value))
            {
                SelectedAccount = Accounts.FirstOrDefault(a => a.AccountId == value);
            }
        }
    }

    public string ToAddress
    {
        get => _toAddress;
        set => SetProperty(ref _toAddress, value);
    }

    public string Subject
    {
        get => _subject;
        set => SetProperty(ref _subject, value);
    }

    public string BodyText
    {
        get => _bodyText;
        set => SetProperty(ref _bodyText, value);
    }

    public bool IsSending
    {
        get => _isSending;
        set => SetProperty(ref _isSending, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    private readonly IDbContextFactory<LocalCacheDbContext> _dbContextFactory;
    private readonly IStorageService _storageService;

    private bool _isAttachmentPanelVisible;
    private string _fileSearchQuery = string.Empty;
    private CloudFileEntity? _selectedAvailableFile;

    public ObservableCollection<EmailAttachmentViewModel> Attachments { get; } = new();
    public ObservableCollection<CloudFileEntity> AvailableFiles { get; } = new();

    public bool IsAttachmentPanelVisible
    {
        get => _isAttachmentPanelVisible;
        set => SetProperty(ref _isAttachmentPanelVisible, value);
    }

    public string FileSearchQuery
    {
        get => _fileSearchQuery;
        set
        {
            if (SetProperty(ref _fileSearchQuery, value))
            {
                _ = LoadAvailableFilesAsync();
            }
        }
    }

    public CloudFileEntity? SelectedAvailableFile
    {
        get => _selectedAvailableFile;
        set => SetProperty(ref _selectedAvailableFile, value);
    }

    public ICommand AddCloudAttachmentCommand { get; }
    public ICommand CancelAttachmentCommand { get; }
    public ICommand ConfirmAttachmentCommand { get; }
    public ICommand RemoveAttachmentCommand { get; }

    public NewEmailViewModel(
        IAccountStore accountStore,
        IEmailService emailService,
        IAppStatusService appStatusService,
        IDbContextFactory<LocalCacheDbContext> dbContextFactory,
        IStorageService storageService)
    {
        _accountStore = accountStore;
        _emailService = emailService;
        _appStatusService = appStatusService;
        _dbContextFactory = dbContextFactory;
        _storageService = storageService;
        _logger = Log.ForContext<NewEmailViewModel>();

        SendEmailCommand = new RelayCommand(async _ => await SendEmailAsync(), _ => CanSend());
        CancelCommand = new RelayCommand(_ => CloseAction?.Invoke());

        AddCloudAttachmentCommand = new RelayCommand(async _ => await ShowAttachmentPanelAsync());
        CancelAttachmentCommand = new RelayCommand(_ => IsAttachmentPanelVisible = false);
        ConfirmAttachmentCommand = new RelayCommand(_ => ConfirmAttachment());
        RemoveAttachmentCommand = new RelayCommand<EmailAttachmentViewModel>(att => RemoveAttachment(att));

        _ = LoadAccountsAsync();
    }

    private async Task LoadAccountsAsync()
    {
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
                    SelectedAccountId = Accounts.First().AccountId ?? string.Empty;
                }
            });
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to load accounts for new email composition.");
        }
    }

    private bool CanSend()
    {
        return SelectedAccount != null && 
               !string.IsNullOrWhiteSpace(ToAddress) && 
               ToAddress.Contains("@") && 
               !string.IsNullOrWhiteSpace(Subject) && 
               !IsSending;
    }

    private async Task SendEmailAsync()
    {
        if (SelectedAccount == null) return;

        IsSending = true;
        StatusMessage = "Sending email...";
        _appStatusService.PostUpdate("E-posta gönderiliyor...", isSyncing: true);

        try
        {
            var attachmentsList = new List<EmailAttachment>();
            
            // Ekleri indir
            foreach (var att in Attachments)
            {
                StatusMessage = $"{att.FileName} indiriliyor...";
                using var stream = await _storageService.DownloadFileAsync(SelectedAccount, att.FileId);
                using var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream);
                
                attachmentsList.Add(new EmailAttachment
                {
                    FileName = att.FileName,
                    MimeType = att.MimeType,
                    Size = att.Size,
                    Content = memoryStream.ToArray()
                });
            }

            var emailMsg = new EmailMessage
            {
                From = SelectedAccount.Email,
                To = new System.Collections.Generic.List<string> { ToAddress },
                Subject = Subject,
                Body = BodyText,
                IsHtml = false,
                ReceivedDate = DateTime.UtcNow,
                Provider = SelectedAccount.Provider,
                AccountId = SelectedAccount.AccountId,
                Attachments = attachmentsList.Count > 0 ? attachmentsList : null
            };

            await _emailService.SendEmailAsync(SelectedAccount, emailMsg);
            
            _appStatusService.PostUpdate("E-posta başarıyla gönderildi.", isSyncing: false);
            
            Dispatcher.UIThread.Post(() =>
            {
                CloseAction?.Invoke();
            });
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to send email from {From} to {To}", SelectedAccount.Email, ToAddress);
            StatusMessage = $"Failed: {ex.Message}";
            _appStatusService.PostUpdate($"E-posta gönderilemedi: {ex.Message}", isSyncing: false);
        }
        finally
        {
            IsSending = false;
        }
    }

    private async Task ShowAttachmentPanelAsync()
    {
        FileSearchQuery = string.Empty;
        SelectedAvailableFile = null;
        IsAttachmentPanelVisible = true;
        await LoadAvailableFilesAsync();
    }

    private async Task LoadAvailableFilesAsync()
    {
        if (SelectedAccount == null) return;
        try
        {
            using var db = await _dbContextFactory.CreateDbContextAsync();
            var query = db.CloudFiles
                .Where(f => f.AccountId == SelectedAccount.AccountId && !f.IsDirectory);

            if (!string.IsNullOrWhiteSpace(FileSearchQuery))
            {
                var lower = FileSearchQuery.ToLower();
                query = query.Where(f => f.FileName.ToLower().Contains(lower));
            }

            // SQLite-level optimization for file extension filtering to bypass heavy client-side processing
            query = query.Where(f => 
                f.FileName.EndsWith(".docx") || f.FileName.EndsWith(".doc") || f.FileName.EndsWith(".xlsx") || 
                f.FileName.EndsWith(".xls") || f.FileName.EndsWith(".pptx") || f.FileName.EndsWith(".ppt") || 
                f.FileName.EndsWith(".pdf") || f.FileName.EndsWith(".txt") || f.FileName.EndsWith(".md") || 
                f.FileName.EndsWith(".csv"));

            var filteredFiles = await query.ToListAsync();

            Dispatcher.UIThread.Post(() =>
            {
                AvailableFiles.Clear();
                foreach (var f in filteredFiles)
                {
                    AvailableFiles.Add(f);
                }
            });
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to load available files for attachments.");
        }
    }

    private void ConfirmAttachment()
    {
        if (SelectedAvailableFile == null) return;

        var existing = Attachments.FirstOrDefault(a => a.FileId == SelectedAvailableFile.FileId);
        if (existing == null)
        {
            Attachments.Add(new EmailAttachmentViewModel
            {
                FileId = SelectedAvailableFile.FileId,
                FileName = SelectedAvailableFile.FileName,
                MimeType = SelectedAvailableFile.MimeType,
                Size = SelectedAvailableFile.FileSize
            });
        }

        IsAttachmentPanelVisible = false;
    }

    private void RemoveAttachment(EmailAttachmentViewModel? att)
    {
        if (att != null)
        {
            Attachments.Remove(att);
        }
    }
}
