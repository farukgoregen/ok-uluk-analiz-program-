# Adım 3 — Ne Yaptık? Ne Öğrendik?

Bu belgede Adım 3'te oluşturulan Razor Pages frontend, kullanılan
teknolojiler ve kavramlar açıklanmaktadır.

---

## 📂 Bu Adımda Eklenen / Değişen Dosyalar

```
Archery.Web/
├── Pages/
│   ├── _ViewImports.cshtml              ← Tag helper ve namespace tanımları
│   ├── _ViewStart.cshtml                ← Varsayılan layout referansı
│   ├── Index.cshtml                     ← Ana sayfa (oturum listesi)
│   ├── Index.cshtml.cs                  ← Ana sayfa code-behind
│   ├── Shared/
│   │   └── _Layout.cshtml               ← Ana şablon (navbar + footer)
│   └── Sessions/
│       ├── Create.cshtml                ← Video yükleme formu
│       ├── Create.cshtml.cs             ← Video yükleme code-behind
│       ├── Details.cshtml               ← Oturum detayı
│       └── Details.cshtml.cs            ← Oturum detay code-behind
├── wwwroot/
│   └── css/
│       └── site.css                     ← Koyu tema stil dosyası
├── Controllers/
│   └── SessionsController.cs           ← MVC Controller (önceki adımda)
└── Program.cs                           ← Güncellendi (AddRazorPages eklendi)
```

---

## 🛠️ Kullanılan Teknolojiler

### 1. Razor Pages
- **Ne?** ASP.NET Core'un sayfa odaklı web arayüzü çerçevesi.
- MVC'nin View + Controller kısmını birleştiren yaklaşım.
- Her sayfa iki dosyadan oluşur:
  - `.cshtml` — HTML + C# (Razor sözdizimi ile)
  - `.cshtml.cs` — Code-behind (iş mantığı)

```
MVC:
  Controller → View → Model (ayrı dosyalar, karmaşık)

Razor Pages:
  Page.cshtml + Page.cshtml.cs (her şey bir arada, daha basit)
```

### 2. Razor Sözdizimi
- HTML içine C# kodu gömme yöntemi.
- `@` ile C# ifadeleri yazılır:

```html
<!-- Değişken yazdırma -->
<p>@Model.Session.VideoFileName</p>

<!-- Koşul -->
@if (Model.Sessions.Count == 0)
{
    <p>Henüz oturum yok</p>
}

<!-- Döngü -->
@foreach (var session in Model.Sessions)
{
    <tr><td>@session.Id</td></tr>
}
```

### 3. Tag Helpers
- HTML etiketlerini C# ile zenginleştiren yardımcılar.

```html
<!-- asp-page: Razor Page'e bağlantı oluşturur -->
<a asp-page="/Sessions/Details" asp-route-id="@session.Id">Detay</a>

<!-- asp-for: Model özelliğine form bileşeni bağlar -->
<input asp-for="VideoFile" />

<!-- method="post" + enctype: Dosya yükleme formu -->
<form method="post" enctype="multipart/form-data">
```

---

## 🧠 Temel Kavramlar

### Layout (Şablon) Sistemi
```
_Layout.cshtml (ana şablon)
├── <nav> ... </nav>        ← Her sayfada ortak
├── @RenderBody()           ← Sayfa içeriği buraya gelir
└── <footer> ... </footer>  ← Her sayfada ortak
```

- `_Layout.cshtml`: Tüm sayfaların ortak yapısı (navbar, footer, CSS/JS).
- `@RenderBody()`: Her sayfanın kendi içeriği bu noktaya eklenir.
- `_ViewStart.cshtml`: Tüm sayfalar için varsayılan layout'u belirler.
- `_ViewImports.cshtml`: Tüm sayfalarda kullanılacak namespace ve tag helper'ları tanımlar.

### PageModel (Code-Behind)
```csharp
public class IndexModel : PageModel
{
    // Dependency Injection — constructor'dan alınır
    private readonly ISessionStore _store;
    public IndexModel(ISessionStore store) { _store = store; }

    // Sayfanın kullanacağı veri
    public IReadOnlyList<ArcherySession> Sessions { get; private set; }

    // GET isteği geldiğinde çalışır
    public async Task OnGetAsync()
    {
        Sessions = await _store.GetAllAsync();
    }

    // POST isteği geldiğinde çalışır
    public async Task<IActionResult> OnPostAsync() { ... }
}
```

- `OnGetAsync()`: Sayfa ilk açıldığında çalışır (HTTP GET).
- `OnPostAsync()`: Form gönderildiğinde çalışır (HTTP POST).
- `PageModel` sınıfı MVC'deki Controller + ViewModel birleşimi gibi.

### [BindProperty] Attribute
```csharp
[BindProperty]
public IFormFile? VideoFile { get; set; }
```
- Form'dan gelen veriyi otomatik olarak bu özelliğe bağlar.
- HTML'deki `name="VideoFile"` ile C#'taki `VideoFile` eşleşir.

### Statik Dosyalar (wwwroot)
```
wwwroot/
└── css/
    └── site.css
```
- `wwwroot/` klasöründeki dosyalar tarayıcıya doğrudan sunulur.
- `UseStaticFiles()` middleware'i ile aktif edilir.
- HTML'de `~/css/site.css` şeklinde referans verilir (`~` = wwwroot kökü).

### CSS Değişkenleri (Custom Properties)
```css
:root {
    --bg-primary: #0f1117;
    --accent: #6c63ff;
}

.navbar {
    background: var(--bg-primary);  /* değişkeni kullan */
}
```
- Renkleri tek yerde tanımla, her yerde kullan.
- Tema değiştirmek = sadece `:root` içindeki değerleri değiştirmek.

### Responsive Tasarım
```css
@media (max-width: 640px) {
    .nav-content {
        flex-direction: column;  /* mobilde dikey yerleşim */
    }
}
```
- `@media` sorguları ile ekran boyutuna göre stil değişir.
- Masaüstü: yatay menü, mobil: dikey menü.

---

## 🏗️ Razor Pages vs MVC Controller

Bu projede **ikisini birlikte** kullanıyoruz:

| Özellik | Razor Pages | MVC Controller |
|---|---|---|
| Kullanım | Sayfa bazlı UI | API endpoint'leri |
| Dosya yapısı | `.cshtml` + `.cshtml.cs` | `Controllers/XController.cs` |
| Routing | Dosya yolu tabanlı (`/Sessions/Create`) | Attribute tabanlı (`[Route("api/[controller]")]`) |
| Veri dönüşü | HTML sayfası | JSON |
| Bu projede | Kullanıcı arayüzü | REST API (Swagger) |

Her ikisi de aynı servis katmanını (`ISessionStore`, `IVideoStorage`) kullanır — aynı veriyi paylaşırlar.

---

## 🔧 Çalıştırma

```bash
dotnet run --project Archery.Web --urls "http://localhost:5050"
```

| Sayfa | URL |
|---|---|
| Ana Sayfa | http://localhost:5050 |
| Yeni Analiz | http://localhost:5050/Sessions/Create |
| Oturum Detayı | http://localhost:5050/Sessions/Details?id={GUID} |
| Swagger API | http://localhost:5050/swagger |
