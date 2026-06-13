using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace MultiSych.Services.Data;

public class AccountCredentialEntity : BaseEntity
{
    [Key]
    public string AccountId { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Provider { get; set; }
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string? AdditionalPropertiesJson { get; set; }
    
    private Dictionary<string, object>? _additionalPropertiesCache;

    [NotMapped]
    public Dictionary<string, object> AdditionalProperties
    {
        get
        {
            if (_additionalPropertiesCache == null)
            {
                if (string.IsNullOrWhiteSpace(AdditionalPropertiesJson))
                {
                    _additionalPropertiesCache = new Dictionary<string, object>();
                }
                else
                {
                    _additionalPropertiesCache = JsonSerializer.Deserialize<Dictionary<string, object>>(AdditionalPropertiesJson) ?? new Dictionary<string, object>();
                }
            }
            return _additionalPropertiesCache;
        }
        set
        {
            _additionalPropertiesCache = value;
            AdditionalPropertiesJson = value == null ? null : JsonSerializer.Serialize(value);
        }
    }
}
