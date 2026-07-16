var builder = WebApplication.CreateBuilder(args);

// 1. CORS Güvenlik Ayarýný Ekliyoruz (Dýþarýdaki ekranlar API ile konuþabilsin diye)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddControllers();

var app = builder.Build();

// 2. CORS ayarýný uygulamada aktif ediyoruz
app.UseCors("AllowAll");

app.UseAuthorization();
app.MapControllers();

app.Run();