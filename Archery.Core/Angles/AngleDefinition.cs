namespace Archery.Core.Angles;

/// <summary>
/// Üç landmark noktası ile tanımlanan bir açı.
/// Açı, <see cref="ProximalLandmark"/>→<see cref="MidLandmark"/> ve
/// <see cref="DistalLandmark"/>→<see cref="MidLandmark"/> vektörleri arasında hesaplanır.
/// Yeni bir açı eklemek için kod değiştirmek değil, yeni bir tanım oluşturmak yeterlidir.
/// </summary>
/// <param name="Name">Açının adı (ör. "BowArmElbow").</param>
/// <param name="ProximalLandmark">Proksimal (gövdeye yakın) landmark adı.</param>
/// <param name="MidLandmark">Orta (eklem) landmark adı — açının köşesi.</param>
/// <param name="DistalLandmark">Distal (uç) landmark adı.</param>
public record AngleDefinition(
    string Name,
    string ProximalLandmark,
    string MidLandmark,
    string DistalLandmark);
