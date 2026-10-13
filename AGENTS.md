# Mimari İlkeler
- Açı hesabı ve sinyal işleme, web/veritabanı/poz modeline hiçbir bağımlılığı olmayan saf bir C# sınıf kütüphanesinde yaşar (Archery.Core).
- Ekranda gösterilen açı ile hesaplanan açı aynı kaynaktan gelir (sunucuda saklanan keypoint'ler). Tarayıcıda ayrıca açı hesaplanmaz.
- İki vektör arası açı atan2(|u×v|, u·v) ile hesaplanır. acos KULLANMA (175-180° civarında kararsız).
- Tüm hesaplar double, birim derece. Güveni düşük veya eksik noktada açı ÜRETME (null), tahmin etme, sıfır döndürme.
- Zaman ekseni video zaman damgasıdır (PTS). Kare numarası veya sabit fps varsayma (telefon videoları değişken kare hızlı olabilir).
- Kavramlar: BowArm (yay kolu), DrawArm (çekme kolu), sol/sağ elli desteği. "Kol 1 / Kol 2" adlarını kullanma.
- Açılar veri-güdümlü tanımlanır (AngleDefinition: ad + üç landmark adı), sonradan yeni açı eklemek kod değil tanım eklemek olsun. İlk sürümde her iki kol için dirsek açısı (omuz-dirsek-bilek).
- İlk sürüm tek kamera 2D. Tasarım 3D'ye hazır olsun (2D ve 3D tipleri ayrı, karıştırılmaz). 2D açının görüntü düzlemine izdüşüm olduğunu dokümante et.
- Okçuluk için "ideal/hedef açı" değeri UYDURMA.
- Kod ve tanımlayıcılar İngilizce, yorumlar ve kullanıcıya görünen metinler Türkçe.

# Atış Geçmişi & Benzerlik Paneli Özelliği

## Genel Tanım
Her tespit edilen atıştan sonra, BowArm ve DrawArm için AYRI AYRI bir "atış geçmişi" paneli gösterilecek:
- Her atış numaralandırılır (#1, #2, #3...).
- İlk atış otomatik REFERANS (REF) olur, yüzde almaz.
- Sonraki her atış, referans atışın açı eğrisiyle karşılaştırılıp bir BENZERLİK YÜZDESİ alır.
- En altta bir ORT (ortalama) satırı olur.
- Her atış küçük bir eğri (sparkline) ile gösterilir.

## Önemli Anlam Uyarısı
Bu panel TUTARLILIK ölçer (atış referansa ne kadar benziyor), DOĞRULUK değil. Kodda ve UI'da "doğruluk" / "accuracy" KULLANMA. "Benzerlik" / "similarity" / "consistency" kullan.
