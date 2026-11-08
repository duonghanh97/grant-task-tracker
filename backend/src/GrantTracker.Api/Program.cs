using System.Text.Json.Serialization;
using GrantTracker.Api.Common.Database;
using GrantTracker.Api.Common.Errors;
using GrantTracker.Api.Common.Time;
using GrantTracker.Data;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

// Logging (OP-4, partial): structured JSON to the console.
builder.Services.AddSerilog((services, logger) => logger
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console(new RenderedCompactJsonFormatter()));

// Time: TimeProvider is replaced by FakeTimeProvider in tests.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddOptions<BusinessOptions>()
    .BindConfiguration(BusinessOptions.SectionName)
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<BusinessOptions>, BusinessOptionsValidator>();
builder.Services.AddSingleton<BusinessClock>();

// Data.
builder.Services.AddOptions<DatabaseOptions>().BindConfiguration(DatabaseOptions.SectionName);
builder.Services.AddData(sp =>
    sp.GetRequiredService<IConfiguration>().GetConnectionString("GrantTracker")
    ?? throw new InvalidOperationException("Connection string 'GrantTracker' is missing."));

// Errors: every non-2xx response is ProblemDetails with a stable `code`.
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = ApiProblem.AddDefaultCode);
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddExceptionHandler<ConcurrencyExceptionHandler>();
builder.Services.AddExceptionHandler<BadRequestExceptionHandler>();
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)));

// Health checks (OP-5, partial): readiness also proves the schema is reachable.
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>(
        "database",
        tags: ["ready"],
        customTestQuery: async (db, ct) => await db.Grants.CountAsync(ct) >= 0);

builder.Services.AddOpenApi();

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Grant Task Tracker v1"));
}

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

await app.InitializeDatabaseAsync();
await app.RunAsync();
