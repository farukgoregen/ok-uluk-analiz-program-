using Archery.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// --- Servis Kayıtları ---
// MVC Controller desteği (API endpoint'leri için)
// PropertyNamingPolicy: Point2D.X/Y gibi struct property'leri JS tarafında p.x/p.y olarak okunur
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase);

// Razor Pages desteği (sayfa bazlı frontend için)
builder.Services.AddRazorPages();

// Singleton: uygulama ömrü boyunca tek örnek (in-memory depo)
builder.Services.AddSingleton<ISessionStore, InMemorySessionStore>();

// Video dosyaları "uploads" klasörüne kaydedilir
var uploadsPath = Path.Combine(builder.Environment.ContentRootPath, "uploads");
builder.Services.AddSingleton<IVideoStorage>(new LocalVideoStorage(uploadsPath));

// Analiz servisleri
builder.Services.AddSingleton<IPoseEstimator, PythonMediaPipeEstimator>();
builder.Services.AddSingleton<AnalysisService>();

// Swagger / OpenAPI — geliştirme ortamında API'yi keşfetmek için
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Okçuluk Analiz API",
        Version = "v1",
        Description = "Okçuluk poz analizi — video yükleme ve açı hesaplama API'si"
    });
});

var app = builder.Build();

// Swagger UI sadece geliştirme ortamında
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Statik dosyalar (CSS, JS, resimler)
app.UseStaticFiles();

// Routing
app.UseRouting();

// MVC Controller routing (API)
app.MapControllers();

// Razor Pages routing (Frontend)
app.MapRazorPages();

app.Run();
