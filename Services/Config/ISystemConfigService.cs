using System.Threading;
using System.Threading.Tasks;
using HanaMedia.Models;

namespace HanaMedia.Services.Config
{
    public interface ISystemConfigService
    {
        Task<SystemConfigModel> GetConfigAsync(CancellationToken cancellationToken = default);
        Task SaveConfigAsync(SystemConfigModel config, string updatedBy, CancellationToken cancellationToken = default);
        Task<bool> AddIpRangeAsync(string ipRange, string updatedBy, CancellationToken cancellationToken = default);
        Task<bool> RemoveIpRangeAsync(string ipRange, string updatedBy, CancellationToken cancellationToken = default);
    }
}
