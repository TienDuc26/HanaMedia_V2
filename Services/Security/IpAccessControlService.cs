using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HanaMedia.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HanaMedia.Services.Security
{
    public class IpAccessControlService : IIpAccessControlService
    {
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly IConfiguration _configuration;
        private readonly IClientIpResolver _ipResolver;
        private readonly ILogger<IpAccessControlService> _logger;

        private const string CacheKeyRules = "IpAccessControl_Rules_Cache";

        public IpAccessControlService(
            ApplicationDbContext context,
            IMemoryCache cache,
            IConfiguration configuration,
            IClientIpResolver ipResolver,
            ILogger<IpAccessControlService> logger)
        {
            _context = context;
            _cache = cache;
            _configuration = configuration;
            _ipResolver = ipResolver;
            _logger = logger;
        }

        public async Task<bool> IsRestrictionEnabledAsync()
        {
            var value = _configuration["Security:IpRestriction:Enabled"];
            if (bool.TryParse(value, out var enabled))
            {
                return enabled;
            }
            return true;
        }

        public async Task<bool> IsAllowLoopbackEnabledAsync()
        {
            var value = _configuration["Security:IpRestriction:AllowLoopback"];
            if (bool.TryParse(value, out var allowLoopback))
            {
                return allowLoopback;
            }
            return false;
        }

        public async Task<List<IpRuleDto>> GetRulesAsync(CancellationToken cancellationToken = default)
        {
            if (_cache.TryGetValue(CacheKeyRules, out List<IpRuleDto>? cachedRules) && cachedRules != null)
            {
                return cachedRules;
            }

            var entities = await _context.IpAccessRules
                .AsNoTracking()
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync(cancellationToken);

            var dtos = entities.Select(r => new IpRuleDto
            {
                Id = r.Id,
                Cidr = r.Cidr,
                RuleType = r.RuleType,
                IsEnabled = r.IsEnabled,
                Description = r.Description,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt,
                CreatedBy = r.CreatedBy,
                UpdatedBy = r.UpdatedBy
            }).ToList();

            _cache.Set(CacheKeyRules, dtos, TimeSpan.FromMinutes(10));
            return dtos;
        }

        public async Task<IpAccessDecision> EvaluateAccessAsync(string clientIp, CancellationToken cancellationToken = default)
        {
            var cleanIp = CidrHelper.StripIpv6Mapping(clientIp);
            var isRestrictionEnabled = await IsRestrictionEnabledAsync();
            var allowLoopback = await IsAllowLoopbackEnabledAsync();

            var decision = new IpAccessDecision
            {
                ClientIp = cleanIp,
                EvaluatedNetwork = $"{cleanIp}/32"
            };

            if (!isRestrictionEnabled)
            {
                decision.IsAllowed = true;
                decision.Reason = "Tường lửa IP hiện đang TẮT (Hệ thống cho phép mọi IP đăng nhập)";
                return decision;
            }

            if (allowLoopback && _ipResolver.IsLoopbackIp(cleanIp))
            {
                decision.IsAllowed = true;
                decision.MatchedRule = "Loopback (127.0.0.1 / ::1)";
                decision.RuleType = "Allow";
                decision.Reason = "Cho phép tự động địa chỉ Localhost/Loopback theo cấu hình hệ thống";
                return decision;
            }

            var rules = await GetRulesAsync(cancellationToken);
            var activeRules = rules.Where(r => r.IsEnabled).ToList();

            // 1. BLACKLIST priority check (DENY rules evaluated first)
            var activeDenyRules = activeRules
                .Where(r => string.Equals(r.RuleType, IpRuleType.Deny, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var rule in activeDenyRules)
            {
                if (CidrHelper.IsIpInRange(cleanIp, rule.Cidr))
                {
                    decision.IsAllowed = false;
                    decision.MatchedRule = rule.Cidr;
                    decision.RuleType = IpRuleType.Deny;
                    decision.Reason = $"IP [{cleanIp}] bị CHẶN do trùng khớp quy tắc Blacklist [{rule.Cidr}]";
                    return decision;
                }
            }

            // 2. WHITELIST check (ALLOW rules)
            var activeAllowRules = activeRules
                .Where(r => string.Equals(r.RuleType, IpRuleType.Allow, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var rule in activeAllowRules)
            {
                if (CidrHelper.IsIpInRange(cleanIp, rule.Cidr))
                {
                    decision.IsAllowed = true;
                    decision.MatchedRule = rule.Cidr;
                    decision.RuleType = IpRuleType.Allow;
                    decision.Reason = $"IP [{cleanIp}] ĐƯỢC PHÉP truy cập do trùng khớp quy tắc Whitelist [{rule.Cidr}]";
                    return decision;
                }
            }

            // 3. Neither matched or Whitelist empty => DENY
            decision.IsAllowed = false;
            decision.Reason = activeAllowRules.Any()
                ? $"IP [{cleanIp}] bị từ chối truy cập vì không nằm trong danh sách Whitelist được phép"
                : $"Danh sách Whitelist rỗng. Không có IP nào được phép truy cập ngoại trừ cấu hình bypass.";
            return decision;
        }

        public async Task<(bool Success, string Message, IpRuleDto? Rule)> AddRuleAsync(
            string cidrInput,
            string ruleType,
            string? description,
            string updatedBy,
            CancellationToken cancellationToken = default)
        {
            var normalizedType = string.Equals(ruleType, IpRuleType.Deny, StringComparison.OrdinalIgnoreCase)
                ? IpRuleType.Deny
                : IpRuleType.Allow;

            if (!CidrHelper.IsValidCidrOrIp(cidrInput, out var validationError, out var normalizedCidr))
            {
                return (false, validationError, null);
            }

            var existingRule = await _context.IpAccessRules
                .FirstOrDefaultAsync(r => r.Cidr == normalizedCidr && r.RuleType == normalizedType, cancellationToken);

            if (existingRule != null)
            {
                var typeLabel = normalizedType == IpRuleType.Deny ? "Blacklist" : "Whitelist";
                return (false, $"Dải IP [{normalizedCidr}] đã tồn tại trong danh sách {typeLabel}.", null);
            }

            var newEntity = new IpAccessRule
            {
                Cidr = normalizedCidr,
                RuleType = normalizedType,
                IsEnabled = true,
                Description = description,
                CreatedAt = DateTime.Now,
                CreatedBy = updatedBy,
                UpdatedBy = updatedBy
            };

            _context.IpAccessRules.Add(newEntity);
            await _context.SaveChangesAsync(cancellationToken);

            InvalidateCache();

            var dto = new IpRuleDto
            {
                Id = newEntity.Id,
                Cidr = newEntity.Cidr,
                RuleType = newEntity.RuleType,
                IsEnabled = newEntity.IsEnabled,
                Description = newEntity.Description,
                CreatedAt = newEntity.CreatedAt,
                CreatedBy = newEntity.CreatedBy
            };

            var typeName = normalizedType == IpRuleType.Deny ? "Blacklist (Chặn)" : "Whitelist (Cho phép)";
            return (true, $"Đã thêm [{normalizedCidr}] vào danh sách {typeName} thành công!", dto);
        }

        public async Task<(bool Success, string Message)> ToggleRuleAsync(
            int ruleId,
            string updatedBy,
            CancellationToken cancellationToken = default)
        {
            var rule = await _context.IpAccessRules.FindAsync(new object[] { ruleId }, cancellationToken);
            if (rule == null)
            {
                return (false, "Không tìm thấy quy tắc IP cần thay đổi.");
            }

            rule.IsEnabled = !rule.IsEnabled;
            rule.UpdatedAt = DateTime.Now;
            rule.UpdatedBy = updatedBy;

            await _context.SaveChangesAsync(cancellationToken);
            InvalidateCache();

            var statusText = rule.IsEnabled ? "BẬT" : "TẮT";
            return (true, $"Đã {statusText} quy tắc [{rule.Cidr}] thành công.");
        }

        public async Task<(bool Success, string Message)> DeleteRuleAsync(
            int ruleId,
            string updatedBy,
            CancellationToken cancellationToken = default)
        {
            var rule = await _context.IpAccessRules.FindAsync(new object[] { ruleId }, cancellationToken);
            if (rule == null)
            {
                return (false, "Không tìm thấy quy tắc IP cần xóa.");
            }

            _context.IpAccessRules.Remove(rule);
            await _context.SaveChangesAsync(cancellationToken);
            InvalidateCache();

            return (true, $"Đã xóa quy tắc [{rule.Cidr}] khỏi danh sách thành công.");
        }

        public void InvalidateCache()
        {
            _cache.Remove(CacheKeyRules);
        }
    }
}
