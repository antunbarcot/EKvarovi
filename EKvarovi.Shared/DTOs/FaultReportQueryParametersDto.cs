namespace EKvarovi.Shared.DTOs;

public class FaultReportQueryParametersDto
{
    public string? Search { get; set; }
    public int? LocationId { get; set; }
    public int? FaultTypeId { get; set; }
    public int? FaultPriorityId { get; set; }
    public int? FaultStatusId { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }

    // true => samo prijave kojima rok istice unutar sljedeca 24h (i nisu vec
    // Rijesene/Zatvorene). Isto pravilo kao DashboardController.UpcomingDueFaultReportsCount.
    public bool? DueSoon { get; set; }

    // true => samo prijave koje su "Pregledano" ili dalje u toku, ali NEMAJU aktivnu
    // dodjelu. Isto pravilo kao DashboardController.UnassignedFaultReportsCount.
    public bool? UnassignedOnly { get; set; }

    // Podrzane vrijednosti: "createdAt", "dueDate", "priority" (case-insensitive).
    // Nepoznata/prazna vrijednost -> default sortiranje po CreatedAt descending.
    public string? SortBy { get; set; }
    public bool SortDescending { get; set; }
}
