using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;

namespace HanaMedia.Services;

/// <summary>
/// Helper lưu ảnh đại diện nhân viên từ chuỗi base64 vào wwwroot/uploads/employees/.
/// Backend tự động resize + compress ảnh trước khi lưu.
/// Hỗ trợ cả data URL ("data:image/png;base64,...") và chuỗi base64 thuần.
/// </summary>
public class EmployeeAvatarService
{
    /// <summary>
    /// Dung lượng file gốc tối đa backend chấp nhận nhận vào (10 MB).
    /// Backend sẽ resize/compress nếu cần, không reject ngay.
    /// </summary>
    private const long MaxUploadBytes = 10 * 1024 * 1024;

    /// <summary>
    /// Kích thước tối đa mỗi cạnh sau resize.
    /// </summary>
    private const int MaxDimension = 1200;

    /// <summary>
    /// Dung lượng mục tiêu sau khi nén (500 KB).
    /// </summary>
    private const int TargetBytes = 500 * 1024;

    /// <summary>
    /// Quality thấp nhất chấp nhận được.
    /// </summary>
    private const int MinQuality = 60;

    private static readonly string[] AllowedMimeTypes =
    {
        "image/jpeg", "image/jpg", "image/png", "image/webp"
    };

    private readonly IWebHostEnvironment _env;
    private readonly IImageProcessingService _imgProcessor;

    public EmployeeAvatarService(
        IWebHostEnvironment env,
        IImageProcessingService imgProcessor)
    {
        _env = env;
        _imgProcessor = imgProcessor;
    }

    /// <summary>
    /// Lưu avatar: decode base64 → validate → resize/compress → ghi file.
    /// Trả về URL public (/uploads/employees/xxx.ext) hoặc null nếu input null/rỗng.
    /// Throw InvalidOperationException nếu có lỗi.
    /// </summary>
    public async Task<string?> SaveAsync(int employeeId, string? avatarBase64)
    {
        if (string.IsNullOrWhiteSpace(avatarBase64))
            return null;

        var (rawBytes, mime) = _imgProcessor.DecodeBase64WithMime(avatarBase64);

        if (rawBytes.Length == 0)
            throw new InvalidOperationException("Dữ liệu ảnh rỗng.");

        if (rawBytes.Length > MaxUploadBytes)
            throw new InvalidOperationException($"Ảnh quá lớn. Vui lòng chọn ảnh nhỏ hơn {MaxUploadBytes / 1024 / 1024} MB.");

        if (!AllowedMimeTypes.Contains(mime))
            throw new InvalidOperationException("Định dạng ảnh không hỗ trợ (chỉ chấp nhận JPG, PNG, WEBP).");

        if (!_imgProcessor.IsValidImage(rawBytes, mime))
            throw new InvalidOperationException("File không phải là ảnh hợp lệ.");

        // Process: resize + compress
        var (processedBytes, finalMime) = await _imgProcessor.ProcessImageAsync(
            rawBytes, mime, MaxDimension, TargetBytes, MinQuality);

        var ext = finalMime switch
        {
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => ".jpg"
        };

        var uploadsRoot = Path.Combine(
            _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"),
            "uploads", "employees");
        Directory.CreateDirectory(uploadsRoot);

        // Sinh tên file an toàn với GUID
        var fileName = $"emp_{employeeId}_{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(uploadsRoot, fileName);

        // Ghi file mới trước
        await File.WriteAllBytesAsync(fullPath, processedBytes);

        // Xoá avatar cũ (nếu có) — sau khi file mới đã ghi thành công
        var oldFiles = Directory.GetFiles(uploadsRoot, $"emp_{employeeId}_*")
            .Where(f => !f.EndsWith(fileName));
        foreach (var oldPath in oldFiles)
        {
            try { File.Delete(oldPath); } catch { /* bỏ qua nếu bị lock */ }
        }

        return $"/uploads/employees/{fileName}";
    }
}
