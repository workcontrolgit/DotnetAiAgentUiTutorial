namespace HrMcp.Agent.Components.Pages;

internal static class DraftWorkspaceHelper
{
    internal static string SlugifyTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return "draft";
        var slug = new string(title.ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray()).Trim('-');
        return slug[..Math.Min(40, slug.Length)];
    }
}
