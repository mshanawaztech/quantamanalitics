using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace QuantamAnalytics.Infrastructure.ResumeParsing;

/// <summary>
/// Deterministic in-memory resume parser. Produces stable, plausible output
/// keyed off the file name's SHA-256, so the recruiter UX can be tested
/// against a consistent fixture without invoking a real parsing vendor.
/// </summary>
/// <remarks>
/// "Deterministic" matters for two reasons: (1) snapshot tests stay green
/// across reruns, and (2) demo data looks coherent — the same resume file
/// always parses to the same name/skills, so recruiter walkthroughs don't
/// surface jarring drift between sessions.
///
/// When a real vendor lands in Phase 9, the registration in
/// <c>DependencyInjection</c> flips and callers don't change.
/// </remarks>
public sealed partial class StubResumeParser : IResumeParser
{
    // Small but varied pools — enough to make a 30-candidate seed look real,
    // small enough to keep the assembly tiny.
    private static readonly string[] FirstNames =
        ["Avery", "Jordan", "Riley", "Morgan", "Taylor", "Casey", "Quinn", "Reese", "Dakota", "Skyler"];

    private static readonly string[] LastNames =
        ["Carter", "Patel", "Nguyen", "Lopez", "Kim", "Khan", "Singh", "Wong", "Garcia", "Reyes"];

    private static readonly string[] Headlines =
        [
            "Senior software engineer",
            "Full-stack developer",
            "Data engineer",
            "Cloud reliability engineer",
            "Mobile platform lead",
            "Frontend architect",
            "Backend platform engineer",
            "Site reliability engineer",
        ];

    private static readonly string[] SkillPool =
        [
            "C#", "ASP.NET Core", "EF Core", "PostgreSQL", "TypeScript", "Angular", "React",
            "Azure", "Kubernetes", "Terraform", "Bicep", "GitHub Actions", "REST APIs",
            "GraphQL", "Redis", "Snowflake", "dbt", "Python", "Go",
        ];

    private static readonly string[] Employers =
        [
            "Acme Logistics", "Northwind Health", "Contoso Cloud", "Globex Banking",
            "Initech Systems", "Umbra Analytics", "Stark Industries", "Wayne Robotics",
        ];

    private static readonly string[] Titles =
        [
            "Senior Engineer", "Staff Engineer", "Tech Lead",
            "Engineering Manager", "Engineer II", "Principal Engineer",
        ];

    private static readonly Regex EmailRegex = MyEmailRegex();
    private static readonly Regex PhoneRegex = MyPhoneRegex();

    public async Task<ResumeParseResult> ParseAsync(
        ResumeParseRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Read up to 256 KB of the stream so we can fish out an email / phone
        // when the file is actual text. Anything beyond that is treated as
        // opaque (PDF, DOCX) and we lean on the deterministic generator.
        var snippet = await ReadSnippetAsync(request.Content, cancellationToken);

        var seed = ComputeSeed(request.FileName);
        var rng = new Random(seed);

        var firstName = FirstNames[rng.Next(FirstNames.Length)];
        var lastName = LastNames[rng.Next(LastNames.Length)];
        var fullName = $"{firstName} {lastName}";

        var emailFromContent = ExtractFirstMatch(EmailRegex, snippet);
        var phoneFromContent = ExtractFirstMatch(PhoneRegex, snippet);

        var headline = Headlines[rng.Next(Headlines.Length)];
        var skills = PickDistinct(SkillPool, rng, count: 6);
        var history = BuildWorkHistory(rng);

        return new ResumeParseResult(
            FullName: fullName,
            Email: emailFromContent ?? $"{firstName}.{lastName}@example.com".ToLowerInvariant(),
            PhoneNumber: phoneFromContent ?? FormatPhone(rng),
            Headline: headline,
            Skills: skills,
            WorkHistory: history);
    }

    private static async Task<string> ReadSnippetAsync(Stream content, CancellationToken cancellationToken)
    {
        if (!content.CanRead)
        {
            return string.Empty;
        }

        const int Cap = 256 * 1024;
        var buffer = new byte[Cap];
        var read = await content.ReadAsync(buffer.AsMemory(0, Cap), cancellationToken);
        if (read == 0)
        {
            return string.Empty;
        }

        // ASCII slice — good enough for the email/phone regex pass; binary
        // bytes from a PDF land as control chars and harmlessly fail the
        // regex match.
        return Encoding.ASCII.GetString(buffer, 0, read);
    }

    private static int ComputeSeed(string fileName)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(fileName ?? string.Empty));
        return BitConverter.ToInt32(bytes, 0);
    }

    private static string? ExtractFirstMatch(Regex regex, string source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return null;
        }

        var match = regex.Match(source);
        return match.Success ? match.Value : null;
    }

    private static string[] PickDistinct(string[] pool, Random rng, int count)
    {
        var picks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var safety = 0;
        while (picks.Count < count && safety++ < pool.Length * 4)
        {
            picks.Add(pool[rng.Next(pool.Length)]);
        }
        return [.. picks];
    }

    private static ResumeWorkHistoryItem[] BuildWorkHistory(Random rng)
    {
        var jobs = rng.Next(2, 4); // 2 or 3 prior roles
        var endCursor = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var items = new List<ResumeWorkHistoryItem>(jobs);

        for (var i = 0; i < jobs; i++)
        {
            var months = rng.Next(12, 36);
            var start = endCursor.AddMonths(-months);
            items.Add(new ResumeWorkHistoryItem(
                Employer: Employers[rng.Next(Employers.Length)],
                Title: Titles[rng.Next(Titles.Length)],
                StartDate: start,
                EndDate: i == 0 ? null : endCursor));
            endCursor = start;
        }

        return [.. items];
    }

    private static string FormatPhone(Random rng) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"+1-{rng.Next(200, 999)}-{rng.Next(200, 999)}-{rng.Next(1000, 9999)}");

    [GeneratedRegex(@"[\w.+-]+@[\w-]+\.[\w.-]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MyEmailRegex();

    [GeneratedRegex(@"\+?\d[\d\s().-]{7,}\d", RegexOptions.CultureInvariant)]
    private static partial Regex MyPhoneRegex();
}
