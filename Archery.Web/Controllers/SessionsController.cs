using Archery.Web.Models;
using Archery.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Archery.Web.Controllers;

/// <summary>
/// Analiz oturumlarını yöneten controller.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SessionsController : ControllerBase
{
    private readonly ISessionStore _store;
    private readonly IVideoStorage _storage;
    private readonly AnalysisService _analysisService;

    public SessionsController(ISessionStore store, IVideoStorage storage, AnalysisService analysisService)
    {
        _store = store;
        _storage = storage;
        _analysisService = analysisService;
    }

    public class CreateSessionRequest
    {
    }

    /// <summary>Yeni bir analiz oturumu oluşturur.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ArcherySession), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create()
    {
        var session = await _store.CreateAsync();
        return CreatedAtAction(nameof(GetById), new { id = session.Id }, session);
    }

    /// <summary>Tüm oturumları listeler.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ArcherySession>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var sessions = await _store.GetAllAsync();
        return Ok(sessions.OrderByDescending(s => s.CreatedAt));
    }

    /// <summary>Belirtilen ID'ye sahip oturumu getirir.</summary>

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ArcherySession), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var session = await _store.GetAsync(id);
        if (session is null)
            return NotFound(new { Hata = "Oturum bulunamadı", Id = id });

        return Ok(session);
    }

    /// <summary>Oturuma video yükler.</summary>
    [HttpPost("{id:guid}/video")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UploadVideo(Guid id, IFormFile video)
    {
        // Oturum var mı?
        var session = await _store.GetAsync(id);
        if (session is null)
            return NotFound(new { Hata = "Oturum bulunamadı", Id = id });

        // Dosya kontrolü
        if (video.Length == 0)
            return BadRequest(new { Hata = "Boş dosya yüklenemez" });

        // Videoyu kaydet
        await using var stream = video.OpenReadStream();
        await _storage.SaveAsync(id, stream, video.FileName);

        // Oturumu güncelle
        session.VideoFileName = video.FileName;
        session.Status = SessionStatus.VideoUploaded;
        await _store.UpdateAsync(session);

        // Fiziksel dosya yolunu al (Python scriptinin okuyabilmesi için)
        var physicalPath = await _storage.GetFilePathAsync(id);
        if (physicalPath != null)
        {
            // Arka planda analizi başlat (fire-and-forget)
            _ = Task.Run(() => _analysisService.RunAnalysisAsync(id, physicalPath));
        }

        return Ok(new
        {
            Mesaj = "Video başarıyla yüklendi",
            DosyaAdi = video.FileName,
            Boyut = video.Length,
            Durum = session.Status.ToString()
        });
    }

    /// <summary>Belirtilen oturumun analiz sonuçlarını getirir.</summary>
    [HttpGet("{id:guid}/analysis")]
    [ProducesResponseType(typeof(SessionAnalysis), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAnalysis(Guid id)
    {
        var analysis = await _store.GetAnalysisAsync(id);
        if (analysis is null)
            return NotFound(new { Hata = "Analiz bulunamadı veya henüz tamamlanmadı", Id = id });

        return Ok(analysis);
    }

    /// <summary>Belirtilen oturumun video dosyasını getirir.</summary>
    [HttpGet("{id:guid}/video")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetVideo(Guid id)
    {
        var session = await _store.GetAsync(id);
        if (session is null || session.VideoFileName is null)
            return NotFound();

        var stream = await _storage.GetAsync(id);
        if (stream is null)
            return NotFound();

        // Basitçe stream ediyoruz. İleride content type daha dinamik olabilir.
        return File(stream, "video/mp4", enableRangeProcessing: true);
    }
}
