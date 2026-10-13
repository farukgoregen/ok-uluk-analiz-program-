# Adım 2 — Ne Yaptık? Ne Öğrendik?

Bu belgede Adım 2'de oluşturulan ASP.NET Core Web API projesi,
kullanılan teknolojiler, kütüphaneler ve temel kavramlar açıklanmaktadır.

---

## 📂 Bu Adımda Eklenen Dosyalar

```
Archery.Web/
├── Archery.Web.csproj              ← Proje dosyası (ASP.NET Core Web API)
├── Program.cs                      ← Uygulama giriş noktası + API endpoint'leri
├── Models/
│   └── ArcherySession.cs           ← Analiz oturumu modeli + SessionStatus enum
├── Services/
│   ├── ISessionStore.cs            ← Oturum deposu arayüzü (interface)
│   ├── InMemorySessionStore.cs     ← Bellekte saklama (geçici)
│   ├── IVideoStorage.cs            ← Video depolama arayüzü
│   └── LocalVideoStorage.cs        ← Yerel dosya sisteminde saklama
└── uploads/                        ← Yüklenen videolar buraya kaydedilir
```

---

## 🛠️ Kullanılan Teknolojiler

### 1. ASP.NET Core (Web API)
- **Ne?** Microsoft'un web uygulamaları geliştirmek için açık kaynak çerçevesi.
- **Web API ne demek?** Tarayıcı/mobil uygulama gibi istemcilerin HTTP ile konuştuğu sunucu tarafı uygulama.
- İstemci → HTTP isteği gönderir (ör. `POST /api/sessions`) → Sunucu işler → JSON yanıt döner.
- 📖 https://learn.microsoft.com/en-us/aspnet/core/

### 2. Minimal API
- ASP.NET Core'un iki API yazım stili var:
  1. **Controller tabanlı:** Her endpoint grubu için bir sınıf (ör. `SessionsController`). Daha yapılandırılmış, büyük projeler için.
  2. **Minimal API:** `Program.cs`'de doğrudan `app.MapGet(...)`, `app.MapPost(...)` ile tanım. Daha az kod, hızlı başlangıç.
- Biz Minimal API kullandık çünkü küçük bir API ve hızlı ilerleme istiyoruz.

```csharp
// Minimal API örneği
app.MapGet("/api/sessions", async (ISessionStore store) =>
{
    var sessions = await store.GetAllAsync();
    return Results.Ok(sessions);
});
```

### 3. Swagger / OpenAPI (Swashbuckle)
- **Ne?** API'nin otomatik belgesi ve test arayüzü.
- Tarayıcıda `http://localhost:5050/swagger` adresine gidince tüm endpoint'leri görebilir ve test edebilirsin.
- **Swashbuckle:** .NET için Swagger implementasyonu.
- 📖 https://swagger.io/tools/swagger-ui/

---

## 📦 Kullanılan NuGet Paketleri

| Paket | Versiyon | Ne İşe Yarar |
|---|---|---|
| `Microsoft.AspNetCore.OpenApi` | 8.0.0 | API endpoint'lerini OpenAPI standardına göre tanımlar |
| `Swashbuckle.AspNetCore` | 6.5.0 | Swagger UI oluşturur — tarayıcıda API test arayüzü |

> **Not:** ASP.NET Core'un kendisi NuGet paketi olarak eklenmez, .NET SDK'nın bir parçasıdır
> (`Microsoft.NET.Sdk.Web`). `.csproj` dosyasındaki `Sdk` özniteliğinde belirtilir.

---

## 🧠 Temel Kavramlar

### HTTP Metotları
| Metot | Anlam | Bu Projede |
|---|---|---|
| `GET` | Veri getir (oku) | Oturumları listele, tek oturum getir |
| `POST` | Yeni veri oluştur | Oturum oluştur, video yükle |
| `PUT` | Mevcut veriyi güncelle | (Henüz yok) |
| `DELETE` | Veri sil | (Henüz yok) |

### HTTP Durum Kodları
| Kod | Anlam | Ne Zaman Kullanıyoruz |
|---|---|---|
| `200 OK` | İstek başarılı | GET sonuçları, video yükleme başarısı |
| `201 Created` | Yeni kaynak oluşturuldu | Yeni oturum oluşturma |
| `400 Bad Request` | İstek hatalı | Boş video dosyası |
| `404 Not Found` | Kaynak bulunamadı | Olmayan oturum ID'si |

### Dependency Injection (Bağımlılık Enjeksiyonu)
```csharp
// Kayıt (Program.cs'de):
builder.Services.AddSingleton<ISessionStore, InMemorySessionStore>();

// Kullanım (endpoint'te):
app.MapGet("/api/sessions", async (ISessionStore store) => { ... });
```
- **Ne?** Bir sınıfın ihtiyaç duyduğu bağımlılıkları dışarıdan (framework tarafından) alması.
- **Neden?** Endpoint `InMemorySessionStore`'u doğrudan bilmiyor, `ISessionStore` arayüzünü biliyor. Yarın veritabanı implementasyonu yazarsak sadece kayıt satırını değiştiririz, endpoint kodu değişmez.
- **Singleton:** Uygulama ömrü boyunca tek bir örnek oluşturulur. In-memory depo için mantıklı çünkü veri tek yerde olmalı.

### Interface (Arayüz) Neden Kullanıyoruz?

```
ISessionStore (arayüz)
    ↑ implements
InMemorySessionStore (şimdiki)     →  SqlSessionStore (ileride)
```

- Arayüz = "ne yapılacağını" tanımlar (sözleşme).
- Sınıf = "nasıl yapılacağını" tanımlar (implementasyon).
- Fayda: Implementasyonu değiştirmek kolay. Endpoint'ler arayüze bağlı, somut sınıfa değil.

### `async` / `await` (Asenkron Programlama)
```csharp
public async Task<ArcherySession> CreateAsync()
{
    // await = "bu işlem bitene kadar bekle, ama başka istekleri de işle"
    var session = await store.CreateAsync();
    return session;
}
```
- **Ne?** Uzun süren işlemler (dosya yazma, veritabanı sorgusu) sırasında sunucunun diğer istekleri de işleyebilmesi.
- **Neden `Task` dönüyor?** Asenkron metotlar `Task<T>` döner. `Task` = "gelecekte bir sonuç vereceğim" demek.
- In-memory depoda aslında gerçek asenkron yok (`Task.FromResult` kullanıyoruz), ama arayüzü asenkron tanımladık çünkü veritabanına geçince gerçekten asenkron olacak.

### `ConcurrentDictionary`
```csharp
private readonly ConcurrentDictionary<Guid, ArcherySession> _sessions = new();
```
- Normal `Dictionary` aynı anda birden fazla istek gelince sorun çıkarabilir (thread-safe değil).
- `ConcurrentDictionary` çok iş parçacıklı (multi-threaded) ortamda güvenli.
- Web sunucusu birden fazla isteği aynı anda işlediği için bu gerekli.

### `IFormFile` (Dosya Yükleme)
```csharp
app.MapPost("/api/sessions/{id}/video", async (Guid id, IFormFile video, ...) => { ... });
```
- `IFormFile` = HTTP `multipart/form-data` ile gönderilen dosya.
- `video.OpenReadStream()` ile dosyanın içeriğine erişilir.
- `video.FileName`, `video.Length` gibi özellikler mevcut.

### Route Parametreleri
```csharp
app.MapGet("/api/sessions/{id:guid}", async (Guid id, ISessionStore store) => { ... });
```
- `{id:guid}` = URL'deki `id` parametresi, GUID formatında olmalı.
- `:guid` kısıtlaması sayesinde `abc123` gibi geçersiz ID'ler otomatik reddedilir (404).
- ASP.NET Core parametreyi otomatik olarak `Guid` tipine dönüştürür.

---

## 🔧 Çalıştırma Komutları

```bash
# Web API'yi başlat
dotnet run --project Archery.Web

# Belirli port ile başlat
dotnet run --project Archery.Web --urls "http://localhost:5050"

# Tarayıcıda Swagger UI'ı aç
# http://localhost:5050/swagger
```

### PowerShell ile API Testi
```powershell
# Yeni oturum oluştur
Invoke-RestMethod -Uri http://localhost:5050/api/sessions -Method Post

# Tüm oturumları listele
Invoke-RestMethod -Uri http://localhost:5050/api/sessions -Method Get

# Tek oturum getir
Invoke-RestMethod -Uri http://localhost:5050/api/sessions/{GUID} -Method Get

# Video yükle
$form = @{ video = Get-Item "C:\path\to\video.mp4" }
Invoke-RestMethod -Uri http://localhost:5050/api/sessions/{GUID}/video -Method Post -Form $form
```

---

## 🏗️ Mimari Kararlar ve Nedenleri

| Karar | Neden |
|---|---|
| Minimal API (Controller yok) | Az endpoint, hızlı başlangıç, az boilerplate |
| `ISessionStore` / `IVideoStorage` arayüzleri | İleride veritabanı/blob storage'a geçiş kolay |
| In-memory depolama | Veritabanı kararı erteleniyor, hızlı prototipleme |
| `Singleton` yaşam döngüsü | In-memory depo tek olmalı, veri kaybı olmamalı |
| `ConcurrentDictionary` | Web sunucusu multi-threaded, thread-safety gerekli |
| `uploads/{sessionId}/` klasör yapısı | Her oturumun videosu izole, çakışma yok |
| Swagger sadece Development'ta | Üretimde API belgesini herkese açmak güvenlik riski |

---

## 📋 API Özeti

| Metot | Yol | Açıklama | Yanıt |
|---|---|---|---|
| `POST` | `/api/sessions` | Yeni oturum oluştur | `201` + oturum JSON |
| `GET` | `/api/sessions` | Tüm oturumları listele | `200` + oturum dizisi |
| `GET` | `/api/sessions/{id}` | Tek oturum getir | `200` veya `404` |
| `POST` | `/api/sessions/{id}/video` | Video yükle | `200`, `400` veya `404` |
