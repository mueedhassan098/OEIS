using MudBlazor;

namespace OEIS.Blazor.Theme;

public static class OeisTheme
{
    public static MudTheme Create()
    {
        return new MudTheme
        {
            PaletteLight = new PaletteLight
            {
                Primary = "#6366f1",
                PrimaryContrastText = "#f8fafc",
                Secondary = "#8b5cf6",
                AppbarBackground = "#4f46e5",
                Background = "#f1f5f9",
                Surface = "#ffffff",
                DrawerBackground = "#0f172a",
                DrawerText = "#e2e8f0",
                Success = "#059669",
                Error = "#dc2626",
                TableLines = "#e2e8f0",
                TableHover = "#f8fafc",
                LinesDefault = "#e2e8f0",
                ActionDefault = "#64748b"
            },
            PaletteDark = new PaletteDark
            {
                Primary = "#818cf8",
                Secondary = "#a78bfa",
                AppbarBackground = "#1e1b4b",
                Background = "#0f172a",
                Surface = "#1e293b",
                DrawerBackground = "#020617",
                DrawerText = "#e2e8f0"
            },
            LayoutProperties = new LayoutProperties
            {
                DefaultBorderRadius = "12px"
            },
            Typography = new Typography
            {
                Default = new DefaultTypography { FontFamily = new[] { "Inter", "Segoe UI", "system-ui", "sans-serif" } },
                H4 = new H4Typography { FontWeight = "600", LetterSpacing = "-0.02em" },
                H5 = new H5Typography { FontWeight = "600" },
                H6 = new H6Typography { FontWeight = "600" }
            }
        };
    }
}
