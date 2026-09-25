using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;

namespace HanaMedia.Services
{
    public class UserMediaService
    {
        private const string AvatarFolder = "uploads/users/avatars";
        private const string QrFolder = "uploads/users/qrcodes";

        // RAW upload limit (file gốc). Backend sẽ resize/compress sau.
        private const long AvatarRawMaxBytes = 25 * 1024 * 1024; // 25 MB
        private const long QrRawMaxBytes = 25 * 1024 * 1024; // 25 MB

        private const int MaxDimension = 1200;       // Avatar: resize tối đa 1200px
        private const int TargetStoredBytes = 1024 * 1024; // Avatar target: ~1 MB
        private const int MinQuality = 60;

        private static readonly string[] ImageMime = { "image/jpeg", "image/jpg", "image/png", "image/webp" };
        private static readonly string[] ImageExt = { ".jpg", ".jpeg", ".png", ".webp" };

        private readonly IWebHostEnvironment _env;
        private readonly IImageProcessingService _imgProcessor;

        public UserMediaService(IWebHostEnvironment env, IImageProcessingService imgProcessor)
        {
            _env = env;
            _imgProcessor = imgProcessor;
        }

        public async Task<string?> SaveAvatarAsync(int userId, string? base64)
        {
            return await SaveImageAsync(userId, base64, AvatarFolder, "avatar", isAvatar: true);
        }

        public async Task<string?> SaveQrCodeAsync(int userId, string? base64)
        {
            return await SaveImageAsync(userId, base64, QrFolder, "qr", isAvatar: false);
        }

        private async Task<string?> SaveImageAsync(int userId, string? base64, string folder, string prefix, bool isAvatar)
        {
            if (string.IsNullOrWhiteSpace(base64))
                return null;

            var (rawBytes, mime) = _imgProcessor.DecodeBase64WithMime(base64);

            if (rawBytes.Length == 0)
                throw new InvalidOperationException("Dữ liệu ảnh rỗng.");

            var rawMax = isAvatar ? AvatarRawMaxBytes : QrRawMaxBytes;
            var fileTypeName = isAvatar ? "Ảnh đại diện" : "Ảnh QR nhận lương";

            if (rawBytes.Length > rawMax)
                throw new InvalidOperationException(
                    $"{fileTypeName} vượt quá {rawMax / 1024 / 1024} MB. Vui lòng chọn ảnh nhỏ hơn.");

            if (!ImageMime.Contains(mime))
                throw new InvalidOperationException("Định dạng không hỗ trợ (chỉ JPG, PNG, WEBP).");

            if (!_imgProcessor.IsValidImage(rawBytes, mime))
                throw new InvalidOperationException("File không phải là ảnh hợp lệ.");

            // Xử lý: resize + compress cho Avatar; giữ nét cho QR
            byte[] processedBytes;
            string finalMime;
            if (isAvatar)
            {
                (processedBytes, finalMime) = await _imgProcessor.ProcessImageAsync(
                    rawBytes, mime, MaxDimension, TargetStoredBytes, MinQuality);
            }
            else
            {
                // QR: resize nhẹ (max 1500), quality cao (>=85), lossless cho PNG
                (processedBytes, finalMime) = await ProcessQrAsync(rawBytes, mime);
            }

            var ext = finalMime switch
            {
                "image/png" => ".png",
                "image/webp" => ".webp",
                _ => ".jpg"
            };

            var root = Path.Combine(_env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), folder);
            Directory.CreateDirectory(root);

            // Ghi file mới trước
            var fileName = $"{prefix}_{userId}_{Guid.NewGuid():N}{ext}";
            var fullPath = Path.Combine(root, fileName);
            await File.WriteAllBytesAsync(fullPath, processedBytes);

            // Xoá file cũ (nếu có) sau khi file mới đã ghi thành công
            var oldFiles = Directory.GetFiles(root, $"{prefix}_{userId}_*")
                .Where(f => !f.Equals(fullPath, StringComparison.OrdinalIgnoreCase));
            foreach (var old in oldFiles)
            {
                try { File.Delete(old); } catch { /* ignore lock */ }
            }
            // Xoá cả file format cũ (theo extent nếu chưa có guid)
            foreach (var oldExt in ImageExt)
            {
                var oldPath = Path.Combine(root, $"{prefix}_{userId}{oldExt}");
                if (File.Exists(oldPath) && !oldPath.Equals(fullPath, StringComparison.OrdinalIgnoreCase))
                {
                    try { File.Delete(oldPath); } catch { /* ignore */ }
                }
            }

            return $"/{folder}/{fileName}";
        }

        private async Task<(byte[] bytes, string mime)> ProcessQrAsync(byte[] rawBytes, string mime)
        {
            // QR: giữ max 1500px để đủ nét scan; resize nhẹ, quality cao
            const int qrMaxDim = 1500;
            if (mime == "image/png")
            {
                // PNG: giữ lossless, tối ưu qua ImageSharp với BestCompression
                using var inputMs = new MemoryStream(rawBytes);
                using var image = await SixLabors.ImageSharp.Image.LoadAsync(inputMs);
                if (image.Width > qrMaxDim || image.Height > qrMaxDim)
                {
                    int newWidth, newHeight;
                    if (image.Width > image.Height) { newWidth = qrMaxDim; newHeight = (int)(image.Height * (double)qrMaxDim / image.Width); }
                    else { newHeight = qrMaxDim; newWidth = (int)(image.Width * (double)qrMaxDim / image.Height); }
                    image.Mutate(x => x.Resize(newWidth, newHeight, SixLabors.ImageSharp.Processing.KnownResamplers.Lanczos3));
                }
                using var outMs = new MemoryStream();
                await image.SaveAsync(outMs, new SixLabors.ImageSharp.Formats.Png.PngEncoder
                {
                    CompressionLevel = SixLabors.ImageSharp.Formats.Png.PngCompressionLevel.BestCompression
                });
                return (outMs.ToArray(), "image/png");
            }
            // JPEG/WEBP: dùng quality cao 90
            return await _imgProcessor.ProcessImageAsync(rawBytes, mime, qrMaxDim, 2 * 1024 * 1024, 85);
        }
    }
}
