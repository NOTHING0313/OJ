using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using OnlineJudge.Api.Authentication;
using OnlineJudge.Api.Authorization;
using OnlineJudge.Api.Common;
using OnlineJudge.Api.Services;
using OnlineJudge.Api.RateLimiting;
using OnlineJudge.Api.Security;
using OnlineJudge.Application.Common.CurrentUser;
using OnlineJudge.Infrastructure;
using OnlineJudge.Infrastructure.Auth;
using OnlineJudge.Infrastructure.Storage;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers(options => options.Filters.Add<SecurityAuditFailureFilter>());
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<SecurityAuditFailureFilter>();
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = BrowserSessionConstants.CsrfHeaderName;
    options.Cookie.Name = BrowserSessionConstants.AntiforgeryCookieName;
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.Path = "/";
});

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("FrontendDev", policy =>
        {
            policy.WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
    });
}

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
    options.KnownProxies.Clear();
    options.KnownProxies.Add(System.Net.IPAddress.Loopback);
    options.KnownProxies.Add(System.Net.IPAddress.IPv6Loopback);
});

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<ActiveSessionJwtBearerEvents>();
builder.Services.AddHostedService<LeaderboardSeasonLifecycleWorker>();
builder.Services.AddHostedService<ChallengePeerReviewAssignmentWorker>();
builder.Services.AddHostedService<TeamChatSystemEventWorker>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var secret = builder.Configuration["Jwt:Secret"]
            ?? throw new InvalidOperationException("Jwt:Secret is not configured.");
        var issuer = builder.Configuration["Jwt:Issuer"]
            ?? throw new InvalidOperationException("Jwt:Issuer is not configured.");
        var audience = builder.Configuration["Jwt:Audience"]
            ?? throw new InvalidOperationException("Jwt:Audience is not configured.");

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
        options.EventsType = typeof(ActiveSessionJwtBearerEvents);
    });

builder.Services.AddCurrentRoleAuthorization();
builder.Services.AddRiskBasedRateLimiting();

builder.Services.AddOpenApi();

var app = builder.Build();

await RootAccountSeeder.SeedAsync(app.Services);

var webRootPath = app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot");
var storagePaths = app.Services.GetRequiredService<IRuntimeStoragePathProvider>();
Directory.CreateDirectory(storagePaths.UploadImagesRoot);
Directory.CreateDirectory(storagePaths.ThemeAssetsRoot);

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseForwardedHeaders();
app.UseMiddleware<SecurityAuditRequestContextMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(webRootPath)
});
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(storagePaths.UploadImagesRoot),
    RequestPath = "/uploads/images"
});
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(storagePaths.ThemeAssetsRoot),
    RequestPath = "/theme-assets"
});

if (app.Environment.IsDevelopment())
{
    app.UseCors("FrontendDev");
}

app.UseAuthentication();
app.UseMiddleware<CookieAntiforgeryMiddleware>();
app.UseRateLimiter();
app.UseAuthorization();
app.MapControllers();

app.Run();
