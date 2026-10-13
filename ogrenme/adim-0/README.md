# Adım 0 — Ne Yaptık? Ne Öğrendik?

Bu belgede Adım 0'da oluşturulan proje yapısı, kullanılan teknolojiler,
kütüphaneler ve temel kavramlar açıklanmaktadır.

---

## 📂 Proje Yapısı

```
okçuluk/
├── Archery.sln                          ← Solution dosyası (tüm projeleri birleştirir)
├── AGENTS.md                            ← Mimari kurallar ve çalışma protokolü
│
├── Archery.Core/                        ← Sınıf kütüphanesi (saf hesaplama, bağımlılık yok)
│   ├── Archery.Core.csproj
│   ├── Geometry/
│   │   ├── Point2D.cs                   ← 2D nokta (piksel koordinatı)
│   │   ├── Vector2D.cs                  ← 2D vektör (yön + büyüklük)
│   │   └── AngleCalculator.cs           ← Açı hesaplama (atan2)
│   ├── Angles/
│   │   ├── AngleDefinition.cs           ← Veri-güdümlü açı tanımı
│   │   ├── WellKnownAngles.cs           ← Sabit açı tanımları (dirsek açıları)
│   │   ├── AngleConfidence.cs           ← Güven seviyesi enum'u
│   │   ├── AngleResult.cs               ← Açı hesaplama sonucu
│   │   └── FrameAngleComputer.cs        ← Bir karedeki tüm açıları hesaplar
│   └── Landmarks/
│       ├── LandmarkPoint.cs             ← Tek bir eklem noktası
│       └── LandmarkFrame.cs             ← Bir karenin tüm eklem noktaları
│
└── Archery.Core.Tests/                  ← Birim testleri
    ├── Archery.Core.Tests.csproj
    ├── Geometry/
    │   └── AngleCalculatorTests.cs      ← Açı hesaplama testleri
    └── Angles/
        └── FrameAngleComputerTests.cs   ← Kare analizi testleri
```

---

## 🛠️ Kullanılan Teknolojiler

### 1. .NET 8 (LTS)
- **Ne?** Microsoft'un açık kaynak, çapraz platform uygulama geliştirme çerçevesi.
- **Neden 8?** Long Term Support (LTS) = uzun süreli destek, kararlı sürüm.
- **Proje tipi:** `classlib` (sınıf kütüphanesi) — çalıştırılabilir değil, başka projeler tarafından referans alınır.
- 📖 https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-8

### 2. C# 12
- .NET 8 ile gelen en güncel C# sürümü.
- Bu projede kullanılan C# 12 özellikleri aşağıda detaylandırılmıştır.

### 3. xUnit
- **Ne?** .NET için en yaygın birim test çerçevesi.
- **Neden?** Basit, topluluk desteği güçlü, .NET şablonlarında varsayılan.
- 📖 https://xunit.net/

---

## 📦 Kullanılan NuGet Paketleri

| Paket | Versiyon | Ne İşe Yarar | Hangi Projede |
|---|---|---|---|
| `xunit` | 2.5.3 | Test çerçevesi — `[Fact]`, `Assert` gibi araçlar sağlar | Tests |
| `xunit.runner.visualstudio` | 2.5.3 | Visual Studio ve `dotnet test` ile testleri çalıştırır | Tests |
| `Microsoft.NET.Test.Sdk` | 17.8.0 | .NET test altyapısı — test keşfi ve çalıştırma | Tests |
| `coverlet.collector` | 6.0.0 | Kod kapsam (coverage) raporu toplar | Tests |

> **Not:** `Archery.Core` projesinde **hiçbir NuGet paketi yok**. Sadece .NET'in
> yerleşik (built-in) kütüphanelerini kullanır. Bu bilinçli bir karar:
> hesaplama katmanı saf ve bağımsız olmalı.

---

## 🧠 Temel C# Kavramları

### `readonly record struct` (Point2D, Vector2D)
```csharp
public readonly record struct Point2D(double X, double Y);
```
- **`record`**: Otomatik olarak `Equals`, `GetHashCode`, `ToString` üretir. Değer eşitliği sağlar.
- **`struct`**: Yığın (stack) üzerinde yaşar, heap allocation yok → performanslı.
- **`readonly`**: Oluşturulduktan sonra değiştirilemez (immutable).
- **Neden?** Geometri noktaları matematiksel değerlerdir, değişmemeli. İki `Point2D(3, 4)` eşit olmalı.

### `record` (AngleDefinition, AngleResult, LandmarkFrame, LandmarkPoint)
```csharp
public record AngleDefinition(string Name, string ProximalLandmark, ...);
```
- `record class` (varsayılan) — referans tipli, ama yine değer eşitliği var.
- `struct` yerine `class` çünkü bu tipler daha karmaşık (dictionary içerir vb.).

### `static class` (AngleCalculator, FrameAngleComputer, WellKnownAngles)
```csharp
public static class AngleCalculator { ... }
```
- Durumu (state) olmayan, saf fonksiyonlardan oluşan sınıf.
- `new` ile oluşturulamaz, doğrudan `AngleCalculator.ComputeAngleDegrees(u, v)` şeklinde çağrılır.

### Nullable Value Types (`double?`)
```csharp
public static double? ComputeAngleDegrees(Vector2D u, Vector2D v)
```
- `double?` = "bu değer bir `double` olabilir veya `null` olabilir".
- Neden? Sıfır vektör veya eksik landmark varsa açı **hesaplanamaz**. Sıfır veya -1 döndürmek yerine `null` döndürmek daha güvenli ve anlamlı.

### `enum` (AngleConfidence)
```csharp
public enum AngleConfidence { High, Low, Insufficient }
```
- Sabit değerler kümesi. Tip güvenliği sağlar (string yerine).

---

## 📐 Matematiksel Kavramlar

### Vektör Nedir?
Bir noktadan diğerine yönü ve mesafeyi gösteren ok.
```
A(1,2) → B(4,6) ise vektör = (4-1, 6-2) = (3, 4)
```

### Nokta Çarpım (Dot Product)
```
u · v = u.X * v.X + u.Y * v.Y
```
- İki vektörün ne kadar "aynı yöne" baktığını ölçer.
- Aynı yön → pozitif, dik → 0, ters yön → negatif.

### Çapraz Çarpım (Cross Product, 2D skaler)
```
u × v = u.X * v.Y - u.Y * v.X
```
- İki vektör arasındaki "dönme miktarını" ölçer.
- 2D'de skaler bir değer döner (3D'de vektör döner).

### Neden `atan2(|u×v|, u·v)` ve Neden `acos` Değil?

**`acos` sorunu:**
```
açı = acos(u·v / (|u| * |v|))
```
- 175°–180° arasında `acos`'un girdi değeri -1'e çok yakın olur.
- Kayan nokta (floating point) hatası nedeniyle girdi -1.0000000001 gibi bir değer alabilir.
- `acos(-1.0000000001)` = **NaN** (tanımsız) → program çöker veya yanlış sonuç verir.

**`atan2` çözümü:**
```
açı = atan2(|u×v|, u·v)
```
- `atan2` tüm açı aralığında **sayısal olarak kararlıdır**.
- 180° civarında bile doğru sonuç verir.
- `|u×v|` (mutlak değer) kullanarak açıyı [0°, 180°] aralığına sınırlıyoruz.

**Somut örnek:**
```
u = (1, 0), v = (-1, 0.0001)  → neredeyse 180°

acos ile: girdi ≈ -0.99999999 → acos hassasiyeti düşük, hata riski
atan2 ile: atan2(0.0001, -0.99999999) → 179.994...° ✅ kararlı
```

---

## 🏹 Okçuluk Kavramları

### BowArm (Yay Kolu) vs DrawArm (Çekiş Kolu)
- **BowArm:** Yayı tutan kol (sol elli okçuda sağ kol, sağ elli okçuda sol kol).
- **DrawArm:** Kirişi çeken kol (diğer kol).
- Kodda sol/sağ değil BowArm/DrawArm kullanıyoruz çünkü okçunun elinden bağımsız.

### Dirsek Açısı
```
Omuz -----> Dirsek -----> Bilek
         (açının köşesi)
```
- Üç nokta: Omuz (proximal), Dirsek (mid/vertex), Bilek (distal).
- Tam gergin kol = 180°, bükülü kol < 180°.

### Landmark Güven Değeri
- Poz tahmini modeli her eklem noktasına 0–1 arası güven verir.
- 0.9 = "bu noktanın burada olduğundan çok eminim"
- 0.1 = "tahmin ettim ama emin değilim"
- Eşik altı güven → açı hesaplanmaz (null döner), yanlış veri üretmek yerine "bilmiyorum" deriz.

---

## 🔧 Kullanılan Komutlar

| Komut | Ne Yapar |
|---|---|
| `dotnet new sln -n Archery` | Boş bir solution dosyası oluşturur |
| `dotnet new classlib -n Archery.Core -f net8.0` | Sınıf kütüphanesi projesi oluşturur |
| `dotnet new xunit -n Archery.Core.Tests -f net8.0` | xUnit test projesi oluşturur |
| `dotnet sln add Proje.csproj` | Projeyi solution'a ekler |
| `dotnet add reference Proje.csproj` | Bir projeye diğerine referans ekler |
| `dotnet build` | Projeyi derler (kaynak kod → DLL) |
| `dotnet test` | Testleri çalıştırır |

---

## 📝 .csproj Dosyası Ne Demek?

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>      <!-- .NET 8 hedefleniyor -->
    <ImplicitUsings>enable</ImplicitUsings>         <!-- System, System.Collections.Generic vb. otomatik using -->
    <Nullable>enable</Nullable>                     <!-- null güvenliği açık (derleyici uyarıları) -->
  </PropertyGroup>
</Project>
```

- Her .NET projesi bir `.csproj` dosyasıyla tanımlanır.
- `TargetFramework`: Hangi .NET sürümü.
- `ImplicitUsings`: Sık kullanılan namespace'ler otomatik dahil edilir.
- `Nullable`: `null` olabilecek referanslar için derleyici uyarısı verir.

---

## 🔑 Mimari Kararlar ve Nedenleri

| Karar | Neden |
|---|---|
| Archery.Core'da NuGet paketi yok | Hesaplama katmanı saf olmalı, dış bağımlılık minimum |
| `acos` yerine `atan2` | 175–180° civarında sayısal kararlılık |
| `double?` (nullable) | Hesaplanamayan açı için sıfır/tahmin yerine "bilmiyorum" |
| `AngleDefinition` record | Yeni açı = yeni tanım, kod değişikliği gerekmez |
| `TimeSpan` (PTS) | Telefon videoları değişken kare hızlı, kare numarası güvenilmez |
| `Point2D` / `Vector2D` ayrı adlar | İleride `Point3D` / `Vector3D` eklenecek, karışmasın |
| BowArm / DrawArm | Sol/sağ elden bağımsız kavram, okçuluk terminolojisi |
