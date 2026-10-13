# Adım 4 — Ne Yaptık? Ne Öğrendik?

Bu adımda, videodan hareket noktalarını (landmark) çıkarma ve bu noktaları kullanarak açı hesaplama iş akışını (pipeline) oluşturduk.

Gerçek bir yapay zeka modeli yerine **Mock (Sahte)** bir motor kullandık. Bu sayede verinin frontend'e nasıl ulaşacağını ve video üzerinde nasıl gösterileceğini test ettik.

---

## 📂 Eklenen / Değişen Dosyalar

```
Archery.Web/
├── Models/
│   └── AnalysisResult.cs           ← DTO: SessionAnalysis, FrameData
├── Services/
│   ├── IPoseEstimator.cs           ← Yapay zeka motoru arayüzü
│   ├── MockPoseEstimator.cs        ← Sahte veri üreten motor
│   └── AnalysisService.cs          ← Orkestratör (Veriyi alır, hesaplar, kaydeder)
├── Controllers/
│   └── SessionsController.cs       ← Arka plan işlemi başlatma ve sonuç getirme
├── Pages/
│   └── Sessions/
│       └── Details.cshtml          ← Video oynatıcı ve Canvas katmanı
└── wwwroot/
    └── js/
        └── player.js               ← Video senkronizasyonu ve Canvas çizimi
```

---

## 🧠 Öğrenilen Kavramlar ve Mimari Kararlar

### 1. DTO (Data Transfer Object) Kullanımı
- **Neden?** `Archery.Core` kütüphanesindeki sınıfları (`LandmarkFrame`, `AngleResult` vb.) doğrudan JSON'a çevirip frontend'e göndermek iyi bir fikir değildir. Core katmanı değişirse API sözleşmemiz bozulur.
- **Çözüm:** `Archery.Web.Models` içinde `SessionAnalysis` ve `FrameData` adında saf veri taşıyıcı (record) sınıflar oluşturduk ve JSON'a bunları çevirdik.

### 2. Orkestratör Servis Modeli (`AnalysisService`)
Controller (API noktası) çok fazla iş yapmamalıdır. Bizim iş akışımız:
1. Videoyu al.
2. `IPoseEstimator` ile noktaları bul.
3. `Archery.Core.Angles.FrameAngleComputer` ile açıları hesapla.
4. Sonucu veritabanına (`ISessionStore`) kaydet.

Bu karmaşık mantığı `AnalysisService` adında tek bir sınıfa topladık. Controller sadece bu servisi çağırır.

### 3. Arka Planda İşlem (Fire-and-Forget)
Video analizi uzun sürer. Kullanıcıyı video yüklerken bekletmemek için analizi arka planda başlattık:
```csharp
// "Task.Run" ile işlem arka plana atılır ve kullanıcıya anında "Video Yüklendi" cevabı dönülür.
_ = Task.Run(() => _analysisService.RunAnalysisAsync(id, "mock.mp4"));
```
Kullanıcı daha sonra detay sayfasına giderek analizin bitip bitmediğini görür.

### 4. Video Üzerine Çizim Yapma (Canvas Overlay)
Frontend'de bir videonun üzerine çizim yapmak için en iyi yöntem HTML `<canvas>` kullanmaktır.
- `video` ve `canvas` aynı boyutta yapılır.
- CSS `position: absolute` ile üst üste bindirilir.
- `pointer-events: none` verilerek kullanıcının canvas'a değil, alttaki videoya (play/pause tuşlarına) tıklaması sağlanır.

### 5. Senkronizasyon (requestAnimationFrame)
Video oynarken saniyede 60 kez ekran yenilenir. Çizimlerimizin video ile senkronize kalması için:
```javascript
function drawLoop() {
    const currentTime = video.currentTime; // Videonun o anki saniyesi
    const currentFrame = findFrame(currentTime); // JSON'dan o saniyedeki veriyi bul
    drawSkeleton(currentFrame.keypoints); // Çizgileri çiz
    requestAnimationFrame(drawLoop); // Bir sonraki ekran yenilemesinde tekrar çağır
}
```
`requestAnimationFrame` tarayıcı performansını yormadan pürüzsüz animasyonlar yapmanın en iyi yoludur.

---

## 🎯 Sonraki Adım
Artık UI ve veri akışımız tamamen hazır. Bir sonraki adımda `MockPoseEstimator` yerine gerçek **MediaPipe (Python veya C# Wrapper)** entegrasyonu yapacağız!
