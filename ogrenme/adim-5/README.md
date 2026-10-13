# Adım 5 — Ne Yaptık? Ne Öğrendik?

Bu adımda, C# tabanlı sistemimize **Python** tabanlı bir Yapay Zeka (MediaPipe) motorunu nasıl entegre ettiğimizi öğrendik.

---

## 📂 Eklenen / Değişen Dosyalar

```
okçuluk/
├── scripts/
│   └── extract_pose.py                  ← MediaPipe kullanan Python betiği
├── Archery.Web/
│   ├── Services/
│   │   ├── IVideoStorage.cs             ← GetFilePathAsync eklendi
│   │   ├── LocalVideoStorage.cs         ← GetFilePathAsync eklendi
│   │   └── PythonMediaPipeEstimator.cs  ← Python'u çağıran C# sınıfı
│   ├── Controllers/
│   │   └── SessionsController.cs        ← Gerçek dosya yolunu servise iletiyor
│   ├── wwwroot/js/
│   │   └── player.js                    ← Video ve Canvas çözünürlük orantılaması eklendi
│   └── Program.cs                       ← PythonMediaPipeEstimator DI kaydı yapıldı
```

---

## 🧠 Öğrenilen Kavramlar ve Mimari Kararlar

### 1. Dil Bağımsız Mikroservis / İşlem (Process) Mimarisi
- **Neden?** Yapay Zeka (AI) ve Görüntü İşleme ekosistemi Python'da çok güçlü ve kararlıdır (MediaPipe, OpenCV). C# için yazılmış kütüphaneler genellikle geriden gelir veya kurulumları zordur.
- **Çözüm:** Python'da küçük bir script (`extract_pose.py`) yazdık. C# uygulamasından `System.Diagnostics.Process` kullanarak bu scripti arka planda çağırdık.
- **Veri Akışı:** C# -> Video Yolu -> Python (MediaPipe) -> Geçici JSON Dosyası -> C# (Deserialization).

### 2. Landmark (Eklem) Eşleştirme (Mapping)
- MediaPipe bize insan vücudundaki 33 noktayı bir indeks dizisi (0-32) olarak verir.
- Bizim `Archery.Core` motorumuz ise `BowShoulder` (Yay Omuzu), `DrawElbow` (Çekme Dirseği) gibi okçuluğa özgü isimler bekler.
- Bu dönüşümü C# tarafında `Dictionary` yardımıyla yaptık. 
- *Şimdilik sağ elli bir okçu varsayıldı (Yayı sol eliyle tutar).* `BowArm` sol kola, `DrawArm` sağ kola eşlendi.

### 3. Çözünürlük Ölçeklendirme (Scaling)
- Python'dan dönen koordinatlar, orijinal videonun boyutlarına göredir (ör. 1920x1080).
- Ancak tarayıcıdaki `<canvas>` daha küçük olabilir (ör. 800x450).
- JavaScript içinde oranlama (scaling) yaptık:
```javascript
const scaleX = canvas.width / video.videoWidth;
const scaleY = canvas.height / video.videoHeight;
// Çizerken:
ctx.lineTo(x * scaleX, y * scaleY);
```
Bu sayede ekran boyutu ne olursa olsun iskelet çizgileri adamın üzerine tam oturur.

---

## 🎯 Sonraki Adımlar
Şu anda sistem **Gerçek bir video** analiz edip açıları hesaplıyor!

Bir sonraki adım (Adım 6) için olası hedefler:
1. **Veritabanı (SQLite + Entity Framework Core):** Şu an her şey bellekte (In-Memory). Sunucu kapanınca tüm veriler gidiyor. Gerçek veritabanına geçiş yapılabilir.
2. **Sol/Sağ El Seçimi (Handedness):** Video yüklerken kullanıcıya "Sol elli mi, Sağ elli mi?" diye sorup analiz esnasında kolları ona göre eşleştirebiliriz.
3. **Core Geliştirmeleri:** Sadece `BowArmElbow` değil, omuz çizgisi, çekiş açısı gibi daha fazla açı tanımı eklenebilir.
