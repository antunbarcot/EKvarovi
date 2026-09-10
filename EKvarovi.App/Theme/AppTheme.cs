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

        // "Public Sans" (font vladinih/javnih digitalnih usluga, npr. USWDS) umjesto
        // MudBlazor default Roboto - prikladnije za zupanijsku upravnu aplikaciju.
        // Svaka Typography varijanta (H1-H6, Body1, Body2) u MudBlazoru ima SVOJ vlastiti
        // FontFamily koji NE nasljeđuje Default ako nije eksplicitno postavljen - zato se
        // font mora ponoviti na svakoj od njih pojedinacno, ne samo na Default.
        var fontFamily = new[] { "Public Sans", "sans-serif" };
        Typography = new Typography
        {
            Default = new DefaultTypography { FontFamily = fontFamily },
            H1 = new H1Typography { FontFamily = fontFamily },
            H2 = new H2Typography { FontFamily = fontFamily },
            H3 = new H3Typography { FontFamily = fontFamily },
            H4 = new H4Typography { FontFamily = fontFamily },
            H5 = new H5Typography { FontFamily = fontFamily },
            H6 = new H6Typography { FontFamily = fontFamily },
            Body1 = new Body1Typography { FontFamily = fontFamily },
            Body2 = new Body2Typography { FontFamily = fontFamily }
        };
    }
}
