using MudBlazor;

namespace EKvarovi.App.Helpers;

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
