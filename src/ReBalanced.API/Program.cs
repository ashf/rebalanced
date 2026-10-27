using System.Reflection;
using System.Text.Json.Serialization;
using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using Microsoft.OpenApi;
using ReBalanced.API.Middleware;
using ReBalanced.Application.Services;
using ReBalanced.Application.Services.Interfaces;
using ReBalanced.Domain.Aggregates.PortfolioAggregate;
using ReBalanced.Domain.Providers;
using ReBalanced.Infrastructure.Caching;
using ReBalanced.Infrastructure.LiteDB;
using ReBalanced.Infrastructure.Repositories;
using ReBalanced.Infrastructure.TwelveData;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

// Api Version
builder.Services.AddApiVersioning(options =>
    {
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.DefaultApiVersion = new ApiVersion(1, 0); // 1.0
        options.ReportApiVersions = true;
    })
    // Swagger Api Version expansion
    .AddApiExplorer(setup =>
    {
        setup.GroupNameFormat = "'v'VVV";
        setup.SubstituteApiVersionInUrl = true;
    });

// API Layer Controllers
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.WriteIndented = true;
    });

// Application Layer services
builder.Services.AddScoped<IPortfolioService, PortfolioService>();
builder.Services.AddScoped<IRebalanceService, RebalanceService>();
builder.Services.AddScoped<IAssetService, AssetService>();

// Infrastructure Layer repositories
builder.Services.AddScoped<IEntityRepository<Portfolio>, LiteDbPortfolioRepository>();
builder.Services.AddScoped<IAssetRepository, TwelveDataAssetRepository>();
builder.Services.AddScoped<IAssetCache, LiteDbAssetCache>();

// Infrastructure Layer DB context
var folder = Environment.SpecialFolder.LocalApplicationData;
var path = Environment.GetFolderPath(folder);
var dbPath = Path.Join(path, "rebalanced.db");

builder.Services.AddLiteDb(dbPath);

// Infrastructure Layer REST Wrapper
builder.Services.AddOptions<TwelveDataOptions>()
    .Bind(builder.Configuration.GetSection(TwelveDataOptions.TwelveData))
    .Validate(options => !string.IsNullOrWhiteSpace(options.ApiKey), "TwelveData:ApiKey is not configured")
    .ValidateOnStart();

builder.Services.AddTwelveDataApi();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo {Title = "ReBalanced", Version = "v1"});
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory,
        $"{Assembly.GetExecutingAssembly().GetName().Name}.xml"));
});

// Logger
builder.Host.UseSerilog((hostingContext, services, loggerConfiguration) =>
    loggerConfiguration
        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Information)
        .Enrich.FromLogContext()
        .WriteTo.Console());

var app = builder.Build();

app.UseHttpsRedirection();

if (app.Environment.IsDevelopment()) app.UseMiddleware<ErrorHandlerMiddleware>();

app.MapControllers();

// Swagger UI
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.DocumentTitle = "ReBalanced";
    options.RoutePrefix = string.Empty;

    foreach (var description in app.Services.GetRequiredService<IApiVersionDescriptionProvider>()
                 .ApiVersionDescriptions)
        options.SwaggerEndpoint(
            $"/swagger/{description.GroupName}/swagger.json",
            description.GroupName.ToUpperInvariant());
});

app.Run();