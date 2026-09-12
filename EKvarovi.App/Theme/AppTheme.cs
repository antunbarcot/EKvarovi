using MudBlazor;

namespace EKvarovi.App.Theme;

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

        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "4px"
        };

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
