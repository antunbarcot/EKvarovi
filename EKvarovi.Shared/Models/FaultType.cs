namespace EKvarovi.Shared.Models;

public class FaultType
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public ICollection<FaultReport> FaultReports { get; set; } = new List<FaultReport>();
}
