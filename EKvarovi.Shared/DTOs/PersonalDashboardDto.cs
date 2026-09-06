namespace EKvarovi.Shared.DTOs;

public class PersonalDashboardDto
{
    // Popunjeno samo za ulogu Reporter, inace null.
    public int? MyOpenReportsCount { get; set; }

    // Popunjeno samo za ulogu Technician, inace null.
    public int? MyActiveAssignmentsCount { get; set; }
}
