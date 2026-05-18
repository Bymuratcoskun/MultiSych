using System;
using System.ComponentModel.DataAnnotations;

namespace MultiSych.Services.Data
{
    public class AppSecretEntity
    {
        [Key]
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; }
    }
}