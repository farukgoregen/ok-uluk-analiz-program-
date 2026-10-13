namespace Archery.Web.Services;

/// <summary>
/// Videoları yerel dosya sisteminde saklayan implementasyon.
/// Her oturum için <c>uploads/{sessionId}/</c> alt klasörü oluşturulur.
/// Üretimde blob storage vb. ile değiştirilecek.
/// </summary>
public class LocalVideoStorage : IVideoStorage
{
    private readonly string _basePath;

    public LocalVideoStorage(string basePath)
    {
        _basePath = basePath;
        Directory.CreateDirectory(_basePath);
    }

    public async Task<string> SaveAsync(Guid sessionId, Stream videoStream, string fileName)
    {
        // Her oturum için ayrı klasör
        var sessionDir = Path.Combine(_basePath, sessionId.ToString());
        Directory.CreateDirectory(sessionDir);

        // Güvenli dosya adı: sadece dosya adı, yol bileşeni yok
        var safeFileName = Path.GetFileName(fileName);
        var filePath = Path.Combine(sessionDir, safeFileName);

        await using var fileStream = File.Create(filePath);
        await videoStream.CopyToAsync(fileStream);

        return filePath;
    }

    public Task<Stream?> GetAsync(Guid sessionId)
    {
        var filePath = GetFilePath(sessionId);
        if (filePath is null || !File.Exists(filePath))
            return Task.FromResult<Stream?>(null);

        return Task.FromResult<Stream?>(File.OpenRead(filePath));
    }

    public Task<string?> GetFilePathAsync(Guid sessionId)
    {
        var filePath = GetFilePath(sessionId);
        if (filePath is null || !File.Exists(filePath))
            return Task.FromResult<string?>(null);

        return Task.FromResult<string?>(filePath);
    }

    private string? GetFilePath(Guid sessionId)
    {
        var sessionDir = Path.Combine(_basePath, sessionId.ToString());

        if (!Directory.Exists(sessionDir))
            return null;

        // Klasördeki ilk video dosyasını döndür
        var files = Directory.GetFiles(sessionDir);
        if (files.Length == 0)
            return null;

        return files[0];
    }
}
