using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HanaMedia.Models
{
    public static class IpRuleType
    {
        public const string Allow = "Allow";
        public const string Deny = "Deny";
    }

    [Table("ip_access_rules")]
    public class IpAccessRule
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        [Column("cidr")]
        public string Cidr { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        [Column("rule_type")]
        public string RuleType { get; set; } = IpRuleType.Allow;

        [Column("is_enabled")]
        public bool IsEnabled { get; set; } = true;

        [MaxLength(255)]
        [Column("description")]
        public string? Description { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        [MaxLength(100)]
        [Column("created_by")]
        public string? CreatedBy { get; set; }

        [MaxLength(100)]
        [Column("updated_by")]
        public string? UpdatedBy { get; set; }
    }
}
