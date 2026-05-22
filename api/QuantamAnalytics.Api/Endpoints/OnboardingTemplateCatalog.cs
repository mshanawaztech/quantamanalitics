using QuantamAnalytics.Domain.Entities;

namespace QuantamAnalytics.Api.Endpoints;

/// <summary>
/// Default onboarding templates per worker classification. These encode the
/// real-world staffing/consulting onboarding flow so a brand-new consultant
/// gets a complete, phased checklist in one click instead of a recruiter
/// hand-adding items. Phase/required metadata comes from
/// <see cref="OnboardingPhases"/>; this catalog just decides which steps each
/// worker type gets, and the human-readable copy.
/// </summary>
public static class OnboardingTemplateCatalog
{
    public static IReadOnlyList<OnboardingTemplateStep> For(ConsultantType type) => type switch
    {
        ConsultantType.FullTime => FullTime,
        ConsultantType.Vendor => Vendor,
        _ => Contractor,
    };

    private static readonly OnboardingTemplateStep[] Contractor =
    [
        new(OnboardingItemType.GovernmentId, "Government-issued photo ID",
            "Upload a clear photo of your driver's license or passport for identity verification."),
        new(OnboardingItemType.WorkAuthorization, "Proof of work authorization",
            "Provide documentation establishing your eligibility to work."),
        new(OnboardingItemType.TaxFormW9, "Form W-9",
            "Complete and sign Form W-9 (Request for Taxpayer Identification Number)."),
        new(OnboardingItemType.Resume, "Up-to-date resume",
            "Upload your current resume / CV."),
        new(OnboardingItemType.Certification, "Certifications & licenses",
            "Upload any professional certifications or licenses relevant to your engagement."),
        new(OnboardingItemType.ProfessionalReference, "Professional references",
            "Provide two professional references we can contact."),
        new(OnboardingItemType.ServicesAgreement, "Independent contractor agreement (SOW)",
            "Review and sign the services agreement / statement of work."),
        new(OnboardingItemType.Nda, "Non-disclosure agreement",
            "Review and sign the mutual NDA."),
        new(OnboardingItemType.BackgroundCheck, "Background check authorization",
            "Authorize and complete the background screening."),
        new(OnboardingItemType.ClientCompliance, "Client-specific compliance",
            "Complete any client-required compliance (e.g., HIPAA, safety, security clearance)."),
        new(OnboardingItemType.DirectDeposit, "Payment / banking details",
            "Provide your remittance / banking details for payment."),
        new(OnboardingItemType.EmergencyContact, "Emergency contact",
            "Provide an emergency contact."),
        new(OnboardingItemType.EquipmentAccess, "Equipment & system access",
            "Confirm equipment needs and complete system-access setup."),
    ];

    private static readonly OnboardingTemplateStep[] FullTime =
    [
        new(OnboardingItemType.GovernmentId, "Government-issued photo ID",
            "Upload a clear photo of your driver's license or passport for identity verification."),
        new(OnboardingItemType.WorkAuthorization, "Proof of work authorization",
            "Provide documentation establishing your eligibility to work."),
        new(OnboardingItemType.I9, "Form I-9 (employment eligibility)",
            "Complete Form I-9 — Section 1 must be done on or before your start date."),
        new(OnboardingItemType.W4, "Form W-4 (federal withholding)",
            "Complete your federal tax withholding election."),
        new(OnboardingItemType.StateTaxForm, "State tax withholding",
            "Complete your state withholding form, if applicable."),
        new(OnboardingItemType.Resume, "Up-to-date resume",
            "Upload your current resume / CV."),
        new(OnboardingItemType.Certification, "Certifications & licenses",
            "Upload any professional certifications or licenses for your role."),
        new(OnboardingItemType.ProfessionalReference, "Professional references",
            "Provide two professional references we can contact."),
        new(OnboardingItemType.OfferLetter, "Signed offer letter",
            "Review and sign your offer letter."),
        new(OnboardingItemType.Nda, "Confidentiality agreement",
            "Review and sign the confidentiality / NDA."),
        new(OnboardingItemType.HandbookAcknowledgement, "Employee handbook acknowledgement",
            "Read and acknowledge the employee handbook."),
        new(OnboardingItemType.BackgroundCheck, "Background check authorization",
            "Authorize and complete the background screening."),
        new(OnboardingItemType.DirectDeposit, "Direct deposit",
            "Set up direct deposit for payroll."),
        new(OnboardingItemType.EmergencyContact, "Emergency contact",
            "Provide an emergency contact."),
        new(OnboardingItemType.EquipmentAccess, "Equipment & system access",
            "Confirm equipment needs and complete system-access setup."),
    ];

    private static readonly OnboardingTemplateStep[] Vendor =
    [
        new(OnboardingItemType.WorkAuthorization, "Business registration documents",
            "Provide your company's registration / incorporation documents."),
        new(OnboardingItemType.TaxFormW9, "Form W-9 (business)",
            "Provide your company's completed Form W-9."),
        new(OnboardingItemType.ServicesAgreement, "Master services agreement (MSA)",
            "Review and sign the master services agreement."),
        new(OnboardingItemType.Nda, "Mutual non-disclosure agreement",
            "Review and sign the mutual NDA."),
        new(OnboardingItemType.ClientCompliance, "Insurance & compliance certificates",
            "Provide certificate of insurance and any required compliance documents."),
        new(OnboardingItemType.BackgroundCheck, "Vendor due-diligence screening",
            "Complete vendor due-diligence screening."),
        new(OnboardingItemType.DirectDeposit, "Remittance / banking details",
            "Provide remittance details for payment."),
        new(OnboardingItemType.EquipmentAccess, "System access provisioning",
            "Complete system-access provisioning for your assigned consultants."),
    ];
}

public sealed record OnboardingTemplateStep(
    OnboardingItemType ItemType,
    string Title,
    string? Instructions);
