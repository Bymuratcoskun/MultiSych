namespace MultiSych.Services.Interfaces;

/// <summary>
/// Cloud Service Interface
/// Base interface for cloud provider integrations (Mail, Drive, Calendar)
/// </summary>
public interface ICloudService
{
    /// <summary>
    /// Get the provider name
    /// </summary>
    string ProviderName { get; }
}
