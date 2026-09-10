using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace HanaMedia.Services.Security
{
    public static class CidrHelper
    {
        public static string StripIpv6Mapping(string ip)
        {
            if (string.IsNullOrWhiteSpace(ip)) return string.Empty;
            var trimmed = ip.Trim();
            if (trimmed.StartsWith("::ffff:", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed.Substring(7);
            }
            return trimmed;
        }

        public static bool IsValidCidrOrIp(string? input, out string errorMessage, out string normalizedCidr)
        {
            errorMessage = string.Empty;
            normalizedCidr = string.Empty;

            if (string.IsNullOrWhiteSpace(input))
            {
                errorMessage = "Vui lòng nhập địa chỉ IP hoặc dải CIDR.";
                return false;
            }

            var trimmed = input.Trim();

            if (!trimmed.Contains('/'))
            {
                if (!IPAddress.TryParse(trimmed, out var ipAddr))
                {
                    errorMessage = $"Địa chỉ IP [{trimmed}] không hợp lệ.";
                    return false;
                }

                if (ipAddr.AddressFamily == AddressFamily.InterNetwork)
                {
                    normalizedCidr = $"{ipAddr}/32";
                    return true;
                }
                if (ipAddr.AddressFamily == AddressFamily.InterNetworkV6)
                {
                    normalizedCidr = $"{ipAddr}/128";
                    return true;
                }

                errorMessage = "Định dạng IP không được hỗ trợ.";
                return false;
            }

            var parts = trimmed.Split('/');
            if (parts.Length != 2)
            {
                errorMessage = "Cú pháp CIDR không hợp lệ (Ví dụ: 192.168.1.0/24).";
                return false;
            }

            if (!IPAddress.TryParse(parts[0].Trim(), out var networkIp))
            {
                errorMessage = $"Địa chỉ IP base [{parts[0]}] không hợp lệ.";
                return false;
            }

            if (!int.TryParse(parts[1].Trim(), CultureInfo.InvariantCulture, out var prefixLength))
            {
                errorMessage = $"Độ dài subnet mask [{parts[1]}] không hợp lệ.";
                return false;
            }

            if (networkIp.AddressFamily == AddressFamily.InterNetwork)
            {
                if (prefixLength is < 0 or > 32)
                {
                    errorMessage = $"Subnet mask IPv4 phải nằm trong khoảng từ 0 đến 32 (nhập: /{prefixLength}).";
                    return false;
                }

                var netAddr = GetNetworkAddress(networkIp, prefixLength);
                normalizedCidr = $"{netAddr}/{prefixLength}";
                return true;
            }

            if (networkIp.AddressFamily == AddressFamily.InterNetworkV6)
            {
                if (prefixLength is < 0 or > 128)
                {
                    errorMessage = $"Subnet mask IPv6 phải nằm trong khoảng từ 0 đến 128 (nhập: /{prefixLength}).";
                    return false;
                }

                normalizedCidr = $"{networkIp}/{prefixLength}";
                return true;
            }

            errorMessage = "Họ địa chỉ IP không được hỗ trợ.";
            return false;
        }

        public static IPAddress GetNetworkAddress(IPAddress address, int prefixLength)
        {
            var ipBytes = address.GetAddressBytes();

            if (address.AddressFamily == AddressFamily.InterNetwork && prefixLength >= 0 && prefixLength <= 32)
            {
                var ipValue = BitConverter.ToUInt32(ipBytes.Reverse().ToArray(), 0);
                var mask = prefixLength == 0 ? 0U : uint.MaxValue << (32 - prefixLength);
                var netValue = ipValue & mask;
                var netBytes = BitConverter.GetBytes(netValue).Reverse().ToArray();
                return new IPAddress(netBytes);
            }

            return address;
        }

        public static bool IsIpInRange(string rawClientIp, string cidr)
        {
            if (string.IsNullOrWhiteSpace(rawClientIp) || string.IsNullOrWhiteSpace(cidr))
                return false;

            var clientIpStr = StripIpv6Mapping(rawClientIp);
            var targetCidr = StripIpv6Mapping(cidr.Trim());

            if (!IPAddress.TryParse(clientIpStr, out var clientIp))
                return false;

            if (!targetCidr.Contains('/'))
            {
                return string.Equals(clientIpStr, targetCidr, StringComparison.OrdinalIgnoreCase);
            }

            var parts = targetCidr.Split('/');
            if (parts.Length != 2) return false;

            if (!IPAddress.TryParse(parts[0].Trim(), out var networkAddress))
                return false;

            if (!int.TryParse(parts[1].Trim(), CultureInfo.InvariantCulture, out var prefixLength))
                return false;

            if (clientIp.AddressFamily != networkAddress.AddressFamily)
                return false;

            if (clientIp.AddressFamily == AddressFamily.InterNetwork)
            {
                if (prefixLength is < 0 or > 32) return false;

                var ipBytes = clientIp.GetAddressBytes();
                var netBytes = networkAddress.GetAddressBytes();

                var ipValue = BitConverter.ToUInt32(ipBytes.Reverse().ToArray(), 0);
                var netValue = BitConverter.ToUInt32(netBytes.Reverse().ToArray(), 0);
                var mask = prefixLength == 0 ? 0U : uint.MaxValue << (32 - prefixLength);

                return (ipValue & mask) == (netValue & mask);
            }

            return false;
        }
    }
}
