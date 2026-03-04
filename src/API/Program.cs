using API.Auth;
using FastEndpoints;
using FastEndpoints.Swagger;
using Infrastructure;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.AddSingleton<AuthSessionStore>();
builder.Services.AddScoped<JwtTokenService>();

builder.Services.AddFastEndpoints();
builder.Services.SwaggerDocument(opt =>
{
    opt.DocumentSettings = s =>
    {
        s.Title = "Aksadigitex DailyTask API";
        s.Version = "v1";
    };
});

var app = builder.Build();

app.UseFastEndpoints();
app.UseSwaggerGen();
app.Run();
