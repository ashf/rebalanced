using System.Reflection;
using System.Text.Json.Serialization;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using ReBalanced.API.Middleware;
using ReBalanced.Application.Services;
using ReBalanced.Application.Services.Interfaces;
using ReBalanced.Domain.Entities;
using ReBalanced.Domain.Providers;
using ReBalanced.Infastructure;
using ReBalanced.Infastructure.Providers;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

// Api Version
builder.Services.AddApiVersioning(options =>
{
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.DefaultApiVersion = new ApiVersion(1, 0); // 1.0
    options.ReportApiVersions = true;
});

// Swagger Api Version expansion
builder.Services.AddVersionedApiExplorer(setup =>
{
    setup.GroupNameFormat = "'v'VVV";
    setup.SubstituteApiVersionInUrl = true;
});

// MediatR
builder.Services.AddMediatR(typeof(Program));

// auto mapper
builder.Services.AddAutoMapper(typeof(Program));

// API Layer Controllers
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.WriteIndented = true;
    });

// Application Layer services
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IPortfolioService, PortfolioService>();

// Infastructure Layer repositories
builder.Services.AddScoped<IEntityRepository<Holding>, HoldingRepository>();
builder.Services.AddScoped<IEntityRepository<Portfolio>, PortfolioRepository>();

// Infastructure Layer DB context
var folder = Environment.SpecialFolder.LocalApplicationData;
var path = Environment.GetFolderPath(folder);
var DbPath = Path.Join(path, "rebalanced.db");

builder.Services.AddDbContext<Context>(options => { options.UseSqlite($"Data Source={DbPath}"); });

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
        .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Information)
        .Enrich.FromLogContext()
        .WriteTo.Console());

var app = builder.Build();

app.UseHttpsRedirection();

app.MapControllers();

if (app.Environment.IsDevelopment()) app.UseMiddleware<ErrorHandlerMiddleware>();

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