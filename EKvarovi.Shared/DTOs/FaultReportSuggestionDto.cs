namespace EKvarovi.Shared.DTOs;

public class FaultReportSuggestionDto
{
    public string SuggestedTitle { get; set; } = string.Empty;
    public int? SuggestedFaultTypeId { get; set; }
    public string SuggestedFaultTypeName { get; set; } = string.Empty;
    public int? SuggestedFaultPriorityId { get; set; }
    public string SuggestedFaultPriorityName { get; set; } = string.Empty;
    public string Reasoning { get; set; } = string.Empty;
}
