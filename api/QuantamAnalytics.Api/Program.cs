using QuantamAnalytics.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);

// CORS for the Angular dev server (http://localhost:4200). The production
// origin is added via configuration once the SWA URL is known (PR-04).
const string DevCorsPolicy = "DevClient";
builder.Services.AddCors(options =>
{
    options.AddPolicy(DevCorsPolicy, policy => policy
        .WithOrigins("http://localhost:4200")
        .AllowAnyHeader()
        .AllowAnyMethod());
});

builder.Services.AddProblemDetails();

var app = builder.Build();

// Standardized RFC 7807 error responses for any unhandled exception.
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseCors(DevCorsPolicy);
}

app.MapHealthEndpoint();

app.Run();

// Exposed for WebApplicationFactory<TEntryPoint> in QuantamAnalytics.Tests.
public partial class Program;
