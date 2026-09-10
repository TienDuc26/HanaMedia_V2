using System;
using System.Collections.Generic;
using HanaMedia.Services.Security;

namespace HanaMedia.Models
{
    public class SystemConfigModel
    {
        public int MaxUploadSizeMb { get; set; } = 50;
        public int SessionTimeoutMinutes { get; set; } = 30;
        public bool IpRestrictionEnabled { get; set; } = true;
        public bool AllowLoopback { get; set; } = false;

        public IpAccessDecision CurrentClientIpInfo { get; set; } = new();

        public List<IpRuleDto> WhitelistRules { get; set; } = new();
        public List<IpRuleDto> BlacklistRules { get; set; } = new();

        // Compatibility property for legacy calls if any
        public List<string> WhitelistedIps { get; set; } = new();

        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        public string? UpdatedBy { get; set; }
    }
}
