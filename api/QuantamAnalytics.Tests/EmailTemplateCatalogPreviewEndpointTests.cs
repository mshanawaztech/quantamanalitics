using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using QuantamAnalytics.Api.Endpoints;
using QuantamAnalytics.Domain.Common;
using QuantamAnalytics.Tests.TestAuth;

namespace QuantamAnalytics.Tests;

public sealed class EmailTemplateCatalogPreviewEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public EmailTemplateCatalogPreviewEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Catalog_returns_starter_presets_and_supported_merge_fields()
    {
        var recruiter = _factory
            .WithAuthenticatedUser(Guid.NewGuid(), "auth0|rec-a", "ra@a.example", Roles.Recruiter)
            .CreateClient();

        var response = await recruiter.GetAsync("/api/v1/recruiter/email-templates/catalog");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<EmailTemplateCatalogResponse>();
        body.Should().NotBeNull();
        body!.Presets.Should().NotBeEmpty();
        body.Presets.Should().Contain(x => x.Slug == "interview-invite");
        body.Presets.Should().Contain(x => x.Slug == "offer-ready");
        body.SupportedMergeFields.Should().Contain("candidate_name");
        body.SupportedMergeFields.Should().Contain("job_title");
        body.Presets.SelectMany(x => x.MergeFields).Should().OnlyContain(x => body.SupportedMergeFields.Contains(x));
    }

    [Fact]
    public async Task Preview_renders_supported_merge_fields_case_insensitively()
    {
        var recruiter = _factory
            .WithAuthenticatedUser(Guid.NewGuid(), "auth0|rec-a", "ra@a.example", Roles.Recruiter)
            .CreateClient();

        var response = await recruiter.PostAsJsonAsync("/api/v1/recruiter/email-templates/preview", new
        {
            subject = "Interview invite for {{JOB_TITLE}}",
            bodyMarkdown = "Hi {{candidate_name}} from {{Company}}.",
            mergeFields = new Dictionary<string, string?>
            {
                ["job_title"] = "Platform Engineer",
                ["candidate_name"] = "Jordan Kim",
                ["company"] = "Quantam Analytics",
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<EmailTemplatePreviewResponse>();
        body.Should().NotBeNull();
        body!.Subject.Should().Be("Interview invite for Platform Engineer");
        body.BodyMarkdown.Should().Be("Hi Jordan Kim from Quantam Analytics.");
    }

    [Fact]
    public async Task Preview_rejects_unsupported_merge_fields()
    {
        var recruiter = _factory
            .WithAuthenticatedUser(Guid.NewGuid(), "auth0|rec-a", "ra@a.example", Roles.Recruiter)
            .CreateClient();

        var response = await recruiter.PostAsJsonAsync("/api/v1/recruiter/email-templates/preview", new
        {
            subject = "Hello",
            bodyMarkdown = "Body",
            mergeFields = new Dictionary<string, string?>
            {
                ["favorite_color"] = "blue",
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("favorite_color");
    }

    [Fact]
    public async Task Non_recruiter_caller_gets_403_on_catalog_and_preview()
    {
        var candidate = _factory
            .WithAuthenticatedUser(Guid.NewGuid(), "auth0|candidate", "c@example.com", Roles.Candidate)
            .CreateClient();

        var catalog = await candidate.GetAsync("/api/v1/recruiter/email-templates/catalog");
        var preview = await candidate.PostAsJsonAsync("/api/v1/recruiter/email-templates/preview", new
        {
            subject = "Hello",
            bodyMarkdown = "Body",
            mergeFields = new Dictionary<string, string?>
            {
                ["candidate_name"] = "Taylor",
            }
        });

        catalog.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        preview.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
