using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using QuantamAnalytics.Api.Endpoints;
using QuantamAnalytics.Domain.Common;
using QuantamAnalytics.Tests.TestAuth;

namespace QuantamAnalytics.Tests;

public sealed class RecruiterImportCandidateEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public RecruiterImportCandidateEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Import_from_resume_creates_consultant_profile()
    {
        var client = _factory.WithAuthenticatedUser(
                Guid.NewGuid(),
                "auth0|recruiter-1",
                "recruiter@example.com",
                Roles.PlatformAdmin)
            .CreateClient();

        using var content = FileContent(
            "jane.txt", "text/plain", "jane.candidate@example.com +1 (415) 555-0101");

        using var response = await client.PostAsync(
            "/api/v1/recruiter/candidates/from-resume", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<ImportedCandidateResponse>();
        payload.Should().NotBeNull();
        payload!.Id.Should().NotBeEmpty();
        payload.Email.Should().Be("jane.candidate@example.com");
        payload.PhoneNumber.Should().Contain("415");
    }

    [Fact]
    public async Task Import_rejects_missing_file()
    {
        var client = _factory.WithAuthenticatedUser(
                Guid.NewGuid(),
                "auth0|recruiter-1",
                "recruiter@example.com",
                Roles.PlatformAdmin)
            .CreateClient();

        using var content = new MultipartFormDataContent();
        content.Add(JsonContent.Create(new { note = "no file" }), "payload");

        using var response = await client.PostAsync(
            "/api/v1/recruiter/candidates/from-resume", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static MultipartFormDataContent FileContent(string fileName, string contentType, string body)
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(body));
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        content.Add(fileContent, "file", fileName);
        return content;
    }
}
