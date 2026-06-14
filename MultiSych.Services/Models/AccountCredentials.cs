using System;
using System.Collections.Generic;

namespace MultiSych.Services.Models
{
    public class AccountCredentials
    {
        public string? AccountId { get; set; }
        public string? Email { get; set; }
        public string? Provider { get; set; }
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public DateTime ExpiresAt { get; set; }
        public string? Scopes { get; set; }
        public DateTime CreatedAt { get; set; }
        public Dictionary<string, object>? AdditionalProperties { get; set; }
    }
}
