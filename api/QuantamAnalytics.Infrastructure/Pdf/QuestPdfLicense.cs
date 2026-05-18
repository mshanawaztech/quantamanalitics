using System.Runtime.CompilerServices;
using QuestPDF.Infrastructure;

namespace QuantamAnalytics.Infrastructure.Pdf;

/// <summary>
/// One-time QuestPDF community-license registration.
///
/// Why a ModuleInitializer: QuestPDF requires the license to be set
/// exactly once per process, before the first document is rendered.
/// Setting it inside <see cref="QuestPdfInvoiceRenderer"/>'s constructor
/// is fragile — the constructor runs on the first DI resolution, which
/// can happen mid-request after <see cref="QuestPDF.Settings.License"/>
/// has already been read elsewhere (e.g. when WebApplicationFactory in
/// integration tests builds a host that touches QuestPDF before any
/// renderer is resolved).
///
/// A ModuleInitializer runs once when the Infrastructure assembly is
/// loaded into the process, guaranteed before any type from this
/// assembly is touched. That covers both Program.cs (production) and
/// WebApplicationFactory&lt;TEntryPoint&gt; (tests) without either
/// having to know about QuestPDF.
/// </summary>
internal static class QuestPdfLicense
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }
}
