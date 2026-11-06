using System.Diagnostics;
using System.Text.Json.Serialization;
using Scalar.AspNetCore;
using Serilog;
using ServiceTemplate.Api.Features.Orders;
using ServiceTemplate.Api.Infrastructure;
using ServiceTemplate.Api.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.AddObservability();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database", tags: [HealthEndpoints.ReadyTag]);

// RFC 9457 problem details for every error response, including 404/405 from routing and unhandled
// exceptions. The trace id lets support correlate a client-reported error with logs and traces.
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    context.ProblemDetails.Extensions.TryAdd("traceId", Activity.Current?.TraceId.ToString() ?? context.HttpContext.TraceIdentifier));
builder.Services.AddExceptionHandler<ConcurrencyConflictExceptionHandler>();
builder.Services.AddValidation();
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddOpenApi("v1", options => options.AddDocumentTransformer((document, _, _) =>
{
    document.Info.Title = "ServiceTemplate API";
    document.Info.Version = "v1";
    document.Info.Description = "Sample order service built from the hekim-service template.";
    return Task.CompletedTask;
}));

var app = builder.Build();

// One image, two roles: `--migrate-only` applies migrations with the schema-owner role and exits, so the
// long-running API process can connect with a role that has DML rights only.
if (args.Contains("--migrate-only"))
{
    app.Logger.LogInformation("Applying database migrations");
    await app.Services.MigrateDatabaseAsync();
    app.Logger.LogInformation("Database migrations applied");
    return;
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSerilogRequestLogging(options =>
    // Successful probe calls are routine; keep them out of the logs but still log failures.
    options.GetLevel = (context, _, exception) =>
        exception is null && context.Response.StatusCode < 400 && context.Request.Path.StartsWithSegments("/health")
            ? Serilog.Events.LogEventLevel.Verbose
            : Serilog.Events.LogEventLevel.Information);

if (app.Configuration.GetValue<bool>("OpenApi:Enabled"))
{
    app.MapOpenApi();
    app.MapScalarApiReference(options => options.WithTitle("ServiceTemplate API"));
}

app.MapHealthEndpoints();
app.MapGroup("/api/v1")
    .WithGroupName("v1")
    .MapOrderEndpoints();

if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await app.Services.MigrateDatabaseAsync();
}

await app.RunAsync();
