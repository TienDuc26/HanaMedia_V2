using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using HanaMedia.Models;
using HanaMedia.Services;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = Path.Combine(AppContext.BaseDirectory, "..", "..", ".."),
    WebRootPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "wwwroot")
});

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<AccountService>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login";
        options.AccessDeniedPath = "/Login";
    });

var app = builder.Build();

// Đảm bảo 2 field lockout tồn tại trong DB (chạy SQL nếu thiếu)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    try
    {
        var hasFailedCount = db.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS Value FROM sys.columns WHERE object_id = OBJECT_ID(N'[users]') AND name = 'failed_login_attempts'"
        ).AsEnumerable().FirstOrDefault();

        if (hasFailedCount == 0)
        {
            db.Database.ExecuteSqlRaw("ALTER TABLE [users] ADD [failed_login_attempts] INT NOT NULL CONSTRAINT DF_users_failed_login_attempts DEFAULT 0");
        }

        var hasLockedUntil = db.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS Value FROM sys.columns WHERE object_id = OBJECT_ID(N'[users]') AND name = 'locked_until'"
        ).AsEnumerable().FirstOrDefault();

        if (hasLockedUntil == 0)
        {
            db.Database.ExecuteSqlRaw("ALTER TABLE [users] ADD [locked_until] DATETIME2 NULL");
        }
    }
    catch (Exception ex)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Failed to ensure lockout fields exist in users table");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Lắng nghe trên tất cả IP (để test từ máy khác trong mạng LAN)
app.Urls.Clear();
app.Urls.Add("http://0.0.0.0:5028");
app.Urls.Add("https://0.0.0.0:7107");

app.Run();
