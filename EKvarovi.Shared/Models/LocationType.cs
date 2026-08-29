namespace EKvarovi.Shared.Models;

public class LocationType
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public ICollection<Location> Locations { get; set; } = new List<Location>();
}
