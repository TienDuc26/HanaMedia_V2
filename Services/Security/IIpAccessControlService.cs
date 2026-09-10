using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HanaMedia.Models;

namespace HanaMedia.Services.Security
{
    public class IpAccessDecision
    {
        public bool IsAllowed { get; set; }
        public string MatchedRule { get; set; } = string.Empty;
        public string RuleType { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string ClientIp { get; set; } = string.Empty;
        public string EvaluatedNetwork { get; set; } = string.Empty;
    }

    public class IpRuleDto
    {
        public int Id { get; set; }
        public string Cidr { get; set; } = string.Empty;
        public string RuleType { get; set; } = IpRuleType.Allow;
        public bool IsEnabled { get; set; }
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
    }

    public interface IIpAccessControlService
    {
        Task<IpAccessDecision> EvaluateAccessAsync(string clientIp, CancellationToken cancellationToken = default);
        Task<List<IpRuleDto>> GetRulesAsync(CancellationToken cancellationToken = default);
        Task<(bool Success, string Message, IpRuleDto? Rule)> AddRuleAsync(string cidrInput, string ruleType, string? description, string updatedBy, CancellationToken cancellationToken = default);
        Task<(bool Success, string Message)> ToggleRuleAsync(int ruleId, string updatedBy, CancellationToken cancellationToken = default);
        Task<(bool Success, string Message)> DeleteRuleAsync(int ruleId, string updatedBy, CancellationToken cancellationToken = default);
        Task<bool> IsRestrictionEnabledAsync();
        Task<bool> IsAllowLoopbackEnabledAsync();
        void InvalidateCache();
    }
}
