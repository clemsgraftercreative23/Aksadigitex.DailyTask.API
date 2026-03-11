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
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(
        builder.Configuration.GetConnectionString("Default"),
        npgsql =>
        {
            npgsql.CommandTimeout(60);
            npgsql.ExecutionStrategy(deps => new ResilientNpgsqlExecutionStrategy(deps));
        }));

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<ReportApprovalOptions>(builder.Configuration.GetSection(ReportApprovalOptions.SectionName));
builder.Services.Configure<FirebaseOptions>(builder.Configuration.GetSection(FirebaseOptions.SectionName));
builder.Services.AddSingleton<AuthSessionStore>();
builder.Services.AddScoped<ReportStore>();
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<IFirebasePushService, FirebasePushService>();

// Initialize Firebase - when Enabled=true, file MUST exist, otherwise server will NOT start
var firebaseSection = builder.Configuration.GetSection(FirebaseOptions.SectionName);
var firebaseEnabled = firebaseSection.GetValue<bool>("Enabled", true);
var firebasePath = firebaseSection["ServiceAccountPath"]?.Trim();
var googleCredsPath = Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS")?.Trim();

if (firebaseEnabled)
{
    Console.WriteLine("111111111111111111111111111111111111111111111111111111111111111111");
    if (!string.IsNullOrEmpty(firebasePath))
    {
    Console.WriteLine("222222222222222222222222222222222222222222222222222222222222222222");
        var resolvedPath = Path.IsPathRooted(firebasePath)
            ? firebasePath
            : Path.Combine(builder.Environment.ContentRootPath, firebasePath);
        if (!File.Exists(resolvedPath))
        {
            Console.WriteLine("333333333333333333333333333333333333333333333333333333333333333333 FIREBASE NOT CONFIGURED (SERVICE_ACCOUNT_PATH)");
            throw new FileNotFoundException($"Firebase service account file tidak ditemukan (SERVICE_ACCOUNT_PATH): {resolvedPath}");
        }
        else
        {
            Console.WriteLine("444444444444444444444444444444444444444444444444444444444444444444 FIREBASE CONFIGURED (SERVICE_ACCOUNT_PATH)");
            FirebaseApp.Create(new AppOptions { Credential = GoogleCredential.FromFile(resolvedPath) });
        }
    }
    else if (!string.IsNullOrEmpty(googleCredsPath))
    {
        if (!File.Exists(googleCredsPath)){
          Console.WriteLine("333333333333333333333333333333333333333333333333333333333333333333 FIREBASE NOT CONFIGURED (GOOGLE_APPLICATION_CREDENTIALS)");
            throw new FileNotFoundException($"Firebase service account file tidak ditemukan (GOOGLE_APPLICATION_CREDENTIALS): {googleCredsPath}");

        }
        else
        {
            FirebaseApp.Create();
        }
    }
    else
    {
        Console.WriteLine("333333333333333333333333333333333333333333333333333333333333333333 FIREBASE NOT CONFIGURED");
        throw new InvalidOperationException("Firebase.Enabled=true tetapi ServiceAccountPath dan GOOGLE_APPLICATION_CREDENTIALS tidak dikonfigurasi.");
    }
}

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

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new TimeOnlyJsonConverter());
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
});

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

var app = builder.Build();

// Serve uploaded report attachments
var uploadsPath = Path.Combine(app.Environment.ContentRootPath, "uploads");
Directory.CreateDirectory(uploadsPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(uploadsPath),
    RequestPath = "/uploads",
});
app.UseAuthentication();
app.UseAuthorization();
app.UseFastEndpoints(c =>
{
    c.Endpoints.RoutePrefix = "api";
});
app.UseSwaggerGen();
app.Run();
