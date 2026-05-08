using System.Globalization;
using System.Text;
using System.Xml;
using Microsoft.EntityFrameworkCore;
using QuantamAnalytics.Domain.Entities;
using QuantamAnalytics.Infrastructure.Data;

namespace QuantamAnalytics.Api.Endpoints;

/// <summary>
/// Public, anonymous job-board feeds. The first format we ship is Indeed's
/// XML feed (see https://docs.indeed.com/job-feed/manual-feed-2.0/) — Indeed
/// pulls this URL on a schedule and ingests every <c>&lt;job&gt;</c> into
/// their search index, free of charge (organic posting, not sponsored).
/// </summary>
/// <remarks>
/// Cross-tenant safety: the URL carries the tenant slug, so the endpoint
/// resolves the tenant once and then queries jobs with
/// <c>IgnoreQueryFilters()</c> + an explicit <c>tenantId ==</c> predicate.
/// An invalid slug returns 404 — never a partial feed from another tenant.
///
/// The per-job <c>&lt;url&gt;</c> needs the public web host of the SPA, not
/// the API host. Read it from <c>PublicWeb:BaseUrl</c>; fall back to the
/// request's scheme + host so the feed still renders something sensible in
/// dev / local. Production must set <c>PublicWeb__BaseUrl</c> via env.
/// </remarks>
public static class JobFeedEndpoint
{
    public const string PublicWebBaseUrlConfigKey = "PublicWeb:BaseUrl";

    public static IEndpointRouteBuilder MapJobFeedEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/feeds")
            .WithTags("Job Feeds")
            .AllowAnonymous();

        group.MapGet("/{tenantSlug}/indeed.xml", GetIndeedFeedAsync);

        return app;
    }

    private static async Task<IResult> GetIndeedFeedAsync(
        string tenantSlug,
        AppDbContext db,
        IConfiguration configuration,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        // Tenant table is not tenant-scoped — no IgnoreQueryFilters needed.
        var tenant = await db.Tenants
            .Where(x => x.Slug == tenantSlug && x.IsActive)
            .Select(x => new { x.Id, x.Name })
            .SingleOrDefaultAsync(cancellationToken);

        if (tenant is null)
        {
            return Results.NotFound();
        }

        // IgnoreQueryFilters because there's no current tenant on this
        // anonymous endpoint — but we re-add the tenant constraint by hand
        // using the slug-resolved id. Same shape as the global filter.
        var jobs = await db.Jobs
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenant.Id && x.IsPublished)
            .OrderByDescending(x => x.PostedOnUtc)
            .ToArrayAsync(cancellationToken);

        var publicWebBase = configuration[PublicWebBaseUrlConfigKey];
        if (string.IsNullOrWhiteSpace(publicWebBase))
        {
            publicWebBase = $"{http.Request.Scheme}://{http.Request.Host}";
        }

        var xml = BuildIndeedXml(tenant.Name, publicWebBase!.TrimEnd('/'), jobs);
        return Results.Content(xml, "application/xml", Encoding.UTF8);
    }

    /// <summary>
    /// Renders the Indeed manual-feed v2 schema. Indeed requires
    /// <c>title</c>, <c>date</c>, <c>referencenumber</c>, <c>url</c>,
    /// <c>company</c>, <c>city</c>, <c>country</c>, <c>description</c>.
    /// We pass the free-form <see cref="Job.Location"/> through as the city
    /// and leave state empty — Indeed accepts city-only locations and we
    /// don't have a structured city/state split today.
    /// </summary>
    /// <remarks>
    /// Internal-but-public to make it cheap to unit-test without spinning
    /// up the whole API. Returning a <see cref="string"/> instead of writing
    /// to the response stream directly keeps the rendering pure and
    /// independent of the HTTP layer.
    /// </remarks>
    public static string BuildIndeedXml(string companyName, string publicWebBaseUrl, IReadOnlyList<Job> jobs)
    {
        var settings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "  ",
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
        };

        using var stringWriter = new StringWriterWithEncoding(Encoding.UTF8);
        using (var xml = XmlWriter.Create(stringWriter, settings))
        {
            xml.WriteStartDocument();
            xml.WriteStartElement("source");

            xml.WriteElementString("publisher", companyName);
            xml.WriteElementString("publisherurl", publicWebBaseUrl);
            xml.WriteElementString(
                "lastBuildDate",
                DateTimeOffset.UtcNow.ToString("ddd, dd MMM yyyy HH:mm:ss 'GMT'", CultureInfo.InvariantCulture));

            foreach (var job in jobs)
            {
                xml.WriteStartElement("job");

                WriteCData(xml, "title", job.Title);
                WriteCData(
                    xml,
                    "date",
                    new DateTimeOffset(job.PostedOnUtc.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)
                        .ToString("ddd, dd MMM yyyy HH:mm:ss 'GMT'", CultureInfo.InvariantCulture));
                WriteCData(xml, "referencenumber", job.Id.ToString("N", CultureInfo.InvariantCulture));
                WriteCData(xml, "url", $"{publicWebBaseUrl}/jobs/{job.Slug}");
                WriteCData(xml, "company", companyName);
                WriteCData(xml, "city", job.Location);
                WriteCData(xml, "state", string.Empty);
                WriteCData(xml, "country", "US");
                WriteCData(xml, "description", job.Description);

                xml.WriteEndElement(); // </job>
            }

            xml.WriteEndElement(); // </source>
            xml.WriteEndDocument();
        }

        return stringWriter.ToString();
    }

    private static void WriteCData(XmlWriter xml, string element, string value)
    {
        xml.WriteStartElement(element);
        xml.WriteCData(value);
        xml.WriteEndElement();
    }

    /// <summary>
    /// <see cref="StringWriter"/> hard-codes UTF-16 in its <c>Encoding</c>
    /// property, which causes <see cref="XmlWriter"/> to emit
    /// <c>&lt;?xml version="1.0" encoding="utf-16"?&gt;</c>. Indeed rejects
    /// utf-16 declarations on a UTF-8 wire payload. This subclass lets us
    /// pin the declared encoding to UTF-8.
    /// </summary>
    private sealed class StringWriterWithEncoding : StringWriter
    {
        public StringWriterWithEncoding(Encoding encoding)
        {
            Encoding = encoding;
        }

        public override Encoding Encoding { get; }
    }
}
