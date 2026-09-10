using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace HanaMedia.Services.Security
{
    public class ClientIpResolver : IClientIpResolver
    {
        private readonly IConfiguration _configuration;

        public ClientIpResolver(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string GetClientIp(HttpContext? context)
        {
            if (context == null) return "127.0.0.1";

            var trustProxy = _configuration.GetValue<bool>("Security:IpRestriction:TrustForwardedHeaders", false);

            if (trustProxy)
            {
                var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(forwardedFor))
                {
                    var clientIpCandidate = forwardedFor.Split(',')[0].Trim();
                    var cleanCandidate = CidrHelper.StripIpv6Mapping(clientIpCandidate);
                    if (IPAddress.TryParse(cleanCandidate, out _))
                    {
                        return cleanCandidate;
                    }
                }

                var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(realIp))
                {
                    var cleanRealIp = CidrHelper.StripIpv6Mapping(realIp.Trim());
                    if (IPAddress.TryParse(cleanRealIp, out _))
                    {
                        return cleanRealIp;
                    }
                }
            }

            var remoteIp = context.Connection.RemoteIpAddress;
            if (remoteIp == null) return "unknown";

            return CidrHelper.StripIpv6Mapping(remoteIp.ToString());
        }

        public bool IsLoopbackIp(string rawIp)
        {
            if (string.IsNullOrWhiteSpace(rawIp)) return false;
            var cleanIp = CidrHelper.StripIpv6Mapping(rawIp);

            if (string.Equals(cleanIp, "127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(cleanIp, "::1", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(cleanIp, "localhost", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (IPAddress.TryParse(cleanIp, out var ipAddress))
            {
                return IPAddress.IsLoopback(ipAddress);
            }

            return false;
        }
    }
}
