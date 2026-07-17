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

// [ÖNEMLÝ SIRALAMA] Gelen isteklerin nereye gideceðini belirlemek için önce Routing çalýþmalý
app.UseRouting();

// [ÖNEMLÝ SIRALAMA] Yönlendirme yapýldýktan sonra CORS iznini kontrol ediyoruz
app.UseCors("AllowAll");

app.UseAuthorization();

// En son controller endpoint'lerini eþleþtirip çalýþtýrýyoruz
app.MapControllers();

app.Run();