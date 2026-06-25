// src/HrMcp.McpServer/Tools/HiringOrganizationTools.cs
using System.ComponentModel;
using HrMcp.Application.Services;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace HrMcp.McpServer.Tools;

[McpServerToolType]
public sealed class HiringOrganizationTools(
    HiringOrganizationService organizations,
    ILogger<HiringOrganizationTools> logger)
{
    [McpServerTool(Name = "GetHiringOrganizations"),
     Description("Returns all federal hiring organizations in the database with their department affiliations, IDs, and open position count.")]
    public async Task<IEnumerable<object>> GetHiringOrganizations(CancellationToken ct = default)
    {
        logger.LogInformation("[Request ] GetHiringOrganizations");
        var list = await organizations.GetAllOrganizationsAsync(ct);
        var result = list.Select(o => (object)new
        {
            o.Id,
            o.OrganizationName,
            o.DepartmentName,
            o.AgencyDescription,
            OpenPositionCount = o.Positions.Count(p => p.IsOpen)
        }).ToList();
        logger.LogInformation("[Response] GetHiringOrganizations => {Count} organizations", result.Count);
        return result;
    }
}
