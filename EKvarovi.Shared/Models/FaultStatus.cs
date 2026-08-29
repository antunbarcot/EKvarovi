namespace EKvarovi.Shared.Models;

public class FaultStatus
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }

    public ICollection<FaultReport> FaultReports { get; set; } = new List<FaultReport>();
}
