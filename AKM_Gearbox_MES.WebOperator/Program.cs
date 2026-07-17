var builder = WebApplication.CreateBuilder(args);

// 1. Razor Pages sayfalarýnýn derlenebilmesi için gerekli servisleri ekliyoruz (Aldýðýn hatanýn kesin çözümü)
builder.Services.AddRazorPages();

// 2. CORS politikasýný tanýmlýyoruz (Tarayýcý isteklerine izin vermek için)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowWebOperator", policy =>
    {
        policy.AllowAnyOrigin()   // Þimdilik test için her kökene izin veriyoruz
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// HTTP istek iþleme hattýný (middleware pipeline) yapýlandýrýyoruz.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// 3. CORS politikamýzý yönlendirmeden (Routing) hemen sonra aktif ediyoruz
app.UseCors("AllowWebOperator");

// 4. Razor Pages rotalarýný aktif hale getiriyoruz
app.MapRazorPages();

app.Run();