namespace EKvarovi.Shared.Models;

public class InterventionStatus
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public ICollection<Intervention> Interventions { get; set; } = new List<Intervention>();
}
