using System.Diagnostics;

using Scalar.AspNetCore;

using Serilog;

using UserManagementAPI.Api.Errors;
using UserManagementAPI.Api.Extensions;
using UserManagementAPI.Application;
using UserManagementAPI.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApiAuthentication(builder.Configuration);

// Non-nullable reference types on the request contracts are not runtime
// guarantees at the deserialization boundary; a missing field arrives as null.
// Without this, MVC treats every such property as [Required] and answers 400
// from model binding before the command validator ever runs. The validator is
// the one source of shape errors and it answers 422.
builder.Services.AddControllers(options =>
    options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true);

builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Instance =
            $"{context.HttpContext.Request.Method} {context.HttpContext.Request.Path}";

        context.ProblemDetails.Extensions["traceId"] =
            Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
    };
});

builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddOpenApi();

builder.Services.AddHealthChecks();

var app = builder.Build();

await app.MigrateAndSeedAsync();

app.UseSerilogRequestLogging();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapHealthChecks("/health");

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference("/scalar", options => options
        .WithTitle("UserManagementAPI")
        .WithTheme(ScalarTheme.BluePlanet));
}

app.Run();

/// <summary>
/// Exposed so the functional test project can drive the application
/// through WebApplicationFactory.
/// </summary>
public partial class Program;