// src/HrMcp.Core/Entities/Position.cs
using HrMcp.Core.Enums;

namespace HrMcp.Core.Entities;

public class Position
{
    public int Id { get; set; }

    // USAJobs: PositionID (inside MatchedObjectDescriptor) — announcement number e.g. "23-11942190-6868/AHD"
    public string AnnouncementNumber { get; set; } = string.Empty;

    // USAJobs: MatchedObjectId (on SearchResultItem) — internal numeric ID e.g. "722102800"
    public string UsaJobsId { get; set; } = string.Empty;

    // USAJobs: PositionURI — direct link to the listing on USAJobs.gov
    public string PositionUri { get; set; } = string.Empty;

    // USAJobs: ApplyURI — direct application URL
    public string ApplyUri { get; set; } = string.Empty;

    // USAJobs: PositionTitle
    public string Title { get; set; } = string.Empty;

    // USAJobs: JobSummary — shown in search results
    public string Description { get; set; } = string.Empty;

    // USAJobs: MajorDuties — full narrative of what the employee does
    public string Duties { get; set; } = string.Empty;

    // USAJobs: Requirements + Education + Evaluations combined (matches what USAJobs site shows)
    public string Qualifications { get; set; } = string.Empty;

    // USAJobs: Education — degree/field of study requirements
    public string Education { get; set; } = string.Empty;

    // USAJobs: Evaluations — how applicants will be evaluated
    public string Evaluations { get; set; } = string.Empty;

    // USAJobs: KeyRequirements — bulleted conditions of employment
    public string KeyRequirements { get; set; } = string.Empty;

    // USAJobs: PromotionPotential — highest grade achievable without re-competing
    public string PromotionPotential { get; set; } = string.Empty;

    public bool IsOpen { get; set; }

    // USAJobs: JobCategories[].Code — 4-digit OPM series e.g. "2210"
    public string OccupationalSeries { get; set; } = string.Empty;

    // USAJobs: JobCategories[].Name — series title e.g. "IT Management"
    public string OccupationalSeriesTitle { get; set; } = string.Empty;

    // USAJobs: PayPlan + LowGrade / HighGrade e.g. "GS-09", "GS-11"
    public string PayGradeMin { get; set; } = string.Empty;
    public string PayGradeMax { get; set; } = string.Empty;

    // USAJobs: AppointmentType
    public AppointmentType AppointmentType { get; set; } = AppointmentType.Permanent;

    // USAJobs: PositionOfferingType — e.g. "Competitive Service", "Excepted Service"
    public string PositionOfferingType { get; set; } = string.Empty;

    // USAJobs: WorkSchedule
    public WorkSchedule WorkSchedule { get; set; } = WorkSchedule.FullTime;

    // USAJobs: PositionStartDate / ApplicationCloseDate
    public DateTime OpenDate { get; set; } = DateTime.UtcNow;
    public DateTime? CloseDate { get; set; }

    // USAJobs: WhoMayApply.Name
    public string WhoMayApply { get; set; } = "Open to US Citizens";

    // USAJobs: HiringPath[] — comma-joined e.g. "public, fed-competitive, veterans"
    public string HiringPath { get; set; } = string.Empty;

    // USAJobs: PositionLocation[].CityName
    public string DutyLocation { get; set; } = string.Empty;

    // USAJobs: PositionLocation[].CountrySubDivisionCode — state/territory code
    public string DutyLocationState { get; set; } = string.Empty;

    // USAJobs: TeleworkEligible
    public bool TeleworkEligible { get; set; } = false;

    // USAJobs: TravelCode
    public TravelRequirement TravelRequired { get; set; } = TravelRequirement.NotRequired;

    // USAJobs: SecurityClearance
    public SecurityClearance SecurityClearance { get; set; } = SecurityClearance.NotRequired;

    // USAJobs: ServiceType — "Competitive", "Excepted", or "SeniorExecutive"
    public string ServiceType { get; set; } = string.Empty;

    // USAJobs: SubAgencyName — bureau/office within the department
    public string SubAgencyName { get; set; } = string.Empty;

    // USAJobs: TotalOpenings — number of vacancies
    public string TotalOpenings { get; set; } = string.Empty;

    // USAJobs: AdjudicationType[] — position sensitivity/risk and trust determination e.g. "Credentialing, Suitability/Fitness"
    public string AdjudicationType { get; set; } = string.Empty;

    // USAJobs: RemoteIndicator — fully remote eligible
    public bool RemoteEligible { get; set; } = false;

    // USAJobs: FinancialDisclosure — requires OGE financial disclosure
    public bool FinancialDisclosure { get; set; } = false;

    // USAJobs: SupervisoryStatus
    public bool SupervisoryStatus { get; set; } = false;

    // USAJobs: RelocationExpensesReimbursed
    public bool RelocationAuthorized { get; set; } = false;

    // USAJobs: DrugTestRequired
    public bool DrugTestRequired { get; set; } = false;

    // USAJobs: PositionSensitivityAndRisk — e.g. "Noncritical-Sensitive (NCS) / Moderate Risk"
    public string PositionSensitivityAndRisk { get; set; } = string.Empty;

    // Agency contact for application questions
    public string ContactName { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string ContactAddress { get; set; } = string.Empty;

    // USAJobs: Conditions of employment (e.g. citizenship, drug test, clearance)
    public string ConditionsOfEmployment { get; set; } = string.Empty;

    // USAJobs: Required documents (resume, transcripts, DD-214, etc.)
    public string RequiredDocuments { get; set; } = string.Empty;

    // USAJobs: How to apply instructions
    public string HowToApply { get; set; } = string.Empty;

    // USAJobs: Next steps / timeline after application
    public string NextSteps { get; set; } = string.Empty;

    // USAJobs: Additional information
    public string AdditionalInformation { get; set; } = string.Empty;

    // Navigation
    public int HiringOrganizationId { get; set; }
    public HiringOrganization HiringOrganization { get; set; } = null!;
    public PositionRemuneration PositionRemuneration { get; set; } = null!;
}
