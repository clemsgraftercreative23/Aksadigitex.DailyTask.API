using API.Auth;
using API.Common;
using API.Reports;
using API.Services;
using FastEndpoints;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using FastEndpoints.Swagger;
using Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NSwag.Generation.Processors.Security;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// =======================
// DATABASE
// =======================
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(
        builder.Configuration.GetConnectionString("Default"),
        npgsql =>
        {
            npgsql.CommandTimeout(60);
            npgsql.ExecutionStrategy(deps => new ResilientNpgsqlExecutionStrategy(deps));
        }));

// =======================
// CONFIG
// =======================
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<ReportApprovalOptions>(builder.Configuration.GetSection(ReportApprovalOptions.SectionName));
builder.Services.Configure<FirebaseOptions>(builder.Configuration.GetSection(FirebaseOptions.SectionName));
builder.Services.Configure<DailyReportReminderOptions>(builder.Configuration.GetSection(DailyReportReminderOptions.SectionName));

// =======================
// SERVICES
// =======================
builder.Services.AddSingleton<AuthSessionStore>();
builder.Services.AddScoped<ReportStore>();
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<IFirebasePushService, FirebasePushService>();
builder.Services.AddHostedService<DailyTaskNotificationService>();

// =======================
// FIREBASE INIT (FIX RAILWAY)
// =======================
var firebaseSection = builder.Configuration.GetSection(FirebaseOptions.SectionName);
var firebaseEnabled = firebaseSection.GetValue<bool>("Enabled", true);

if (firebaseEnabled)
{
    var firebaseBase64 = Environment.GetEnvironmentVariable("FIREBASE_JSON_BASE64");

    if (string.IsNullOrEmpty(firebaseBase64))
        throw new InvalidOperationException("FIREBASE_JSON_BASE64 belum diset di environment.");

    try
    {
        var json = Encoding.UTF8.GetString(Convert.FromBase64String(firebaseBase64));

        FirebaseApp.Create(new AppOptions
        {
            Credential = GoogleCredential.FromJson(json)
        });

        Console.WriteLine("🔥 Firebase initialized (BASE64 ENV)");
    }
    catch (Exception ex)
    {
        Console.WriteLine("❌ Firebase init error: " + ex.Message);
        throw;
    }
}

// =======================
// JWT
// =======================
var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("Jwt configuration is missing.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// =======================
// JSON OPTIONS
// =======================
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new TimeOnlyJsonConverter());
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
});

// =======================
// FASTENDPOINTS + SWAGGER
// =======================
builder.Services.AddFastEndpoints();

builder.Services.SwaggerDocument(opt =>
{
    opt.EnableJWTBearerAuth = true;
    opt.DocumentSettings = s =>
    {
        s.Title = "Aksadigitex DailyTask API";
        s.Version = "v1";
        s.OperationProcessors.Add(new AspNetCoreOperationSecurityScopeProcessor("JWTBearerAuth"));
    };
});

// =======================
// BUILD APP
// =======================
var app = builder.Build();

// =======================
// STATIC FILES (UPLOADS)
// =======================
var uploadsPath = Path.Combine(app.Environment.ContentRootPath, "uploads");
Directory.CreateDirectory(uploadsPath);

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(uploadsPath),
    RequestPath = "/uploads",
});

// =======================
// MIDDLEWARE
// =======================
app.UseAuthentication();
app.UseAuthorization();

app.UseFastEndpoints(c =>
{
    c.Endpoints.RoutePrefix = "api";
});

app.UseSwaggerGen();

app.Run();