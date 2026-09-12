using MudBlazor;

namespace EKvarovi.App.Helpers;

// Jedino mjesto koje mapira naziv statusa prijave u MudBlazor boju - koriste ga FaultReports.razor
// (MudChip u listi), FaultReportProfile.razor (MudChip status prikaz) i vremenska crta (ikona
// "Status promijenjen" eventa), tako da ista boja svugdje u aplikaciji znaci isti status.
public static class FaultStatusColors
{
    public static Color StatusColor(string? statusName) => statusName switch
    {
        "Zaprimljeno" => Color.Default,
        "Pregledano" => Color.Info,
        "Dodijeljeno" => Color.Info,
        "U radu" => Color.Warning,
        "Riješeno" => Color.Success,
        "Zatvoreno" => Color.Secondary,
        _ => Color.Default
    };
}
