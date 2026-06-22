// tools/UsaJobsFetcher/Program.cs
// One-time tool: fetches real federal job postings from the USAJobs API
// and writes data/usajobs-seed.json to the solution root.
//
// Store credentials via .NET User Secrets (never commit them):
//   dotnet user-secrets set "UsaJobs:Email"   "your@email.com"  --project tools/UsaJobsFetcher
//   dotnet user-secrets set "UsaJobs:AuthKey" "your-key-here"   --project tools/UsaJobsFetcher
//
// Then run from the solution root:
//   dotnet run --project tools/UsaJobsFetcher
//
// Output: data/usajobs-seed.json — commit this file so readers don't need an API key.

using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.Text.Json.Serialization;

var config = new ConfigurationBuilder()
    .AddUserSecrets("usajobs-fetcher")
    .Build();

var email   = config["UsaJobs:Email"]   ?? throw new InvalidOperationException("UsaJobs:Email secret is not set.");
var authKey = config["UsaJobs:AuthKey"] ?? throw new InvalidOperationException("UsaJobs:AuthKey secret is not set.");


// ── call USAJobs Search API ───────────────────────────────────────────────────
using var http = new HttpClient();
http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", email);
http.DefaultRequestHeaders.Add("Authorization-Key", authKey);

// ST = Department of State; ResultsPerPage max is 500
const string url =
    "https://data.usajobs.gov/api/search" +
    "?ResultsPerPage=500" +
    "&Organization=ST";   // ST = Department of State

Console.WriteLine($"Fetching: {url}");
var responseJson = await http.GetStringAsync(url);

// ── parse ─────────────────────────────────────────────────────────────────────
var parseOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
var root      = JsonSerializer.Deserialize<SearchRoot>(responseJson, parseOpts)!;
var items     = root.SearchResult.SearchResultItems;

Console.WriteLine($"Received {items.Count} positions");

// ── map to seed model ─────────────────────────────────────────────────────────
var orgs      = new Dictionary<string, SeedOrg>(StringComparer.OrdinalIgnoreCase);
var positions = new List<SeedPosition>();

foreach (var item in items)
{
    var d   = item.MatchedObjectDescriptor;
    if (d is null) continue;

    var orgName  = d.OrganizationName ?? "Unknown Agency";
    var deptName = d.DepartmentName   ?? "Unknown Department";

    orgs.TryAdd(orgName, new SeedOrg(orgName, deptName, ""));

    var det  = d.UserArea?.Details;
    var rem  = d.PositionRemuneration?.FirstOrDefault();
    var plan = d.JobGrade?.FirstOrDefault()?.Code ?? "GS";
    var lo   = det?.LowGrade;
    var hi   = det?.HighGrade;

    var isOpen = DateTime.TryParse(d.ApplicationCloseDate, out var close)
                 && close >= DateTime.UtcNow;

    var loc      = d.PositionLocation?.FirstOrDefault();
    var category = d.JobCategory?.FirstOrDefault();

    positions.Add(new SeedPosition(
        AnnouncementNumber:      d.PositionID        ?? "",
        UsaJobsId:               item.MatchedObjectId ?? "",
        PositionUri:             d.PositionURI       ?? "",
        ApplyUri:                d.ApplyURI?.FirstOrDefault() ?? "",
        Title:                   d.PositionTitle     ?? "",
        Description:             det?.JobSummary     ?? "",
        Duties:                  det?.MajorDuties is { Count: > 0 }
                                     ? string.Join(" ", det.MajorDuties)
                                     : "",
        Qualifications:          CombineQualifications(det),
        KeyRequirements:         det?.KeyRequirements is { Count: > 0 }
                                     ? string.Join(" ", det.KeyRequirements)
                                     : "",
        Education:               det?.Education ?? "",
        Evaluations:             det?.Evaluations ?? "",
        PromotionPotential:      det?.PromotionPotential ?? "",
        IsOpen:                  isOpen,
        OccupationalSeries:      category?.Code       ?? "",
        OccupationalSeriesTitle: category?.Name       ?? "",
        PayGradeMin:             lo is not null ? $"{plan}-{lo.PadLeft(2, '0')}" : "",
        PayGradeMax:             hi is not null ? $"{plan}-{hi.PadLeft(2, '0')}" : "",
        AppointmentType:         MapAppointment(d.PositionAppointmentType?.FirstOrDefault()?.Name),
        PositionOfferingType:    d.PositionOfferingType?.FirstOrDefault()?.Name ?? "",
        WorkSchedule:            MapSchedule(d.PositionSchedule?.FirstOrDefault()?.Name),
        OpenDate:                d.PositionStartDate      ?? DateTime.UtcNow.ToString("O"),
        CloseDate:               d.ApplicationCloseDate,
        WhoMayApply:             det?.WhoMayApply?.Name   ?? "Open to US Citizens",
        HiringPath:              det?.HiringPath is { Count: > 0 }
                                     ? string.Join(", ", det.HiringPath)
                                     : "",
        DutyLocation:            loc?.CityName             ?? "",
        DutyLocationState:       loc?.CountrySubDivisionCode ?? "",
        TeleworkEligible:        det?.TeleworkEligible     ?? false,
        TravelRequired:          MapTravel(det?.TravelCode),
        SecurityClearance:       MapClearance(det?.SecurityClearance),
        ServiceType:             MapServiceType(det?.ServiceType),
        SubAgencyName:           det?.SubAgencyName         ?? "",
        AdjudicationType:        det?.AdjudicationType is { Count: > 0 }
                                     ? string.Join(", ", det.AdjudicationType)
                                     : "",
        RemoteEligible:          det?.RemoteIndicator       ?? false,
        FinancialDisclosure:     det?.FinancialDisclosure    ?? false,
        TotalOpenings:           det?.TotalOpenings         ?? "",
        SupervisoryStatus:       "Yes".Equals(det?.SupervisoryPosition, StringComparison.OrdinalIgnoreCase),
        RelocationAuthorized:    "Yes".Equals(det?.Relocation,          StringComparison.OrdinalIgnoreCase),
        DrugTestRequired:        "Yes".Equals(det?.DrugTestRequired,     StringComparison.OrdinalIgnoreCase),
        OrganizationName:            orgName,
        MinimumRange:                decimal.TryParse(rem?.MinimumRange, out var mn) ? mn : 0,
        MaximumRange:                decimal.TryParse(rem?.MaximumRange, out var mx) ? mx : 0,
        RateIntervalCode:            rem?.RateIntervalCode ?? "PA",
        PositionSensitivityAndRisk:  det?.PositionSensitivity    ?? "",
        ContactName:                 det?.AgencyContactName       ?? "",
        ContactPhone:                det?.AgencyContactPhone      ?? "",
        ContactEmail:                det?.AgencyContactEmail      ?? "",
        ContactAddress:              det?.AgencyContactInfo       ?? "",
        ConditionsOfEmployment:      det?.ConditionOfEmployment is { Count: > 0 }
                                         ? string.Join("; ", det.ConditionOfEmployment)
                                         : "",
        RequiredDocuments:           det?.RequiredDocuments       ?? "",
        HowToApply:                  det?.HowToApply              ?? "",
        NextSteps:                   det?.WhatToExpectNext        ?? "",
        AdditionalInformation:       det?.OtherInformation        ?? ""
    ));
}

// ── write data/usajobs-seed.json ──────────────────────────────────────────────
var outPath   = Path.Combine(Directory.GetCurrentDirectory(), "data", "usajobs-seed.json");
Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);

var writeOpts = new JsonSerializerOptions
{
    WriteIndented          = true,
    PropertyNamingPolicy   = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
};

var seed = new SeedFile(orgs.Values.ToList(), positions);
await File.WriteAllTextAsync(outPath, JsonSerializer.Serialize(seed, writeOpts));

Console.WriteLine($"Wrote {positions.Count} positions from {orgs.Count} organizations");
Console.WriteLine($"Output: {outPath}");
return 0;

// ── string → enum label helpers ───────────────────────────────────────────────
static string MapAppointment(string? name) => name?.ToLower() switch
{
    { } s when s.Contains("perm") => "Permanent",
    { } s when s.Contains("term") => "Term",
    _                              => "Temporary"
};

static string MapSchedule(string? name) => name?.ToLower() switch
{
    { } s when s.Contains("full")         => "FullTime",
    { } s when s.Contains("part")         => "PartTime",
    { } s when s.Contains("intermittent") => "Intermittent",
    _                                      => "FullTime"
};

static string MapTravel(string? code) => code switch
{
    "1" => "NotRequired",
    "2" => "Occasional",
    "3" => "Sometimes",
    "4" => "Frequent",
    _   => "NotRequired"
};

// USAJobs splits qualifications across Requirements, Education, and Evaluations.
// Combine all non-empty parts so the stored text matches what the website shows.
static string CombineQualifications(UserAreaDetails? det)
{
    var parts = new[] { det?.Requirements, det?.Education, det?.Evaluations }
        .Where(s => !string.IsNullOrWhiteSpace(s));
    return string.Join("\n\n", parts);
}

static string MapClearance(string? s) => s?.ToLower() switch
{
    { } v when v.Contains("ts/sci") || v.Contains("top secret/sci")
                                   || v.Contains("sensitive compartmented") => "TopSecretSCI",
    { } v when v.Contains("top secret")                                     => "TopSecret",
    { } v when v.Contains("secret")                                         => "Secret",
    { } v when v.Contains("confidential")                                   => "Confidential",
    { } v when v.Contains("public trust")                                   => "PublicTrust",
    { } v when v.Contains("not required") || v == "none" || v == ""        => "NotRequired",
    _                                                                        => "NotRequired"
};

static string MapServiceType(string? code) => code switch
{
    "01" => "Competitive",
    "02" => "Excepted",
    "03" => "SeniorExecutive",
    _    => code ?? ""
};

// ── USAJobs API response types ────────────────────────────────────────────────
record SearchRoot(SearchResult SearchResult);
record SearchResult(int SearchResultCount, List<SearchResultItem> SearchResultItems);
record SearchResultItem(string? MatchedObjectId, MatchedObjectDescriptor? MatchedObjectDescriptor);
record MatchedObjectDescriptor(
    string?                          PositionID,
    string?                          PositionTitle,
    string?                          PositionURI,
    List<string>?                    ApplyURI,
    string?                          OrganizationName,
    string?                          DepartmentName,
    string?                          PositionStartDate,
    string?                          ApplicationCloseDate,
    List<PositionLocation>?          PositionLocation,
    List<JobCategory>?               JobCategory,
    List<JobGrade>?                  JobGrade,
    List<PositionSchedule>?          PositionSchedule,
    List<PositionAppointmentType>?   PositionAppointmentType,
    List<PositionOfferingType>?      PositionOfferingType,
    List<PositionRemuneration>?      PositionRemuneration,
    UserArea?                        UserArea);
record PositionLocation(string? CityName, string? CountrySubDivisionCode, string? CountryCode);
record JobCategory(string? Name, string? Code);
record JobGrade(string? Code);
record PositionSchedule(string? Name, string? Code);
record PositionAppointmentType(string? Name, string? Code);
record PositionOfferingType(string? Name, string? Code);
record PositionRemuneration(string? MinimumRange, string? MaximumRange, string? RateIntervalCode);
record UserArea(UserAreaDetails? Details);
record UserAreaDetails(
    string?       JobSummary,
    WhoMayApply?  WhoMayApply,
    string?       LowGrade,
    string?       HighGrade,
    string?       PromotionPotential,
    string?       Requirements,
    string?       Education,
    string?       Evaluations,
    List<string>? MajorDuties,
    List<string>? KeyRequirements,
    string?       Relocation,
    string?       DrugTestRequired,
    bool?         TeleworkEligible,
    string?       SupervisoryPosition,
    string?       SecurityClearance,
    string?       TravelCode,
    string?       ServiceType,
    string?       SubAgencyName,
    string?       TotalOpenings,
    List<string>? HiringPath,
    List<string>? AdjudicationType,
    bool?         RemoteIndicator,
    bool?         FinancialDisclosure,
    string?       PositionSensitivity,
    string?       AgencyContactName,
    string?       AgencyContactPhone,
    string?       AgencyContactEmail,
    string?       AgencyContactInfo,
    List<string>? ConditionOfEmployment,
    string?       RequiredDocuments,
    string?       HowToApply,
    string?       WhatToExpectNext,
    string?       OtherInformation);
record WhoMayApply(string? Name, string? Code);

// ── seed file model ───────────────────────────────────────────────────────────
record SeedFile(List<SeedOrg> Organizations, List<SeedPosition> Positions);
record SeedOrg(string OrganizationName, string DepartmentName, string AgencyDescription);
record SeedPosition(
    string   AnnouncementNumber,
    string   UsaJobsId,
    string   PositionUri,
    string   ApplyUri,
    string   Title,
    string   Description,
    string   Duties,
    string   Qualifications,
    string   Education,
    string   Evaluations,
    string   KeyRequirements,
    string   PromotionPotential,
    bool     IsOpen,
    string   OccupationalSeries,
    string   OccupationalSeriesTitle,
    string   PayGradeMin,
    string   PayGradeMax,
    string   AppointmentType,
    string   PositionOfferingType,
    string   WorkSchedule,
    string   OpenDate,
    string?  CloseDate,
    string   WhoMayApply,
    string   HiringPath,
    string   DutyLocation,
    string   DutyLocationState,
    bool     TeleworkEligible,
    string   TravelRequired,
    string   SecurityClearance,
    string   ServiceType,
    string   SubAgencyName,
    string   TotalOpenings,
    string   AdjudicationType,
    bool     RemoteEligible,
    bool     FinancialDisclosure,
    bool     SupervisoryStatus,
    bool     RelocationAuthorized,
    bool     DrugTestRequired,
    string   OrganizationName,
    decimal  MinimumRange,
    decimal  MaximumRange,
    string   RateIntervalCode,
    string?  PositionSensitivityAndRisk  = null,
    string?  ContactName                 = null,
    string?  ContactPhone                = null,
    string?  ContactEmail                = null,
    string?  ContactAddress              = null,
    string?  ConditionsOfEmployment      = null,
    string?  RequiredDocuments           = null,
    string?  HowToApply                  = null,
    string?  NextSteps                   = null,
    string?  AdditionalInformation       = null);
