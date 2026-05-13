using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using QuantamAnalytics.Api.Endpoints;
using QuantamAnalytics.Tests.TestAuth;

namespace QuantamAnalytics.Tests;

public sealed class ResumeParseEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const int OversizedBytes = (5 * 1024 * 1024) + 1;
    private readonly WebApplicationFactory<Program> _factory;

    public ResumeParseEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Recruiter_parse_returns_parsed_resume_payload()
    {
        var client = _factory.WithAuthenticatedUser(
                Guid.NewGuid(),
                "auth0|recruiter-1",
                "recruiter@example.com",
                "Recruiter")
            .CreateClient();

        using var response = await client.PostAsync(
            "/api/v1/recruiter/resume/parse",
            CreateFileContent("candidate.txt", "text/plain", "jane.candidate@example.com +1 (415) 555-0101"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<ParsedResumeResponse>();
        payload.Should().NotBeNull();
        payload!.Email.Should().Be("jane.candidate@example.com");
        payload.PhoneNumber.Should().Contain("415");
        payload.Skills.Should().HaveCount(6);
        payload.WorkHistory.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Candidate_parse_returns_same_contract_for_profile_prefill()
    {
        var client = _factory.WithAuthenticatedUser(
                Guid.NewGuid(),
                "auth0|candidate-1",
                "candidate@example.com",
                "Candidate")
            .CreateClient();

        using var response = await client.PostAsync(
            "/api/v1/candidate/profile/resume/parse",
            CreateFileContent("profile.txt", "text/plain", "candidate.resume@example.com"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<ParsedResumeResponse>();
        payload.Should().NotBeNull();
        payload!.Email.Should().Be("candidate.resume@example.com");
        payload.Skills.Should().HaveCount(6);
    }

    [Fact]
    public async Task Recruiter_parse_rejects_missing_file()
    {
        var client = _factory.WithAuthenticatedUser(
                Guid.NewGuid(),
                "auth0|recruiter-1",
                "recruiter@example.com",
                "Recruiter")
            .CreateClient();

        using var content = new MultipartFormDataContent();
        content.Add(JsonContent.Create(new { note = "no file attached" }), "payload");

        using var response = await client.PostAsync("/api/v1/recruiter/resume/parse", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Resume file required");
    }

    [Fact]
    public async Task Recruiter_parse_rejects_unsupported_content_type()
    {
        var client = _factory.WithAuthenticatedUser(
                Guid.NewGuid(),
                "auth0|recruiter-1",
                "recruiter@example.com",
                "Recruiter")
            .CreateClient();

        using var response = await client.PostAsync(
            "/api/v1/recruiter/resume/parse",
            CreateFileContent("candidate.png", "image/png", "not supported"));

        response.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);
    }

    [Fact]
    public async Task Candidate_parse_requires_tenant_claim()
    {
        var client = _factory.WithAuthenticatedUserWithoutTenant(
                "auth0|candidate-1",
                "candidate@example.com",
                "Candidate")
            .CreateClient();

        using var response = await client.PostAsync(
            "/api/v1/candidate/profile/resume/parse",
            CreateFileContent("profile.txt", "text/plain", "candidate.resume@example.com"));

        response.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);
        (await response.Content.ReadAsStringAsync()).Should().Contain("tenant_id");
    }

    [Fact]
    public async Task Recruiter_parse_rejects_oversized_upload()
    {
        var client = _factory.WithAuthenticatedUser(
                Guid.NewGuid(),
                "auth0|recruiter-1",
                "recruiter@example.com",
                "Recruiter")
            .CreateClient();

        var oversized = new string('a', OversizedBytes);
        using var response = await client.PostAsync(
            "/api/v1/recruiter/resume/parse",
            CreateFileContent("large.txt", "text/plain", oversized));

        response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
    }

    private static MultipartFormDataContent CreateFileContent(string fileName, string contentType, string body)
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(body));
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        content.Add(fileContent, "file", fileName);
        return content;
    }
}
