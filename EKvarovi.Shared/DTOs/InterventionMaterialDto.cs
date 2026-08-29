namespace EKvarovi.Shared.DTOs;

public class InterventionMaterialDto
{
    public int MaterialId { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public string MaterialUnitAbbreviation { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
}
