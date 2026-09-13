using System.Diagnostics;

using Asp.Versioning.OpenApi;

using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc.Formatters;

using Scalar.AspNetCore;

using Serilog;
using Serilog.Formatting.Compact;

using UserManagementAPI.Api.Errors;
using UserManagementAPI.Api.Extensions;
using UserManagementAPI.Api.Hateoas;
using UserManagementAPI.Api.Middleware;
using UserManagementAPI.Api.Services;
using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Application;
using UserManagementAPI.Infrastructure;

const long MaxRequestBodyBytes = 256 * 1024;

var builder = WebApplication.CreateBuilder(args);

// Levels and overrides stay in configuration; the sink does not, because which
// sink is used is a security decision rather than an operational one.
//
// A request path is percent-decoded before it reaches a log call, so a request
// for /foo%0A... arrives with a real newline in it. Written to a plain-text
// console that is one forged log line, and it is not only this application's
// log calls that are exposed - UseSerilogRequestLogging writes the same path on
// every single request. Rendering the event as JSON ends the whole class: a
// newline inside a JSON string is two characters and cannot begin a new record,
// whatever it contains. ADR 0018.
//
// Development keeps the readable template. There is no attacker on a
// workstation, and this is the format a person reads all day.
builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext();

    if (context.HostingEnvironment.IsProduction())
    {
        configuration.WriteTo.Console(new CompactJsonFormatter());
    }
    else
    {
        configuration.WriteTo.Console(
            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId} {Message:lj}{NewLine}{Exception}");
    }
});

builder.Services.AddApplication();

// Who is acting, for the audit log. Registered ahead of AddInfrastructure so the
// HTTP-aware implementation wins over Infrastructure's "system" default.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApiAuthentication(builder.Configuration);

// Non-nullable reference types on the request contracts are not runtime
// guarantees at the deserialization boundary; a missing field arrives as null.
// Without this, MVC treats every such property as [Required] and answers 400
// from model binding before the command validator ever runs. The validator is
// the one source of shape errors and it answers 422.
//
// The HATEOAS vendor type is added to the JSON formatter so content negotiation
// can answer with it; the representation itself is chosen by the action.
builder.Services.AddControllers(options =>
{
    options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;

    options.OutputFormatters
        .OfType<SystemTextJsonOutputFormatter>()
        .First()
        .SupportedMediaTypes.Add(HateoasMediaTypes.Hateoas);
})
.ConfigureApiBehaviorOptions(options =>
    options.InvalidModelStateResponseFactory = ProblemDetailsResponses.FromModelState);

builder.Services.AddSingleton<LinkFactory>();
builder.Services.AddSingleton<UserLinkGenerator>();

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
builder.Services.AddExceptionHandler<ConcurrencyExceptionHandler>();
builder.Services.AddExceptionHandler<BadHttpRequestExceptionHandler>();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddVersionedApi();

builder.Services.AddApiRateLimiting(builder.Configuration);
builder.Services.AddApiCors(builder.Configuration);

// A JSON request body has no legitimate reason to be large here; the default
// 30 MB is a free denial-of-service budget.
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = MaxRequestBodyBytes);

// Behind Render's proxy the scheme and the caller's address arrive in headers.
// Without this the rate limiter partitions every request by the proxy's address
// and HTTPS redirection sees http.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // KnownIPNetworks, not KnownNetworks: the latter is obsolete in .NET 10
    // (ASPDEPR005) and obsolete is an error here.
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddObservability(builder.Configuration);

var app = builder.Build();

await app.MigrateAndSeedAsync();

app.UseForwardedHeaders();

app.UseCorrelationId();

app.UseSerilogRequestLogging();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseSecurityHeaders();

// Only in production. In the compose stack nothing listens on an HTTPS port, so
// redirecting there answers every call with a 307 to a port that is not open —
// and it is the source of the "failed to determine the https port" warning.
if (app.Environment.IsProduction())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseRouting();

app.UseCors(CorsExtensions.PolicyName);

// Authentication before the rate limiter, because the read and write policies
// partition by user id and there is no user until the token has been read. The
// order in BUILD-PLAN section 6.7 has the limiter first, which would key every
// authenticated request by IP instead.
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();

app.MapApiHealthChecks();

// The documentation is what makes a deployed instance a demo rather than a
// screenshot, so it can be switched on outside development — deliberately, by
// configuration, and off by default.
var documentationPublished =
    app.Environment.IsDevelopment() || app.Configuration.GetValue("OpenApi:Enabled", false);

if (documentationPublished)
{
    app.MapOpenApi().WithDocumentPerVersion();
    app.MapScalarApiReference("/scalar", options => options
        .WithTitle("UserManagementAPI")
        .WithTheme(ScalarTheme.BluePlanet)
        .AddDocuments(ApiVersioningExtensions.Documents));
}

// Someone who trims the URL down to the host should land somewhere useful
// rather than on a bare 404. The root document at /api is the honest REST entry
// point and is always there; the reference is the thing worth looking at, so it
// wins whenever it is published. HEAD is answered too, because that is what a
// platform health probe sends at the root.
//
// A found status, not a permanent one: this is a convenience, and a 301 would
// be cached by every browser that ever saw it.
app.MapMethods("/", ["GET", "HEAD"], () =>
        Results.Redirect(documentationPublished ? "/scalar" : "/api", permanent: false))
    .AllowAnonymous()
    .ExcludeFromDescription();

app.Run();

/// <summary>
/// Exposed so the functional test project can drive the application
/// through WebApplicationFactory.
/// </summary>
public partial class Program;