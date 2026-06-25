namespace HrMcp.Agent.Web.Models;

public sealed class DraftDocumentState
{
	public string DraftText { get; set; } = string.Empty;
	public int Revision { get; set; }

	public DraftDocumentState()
	{
	}

	public DraftDocumentState(string draftText, int revision)
	{
		DraftText = draftText;
		Revision = revision;
	}
}
