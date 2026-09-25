using System;
using System.IO;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace HanaMedia.Services;

/// <summary>
/// Interface cho xử lý ảnh: decode, resize, compress.
/// </summary>
public interface IImageProcessingService
{
    /// <summary>
    /// Xử lý ảnh: decode, resize, compress để đạt target size.
    /// </summary>
    /// <param name="rawBytes">Bytes gốc của ảnh.</param>
    /// <param name="mime">MIME type đã xác nhận từ data URL.</param>
    /// <param name="maxDimension">Kích thước tối đa (width/height). Default 1200.</param>
    /// <param name="targetBytes">Dung lượng mục tiêu sau nén (bytes). Default 500KB.</param>
    /// <param name="minQuality">Quality thấp nhất chấp nhận được. Default 60.</param>
    /// <returns>Cặp (processedBytes, finalMime).</returns>
    Task<(byte[] bytes, string mime)> ProcessImageAsync(
        byte[] rawBytes,
        string mime,
        int maxDimension = 1200,
        int targetBytes = 500 * 1024,
        int minQuality = 60);

    /// <summary>
    /// Kiểm tra xem bytes có phải là ảnh hợp lệ hay không (decode được).
    /// </summary>
    bool IsValidImage(byte[] bytes, string mime);

    /// <summary>
    /// Lấy MIME từ data URL base64 string.
    /// </summary>
    (byte[] bytes, string mime) DecodeBase64WithMime(string base64);
}

public class ImageProcessingService : IImageProcessingService
{
    public bool IsValidImage(byte[] bytes, string mime)
    {
        if (bytes == null || bytes.Length == 0) return false;
        try
        {
            using var ms = new MemoryStream(bytes);
            return Image.Identify(ms) != null;
        }
        catch
        {
            return false;
        }
    }

    public (byte[] bytes, string mime) DecodeBase64WithMime(string base64)
    {
        var s = base64.Trim();
        string mime = "image/jpeg";

        const string prefix = "base64,";
        var idx = s.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
        if (idx > 0)
        {
            var header = s.Substring(5, idx - 5);
            var semi = header.IndexOf(';');
            if (semi > 0) mime = header.Substring(0, semi).Trim();
            s = s.Substring(idx + prefix.Length);
        }

        byte[] bytes;
        try { bytes = Convert.FromBase64String(s); }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("Chuỗi base64 không hợp lệ: " + ex.Message);
        }

        return (bytes, mime);
    }

    public async Task<(byte[] bytes, string mime)> ProcessImageAsync(
        byte[] rawBytes,
        string mime,
        int maxDimension = 1200,
        int targetBytes = 500 * 1024,
        int minQuality = 60)
    {
        using var inputMs = new MemoryStream(rawBytes);

        Image image;
        try
        {
            image = await Image.LoadAsync(inputMs);
        }
        catch
        {
            throw new InvalidOperationException("Không thể xử lý ảnh đã chọn. Vui lòng thử ảnh khác.");
        }

        int origWidth = image.Width;
        int origHeight = image.Height;

        bool needsResize = origWidth > maxDimension || origHeight > maxDimension;
        if (needsResize)
        {
            int newWidth, newHeight;
            if (origWidth > origHeight)
            {
                newWidth = maxDimension;
                newHeight = (int)Math.Round((double)origHeight * maxDimension / origWidth);
            }
            else
            {
                newHeight = maxDimension;
                newWidth = (int)Math.Round((double)origWidth * maxDimension / origHeight);
            }
            image.Mutate(x => x.Resize(newWidth, newHeight, KnownResamplers.Lanczos3));
        }

        byte[] output;
        string finalMime;

        if (mime == "image/png")
        {
            finalMime = "image/png";
            using var outMs = new MemoryStream();
            var encoder = new PngEncoder
            {
                CompressionLevel = PngCompressionLevel.BestCompression
            };
            await image.SaveAsync(outMs, encoder);
            output = outMs.ToArray();
        }
        else if (mime == "image/webp")
        {
            finalMime = "image/webp";
            output = await EncodeWebp(image, targetBytes, minQuality);
        }
        else
        {
            finalMime = "image/jpeg";
            output = await EncodeJpeg(image, targetBytes, minQuality);
        }

        image.Dispose();
        return (output, finalMime);
    }

    private static async Task<byte[]> EncodeJpeg(Image image, int targetBytes, int minQuality)
    {
        int quality = 85;
        byte[] output;

        do
        {
            using var ms = new MemoryStream();
            var encoder = new JpegEncoder { Quality = quality };
            await image.SaveAsync(ms, encoder);
            output = ms.ToArray();

            if (output.Length <= targetBytes || quality <= minQuality)
                break;

            quality = Math.Max(minQuality, quality - 10);
        }
        while (output.Length > targetBytes && quality > minQuality);

        return output;
    }

    private static async Task<byte[]> EncodeWebp(Image image, int targetBytes, int minQuality)
    {
        int quality = 85;
        byte[] output;

        do
        {
            using var ms = new MemoryStream();
            var encoder = new WebpEncoder { Quality = quality };
            await image.SaveAsync(ms, encoder);
            output = ms.ToArray();

            if (output.Length <= targetBytes || quality <= minQuality)
                break;

            quality = Math.Max(minQuality, quality - 10);
        }
        while (output.Length > targetBytes && quality > minQuality);

        return output;
    }
}
