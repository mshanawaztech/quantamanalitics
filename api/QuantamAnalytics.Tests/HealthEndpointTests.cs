using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace QuantamAnalytics.Tests;

/// <summary>
/// Integration tests for the unauthenticated /health endpoint. The Angular
/// HealthService and any uptime probes depend on this contract — keep field
/// names lowercase camelCase and don't break the response shape.
/// </summary>
public sealed class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Health_returns_200_and_expected_payload()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<HealthDto>();
        payload.Should().NotBeNull();
        payload!.Status.Should().Be("ok");
        payload.Service.Should().Be("quantamanalitics-api");
        payload.Version.Should().NotBeNullOrWhiteSpace();
        payload.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
    }

    // Local DTO mirrors the wire shape so a rename in the Api project breaks
    // this test instead of silently breaking the Angular client.
    private sealed record HealthDto(string Status, string Service, string Version, DateTimeOffset Timestamp);
}
