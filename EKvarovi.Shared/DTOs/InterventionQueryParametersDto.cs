namespace EKvarovi.Shared.DTOs;

public class InterventionQueryParametersDto
{
    // Pretraga po opisu prijave (tranzitivno: Intervention -> WorkAssignment -> FaultReport.Description)
    public string? Search { get; set; }

    public int? WorkAssignmentId { get; set; }

    // Tranzitivno preko WorkAssignment.FaultReportId
    public int? FaultReportId { get; set; }

    public int? InterventionStatusId { get; set; }

    // Tranzitivno preko WorkAssignment.TechnicianId
    public int? TechnicianId { get; set; }

    // Filtriraju StartedAt (kad je intervencija stvarno pokrenuta)
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }

    // Podržane vrijednosti: "startedAt", "endedAt" (case-insensitive).
    // Nepoznata/prazna vrijednost -> default sortiranje po StartedAt descending.
    public string? SortBy { get; set; }
    public bool SortDescending { get; set; }
}
