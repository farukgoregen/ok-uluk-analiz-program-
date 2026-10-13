using Archery.Web.Models;
using Archery.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Archery.Web.Pages;

public class IndexModel : PageModel
{
    private readonly ISessionStore _store;

    public IndexModel(ISessionStore store)
    {
        _store = store;
    }

    public IReadOnlyList<ArcherySession> Sessions { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Sessions = await _store.GetAllAsync();
    }

    /// <summary>Durum enum'una göre CSS badge sınıfı döndürür.</summary>
    public string GetBadgeClass(SessionStatus status) => status switch
    {
        SessionStatus.Created => "badge-created",
        SessionStatus.VideoUploaded => "badge-uploaded",
        SessionStatus.Analyzing => "badge-analyzing",
        SessionStatus.Completed => "badge-completed",
        SessionStatus.Failed => "badge-failed",
        _ => "badge-created"
    };

    /// <summary>Durum enum'unu Türkçe metne çevirir.</summary>
    public string GetStatusText(SessionStatus status) => status switch
    {
        SessionStatus.Created => "Oluşturuldu",
        SessionStatus.VideoUploaded => "Video Yüklendi",
        SessionStatus.Analyzing => "Analiz Ediliyor",
        SessionStatus.Completed => "Tamamlandı",
        SessionStatus.Failed => "Hata",
        _ => "Bilinmiyor"
    };
}
