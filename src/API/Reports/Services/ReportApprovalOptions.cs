namespace API.Reports;

public class ReportApprovalOptions
{
    public const string SectionName = "ReportApproval";

    public List<string> AllowedRoles { get; set; } = new();
    public List<string> AllowedEmails { get; set; } = new();
}
