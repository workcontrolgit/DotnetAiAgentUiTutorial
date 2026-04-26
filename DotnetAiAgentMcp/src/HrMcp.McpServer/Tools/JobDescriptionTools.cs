// src/HrMcp.McpServer/Tools/JobDescriptionTools.cs
using System.ComponentModel;
using HrMcp.Application.Services;
using ModelContextProtocol.Server;

namespace HrMcp.McpServer.Tools;

[McpServerToolType]
public sealed class JobDescriptionTools(PositionService positions)
{
    [McpServerTool(Name = "WriteJobDescription"),
     Description("Generates a formatted USAJobs-style job description for the specified position. Returns a structured template in Part 3; upgraded to LLM-generated narrative in Part 4.")]
    public async Task<string> WriteJobDescription(
        [Description("The numeric ID of the position to write a description for")] int positionId,
        CancellationToken ct = default)
    {
        var p = await positions.GetPositionByIdAsync(positionId, ct);
        if (p is null) return $"Position {positionId} not found.";

        return $"""
            ## {p.Title}

            **Department:** {p.HiringOrganization?.DepartmentName}
            **Organization:** {p.HiringOrganization?.OrganizationName}
            **Series & Grade:** {p.OccupationalSeries} | {p.PayGradeMin}–{p.PayGradeMax}
            **Salary:** ${p.PositionRemuneration?.MinimumRange:N0} – ${p.PositionRemuneration?.MaximumRange:N0} per year
            **Location:** {p.DutyLocation}
            **Telework:** {(p.TeleworkEligible ? "Eligible" : "Not eligible")}
            **Security Clearance:** {p.SecurityClearance}
            **Who May Apply:** {p.WhoMayApply}

            ### Summary
            {p.Description}

            ### Duties
            {p.Duties}

            ### Qualifications
            {p.Qualifications}

            ---
            *[Stub — LLM-generated narrative added in Part 4]*
            """;
    }
}
