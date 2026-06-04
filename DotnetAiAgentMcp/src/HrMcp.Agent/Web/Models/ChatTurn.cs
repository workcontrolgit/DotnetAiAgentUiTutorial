namespace HrMcp.Agent.Web.Models;

public sealed record ChatTurn(string Role, string Text, DateTimeOffset Timestamp);
