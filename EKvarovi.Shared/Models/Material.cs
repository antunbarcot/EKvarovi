namespace EKvarovi.Shared.Models;

public class Material
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int MaterialUnitId { get; set; }
    public bool IsActive { get; set; }

    public MaterialUnit? MaterialUnit { get; set; }
    public ICollection<InterventionMaterial> InterventionMaterials { get; set; } = new List<InterventionMaterial>();
}
