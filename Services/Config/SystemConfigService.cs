using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HanaMedia.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace HanaMedia.Services.Config
{
    public class SystemConfigService : ISystemConfigService
    {
        private readonly string _configFilePath;
        private readonly ILogger<SystemConfigService> _logger;
        private static readonly SemaphoreSlim _fileLock = new(1, 1);

        public SystemConfigService(IWebHostEnvironment env, ILogger<SystemConfigService> logger)
        {
            _logger = logger;
            var dir = Path.Combine(env.ContentRootPath, "App_Data");
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            _configFilePath = Path.Combine(dir, "system_config.json");
        }

        public async Task<SystemConfigModel> GetConfigAsync(CancellationToken cancellationToken = default)
        {
            await _fileLock.WaitAsync(cancellationToken);
            try
            {
                if (!File.Exists(_configFilePath))
                {
                    var defaultConfig = new SystemConfigModel();
                    var json = JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions { WriteIndented = true });
                    await File.WriteAllTextAsync(_configFilePath, json, cancellationToken);
                    return defaultConfig;
                }

                var content = await File.ReadAllTextAsync(_configFilePath, cancellationToken);
                var config = JsonSerializer.Deserialize<SystemConfigModel>(content);
                return config ?? new SystemConfigModel();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading system config file. Returning default config.");
                return new SystemConfigModel();
            }
            finally
            {
                _fileLock.Release();
            }
        }

        public async Task SaveConfigAsync(SystemConfigModel config, string updatedBy, CancellationToken cancellationToken = default)
        {
            await _fileLock.WaitAsync(cancellationToken);
            try
            {
                config.UpdatedAt = DateTime.Now;
                config.UpdatedBy = updatedBy;

                // Deduplicate and clean IP list
                config.WhitelistedIps = config.WhitelistedIps
                    .Where(ip => !string.IsNullOrWhiteSpace(ip))
                    .Select(ip => ip.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(_configFilePath, json, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving system config file.");
                throw;
            }
            finally
            {
                _fileLock.Release();
            }
        }

        public async Task<bool> AddIpRangeAsync(string ipRange, string updatedBy, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(ipRange)) return false;

            var cleanIp = ipRange.Trim();
            var config = await GetConfigAsync(cancellationToken);

            if (!config.WhitelistedIps.Contains(cleanIp, StringComparer.OrdinalIgnoreCase))
            {
                config.WhitelistedIps.Add(cleanIp);
                await SaveConfigAsync(config, updatedBy, cancellationToken);
                return true;
            }

            return false;
        }

        public async Task<bool> RemoveIpRangeAsync(string ipRange, string updatedBy, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(ipRange)) return false;

            var cleanIp = ipRange.Trim();
            var config = await GetConfigAsync(cancellationToken);

            var existing = config.WhitelistedIps.FirstOrDefault(ip => string.Equals(ip, cleanIp, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                config.WhitelistedIps.Remove(existing);
                await SaveConfigAsync(config, updatedBy, cancellationToken);
                return true;
            }

            return false;
        }
    }
}
