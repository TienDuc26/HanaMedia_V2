using Microsoft.AspNetCore.Http;
using System.Net;

namespace HanaMedia.Services.Security
{
    public interface IClientIpResolver
    {
        string GetClientIp(HttpContext? context);
        bool IsLoopbackIp(string ip);
    }
}
