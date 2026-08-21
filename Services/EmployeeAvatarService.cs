using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;

namespace HanaMedia.Services
{
    /// <summary>
    /// Helper lưu ảnh đại diện nhân viên từ chuỗi base64 vào wwwroot/uploads/employees/.
    /// Hỗ trợ cả data URL ("data:image/png;base64,...") và chuỗi base64 thuần.
    /// </summary>
    public class EmployeeAvatarService
    {
        private const string UploadFolder = "uploads/employees";
        private const long MaxBytes = 2 * 1024 * 1024; // 2 MB
        private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
        private static readonly string[] AllowedMimeTypes =
        {
            "image/jpeg", "image/jpg", "image/png", "image/webp"
        };

        private readonly IWebHostEnvironment _env;

        public EmployeeAvatarService(IWebHostEnvironment env)
        {
            _env = env;
        }

        /// <summary>
        /// Giải mã base64, ghi file vào wwwroot/uploads/employees/{employeeId}_{timestamp}.ext,
        /// trả về URL public (/uploads/employees/xxx.ext).
        /// Trả null nếu input null/rỗng (FE không muốn đổi avatar).
        /// Throw InvalidOperationException nếu base64 không hợp lệ / quá lớn / sai MIME.
        /// </summary>
        public async Task<string?> SaveAsync(int employeeId, string? avatarBase64)
        {
            if (string.IsNullOrWhiteSpace(avatarBase64))
                return null;

            var (bytes, mime) = DecodeBase64(avatarBase64);

            if (bytes.Length == 0)
                throw new InvalidOperationException("Dữ liệu ảnh rỗng.");
            if (bytes.Length > MaxBytes)
                throw new InvalidOperationException(
                    $"Ảnh vượt quá dung lượng cho phép ({MaxBytes / 1024 / 1024} MB).");

            if (!AllowedMimeTypes.Contains(mime))
                throw new InvalidOperationException(
                    "Định dạng ảnh không hỗ trợ (chỉ chấp nhận JPG, PNG, WEBP).");

            var ext = mime switch
            {
                "image/png" => ".png",
                "image/webp" => ".webp",
                _ => ".jpg"
            };

            var uploadsRoot = Path.Combine(_env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), UploadFolder);
            Directory.CreateDirectory(uploadsRoot);

            // Xoá file avatar cũ của nhân viên này (nếu có)
            foreach (var oldExt in AllowedExtensions)
            {
                var oldPath = Path.Combine(uploadsRoot, $"emp_{employeeId}{oldExt}");
                if (System.IO.File.Exists(oldPath))
                {
                    try { System.IO.File.Delete(oldPath); } catch { /* bỏ qua nếu đang bị lock */ }
                }
            }

            var fileName = $"emp_{employeeId}_{DateTime.UtcNow:yyyyMMddHHmmss}{ext}";
            var fullPath = Path.Combine(uploadsRoot, fileName);
            await System.IO.File.WriteAllBytesAsync(fullPath, bytes);

            return $"/{UploadFolder}/{fileName}";
        }

        private static (byte[] bytes, string mime) DecodeBase64(string input)
        {
            var s = input.Trim();
            string mime = "image/jpeg";

            // Tách data URL: "data:image/png;base64,iVBOR..."
            const string prefix = "base64,";
            var idx = s.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
            if (idx > 0)
            {
                var header = s.Substring(5, idx - 5); // sau "data:"
                var semi = header.IndexOf(';');
                if (semi > 0) mime = header.Substring(0, semi).Trim();
                s = s.Substring(idx + prefix.Length);
            }

            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(s);
            }
            catch (FormatException ex)
            {
                throw new InvalidOperationException("Chuỗi base64 không hợp lệ: " + ex.Message);
            }

            return (bytes, mime);
        }
    }
}
