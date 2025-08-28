using ApiCausality360.Data;
using ApiCausality360.Middleware;
using ApiCausality360.Services;
using AutoMapper;
using Azure.Security.KeyVault.Secrets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Azure;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAzureClients(factory =>
{
    factory.AddSecretClient(builder.Configuration.GetSection("KeyVault"));
});

SecretClient secretClient = builder.Services.BuildServiceProvider().GetRequiredService<SecretClient>();

KeyVaultSecret secretConnectionString = await secretClient.GetSecretAsync("SqlTajamar");
KeyVaultSecret secretApiKey = await secretClient.GetSecretAsync("ApiClave");

// 🔥 COPIAR PATRÓN QUE FUNCIONA: Variables locales
string connectionString = secretConnectionString.Value;
string groqApiKey = secretApiKey.Value;

// 🔥 AGREGAR A CONFIGURACIÓN PARA QUE LOS SERVICIOS LO ENCUENTREN
builder.Configuration["Groq:ApiKey"] = groqApiKey;

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// NUEVO: Memory Cache
builder.Services.AddMemoryCache();

// 🔥 Database context - USAR VARIABLE LOCAL COMO EN ZUVOPET
builder.Services.AddDbContext<CausalityContext>(options =>
    options.UseSqlServer(connectionString));

// NUEVO: Cache Service
builder.Services.AddScoped<ICacheService, CacheService>();

// NUEVO: Image Extraction Service
builder.Services.AddScoped<IImageExtractionService, ImageExtractionService>();

// NUEVO: Smart Categorizer Service
builder.Services.AddScoped<ISmartCategorizerService, SmartCategorizerService>();

// AutoMapper
builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

// Application services
builder.Services.AddScoped<IEventService, EventService>();
builder.Services.AddScoped<IIAService, IAService>();
builder.Services.AddScoped<INewsService, NewsService>();

// NUEVO: Background Service para procesamiento automático
builder.Services.AddHostedService<NewsSchedulerService>();

// HttpClient para servicios externos
builder.Services.AddHttpClient();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular",
        policy =>
        {
            policy.WithOrigins("https://ashy-bay-0e29e4a03.1.azurestaticapps.net")
            .SetIsOriginAllowedToAllowWildcardSubdomains()
              .AllowAnyHeader()
              .AllowAnyMethod();
        });
});

// NUEVO: Reemplazar Swagger con OpenAPI para Scalar
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    
}
// NUEVO: Configurar OpenAPI y Scalar (sintaxis correcta para v2.6.9)
app.MapOpenApi();
app.UseCors("AllowAngular");

// NUEVO: Rate Limiting Middleware
app.UseMiddleware<RateLimitingMiddleware>();

app.UseHttpsRedirection();
// Scalar UI con configuración personalizada
app.MapScalarApiReference(options =>
{
    options
        .WithTitle("ApiCausality360 - Análisis Geopolítico con IA")
        .WithTheme(ScalarTheme.Mars)
        .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient)
        .WithSidebar(true);
});

app.UseAuthorization();

app.MapControllers();

app.MapGet("/", context => {
    context.Response.Redirect("/scalar/v1");
    return Task.CompletedTask;
});

app.Run();