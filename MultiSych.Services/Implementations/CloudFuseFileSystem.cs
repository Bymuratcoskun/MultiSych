using System;
using Microsoft.EntityFrameworkCore;
using MultiSych.Services.Data;
using MultiSych.Services.Interfaces;
using Serilog;

namespace MultiSych.Services.Implementations;

/// <summary>
/// Linux/macOS FUSE desteği bu derleme için devre dışı bırakıldı.
/// </summary>
public class CloudFuseFileSystem
{
    private readonly string _accountId;
    private readonly IStorageService _storageService;
    private readonly IDbContextFactory<LocalCacheDbContext> _dbContextFactory;
    private readonly ILogger _logger = Log.ForContext<CloudFuseFileSystem>();

    public CloudFuseFileSystem(string accountId, IStorageService storageService, IDbContextFactory<LocalCacheDbContext> dbContextFactory)
    {
        _accountId = accountId;
        _storageService = storageService;
        _dbContextFactory = dbContextFactory;
    }
}
