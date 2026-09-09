using MudBlazor;

namespace EKvarovi.App.Theme;

// "Sluzbena" teget/plava tema za zupanijsku upravnu aplikaciju - zamjenjuje default
// MudBlazor ljubicastu. Jedna klasa nosi i svijetlu i tamnu paletu; MudThemeProvider
// prebacuje izmedu njih preko IsDarkMode (MainLayout), pa ne treba drzati dva odvojena
// MudTheme objekta niti duplicirati Typography/LayoutProperties na dva mjesta.
public class AppTheme : MudTheme
{
    public AppTheme()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#1E3A5F",
            PrimaryDarken = "#152A45",
            PrimaryLighten = "#2E527F",
            Secondary = "#5C6B73",
            AppbarBackground = "#1E3A5F",
            AppbarText = "#FFFFFF",
            Background = "#F5F6FA",
            BackgroundGray = "#EEF0F4",
            Surface = "#FFFFFF",
            DrawerBackground = "#FFFFFF",
            DrawerText = "#2B2F33",
            DrawerIcon = "#5C6B73",
            TextPrimary = "#1F2933",
            TextSecondary = "#5C6B73",
        };

        PaletteDark = new PaletteDark
        {
            Primary = "#4C7CA8",
            PrimaryDarken = "#3A6386",
            PrimaryLighten = "#6C97BE",
            Secondary = "#8A97A0",
            AppbarBackground = "#152535",
            AppbarText = "#FFFFFF",
            Background = "#15181C",
            Surface = "#1E2227",
            DrawerBackground = "#1A1D21",
            DrawerText = "#DDE1E4",
        };

        // Manje zaobljeni kutovi (default MudBlazor je vidljivo "pill" izgled na
        // gumbima/karticama) - kvadratniji rub djeluje sluzbenije, prikladnije za
        // upravnu aplikaciju.
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "4px"
        };
    }
}
