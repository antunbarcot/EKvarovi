namespace EKvarovi.Shared.DTOs;

public class MaterialDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int MaterialUnitId { get; set; }
    public string MaterialUnitName { get; set; } = string.Empty;
    public string MaterialUnitAbbreviation { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
