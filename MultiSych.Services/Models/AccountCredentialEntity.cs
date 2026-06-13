using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace MultiSych.Services.Models
{
    public class AccountCredentialEntity
    {
        public int Id { get; set; }
        public string AccountId { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Provider { get; set; }
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public DateTime ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? AdditionalPropertiesJson { get; set; }

        private Dictionary<string, object>? _additionalPropertiesCache;

        [NotMapped]
        public Dictionary<string, object>? AdditionalProperties
        {
            get
            {
                if (_additionalPropertiesCache == null && !string.IsNullOrWhiteSpace(AdditionalPropertiesJson))
                {
                    _additionalPropertiesCache = JsonSerializer.Deserialize<Dictionary<string, object>>(AdditionalPropertiesJson);
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
}
