using MudBlazor;
using MudBlazor.Utilities;

namespace Domolov.Theming;

/// <summary>Forest &amp; paper MudBlazor theme for Domolov.</summary>
public static class DomolovTheme
{
    public static readonly MudTheme Default = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = new MudColor("#1F4D3A"),
            Secondary = new MudColor("#5A7264"),
            AppbarBackground = new MudColor("#1F4D3A"),
            AppbarText = new MudColor("#F7F5F0"),
            DrawerBackground = new MudColor("#E4EDE6"),
            DrawerText = new MudColor("#2C3330"),
            DrawerIcon = new MudColor("#1F4D3A"),
            Background = new MudColor("#F7F5F0"),
            Surface = new MudColor("#FFFEFB"),
            TextPrimary = new MudColor("#2C3330"),
            TextSecondary = new MudColor("#5A6360"),
            ActionDefault = new MudColor("#1F4D3A"),
            LinesDefault = new MudColor("#C9D2CB"),
            Divider = new MudColor("#C9D2CB"),
            Success = new MudColor("#0B3D2E"),
            Warning = new MudColor("#8A6A2F"),
            Error = new MudColor("#8B2E2E"),
            Info = new MudColor("#3A5A6A"),
        },
        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = ["Source Sans 3", "Segoe UI", "sans-serif"],
            },
            H1 = new H1Typography
            {
                FontFamily = ["Fraunces", "Georgia", "serif"],
                FontWeight = "600",
                FontSize = "2rem",
            },
            H2 = new H2Typography
            {
                FontFamily = ["Fraunces", "Georgia", "serif"],
                FontWeight = "600",
                FontSize = "1.5rem",
            },
            H3 = new H3Typography
            {
                FontFamily = ["Source Sans 3", "Segoe UI", "sans-serif"],
                FontWeight = "600",
                FontSize = "1.25rem",
            },
            H4 = new H4Typography
            {
                FontFamily = ["Source Sans 3", "Segoe UI", "sans-serif"],
                FontWeight = "600",
            },
            H5 = new H5Typography
            {
                FontFamily = ["Source Sans 3", "Segoe UI", "sans-serif"],
                FontWeight = "600",
            },
            H6 = new H6Typography
            {
                FontFamily = ["Source Sans 3", "Segoe UI", "sans-serif"],
                FontWeight = "600",
            },
            Button = new ButtonTypography
            {
                FontFamily = ["Source Sans 3", "Segoe UI", "sans-serif"],
                FontWeight = "600",
                TextTransform = "none",
            },
        },
        LayoutProperties = new LayoutProperties { DefaultBorderRadius = "8px" },
    };
}
