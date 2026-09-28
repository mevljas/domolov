using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Domolov.Infrastructure.Scanning;

/// <summary>Deterministic illustrated "photos" for fixture listings (demo and smoke tests only).</summary>
public static class DemoPhotos
{
    private static readonly (
        string Sky,
        string Horizon,
        string Wall,
        string Roof,
        string Accent
    )[] Palettes =
    [
        ("#F6D6B8", "#F9EEDC", "#FFFDF9", "#1F4D3A", "#C4633F"),
        ("#BFD7E8", "#EAF2F6", "#F3E7D3", "#8A4B38", "#1F4D3A"),
        ("#E8D5E6", "#F7EEF5", "#EFE9DD", "#3E5A6B", "#D9A441"),
        ("#CFE3C8", "#EEF6EA", "#FFF8EC", "#7A3E2B", "#2E6B55"),
        ("#F3C9A8", "#FBE8D8", "#F1ECE1", "#274235", "#E08A66"),
        ("#D7E0F0", "#F0F3FA", "#FAF6EF", "#5B3A29", "#8FA98F"),
    ];

    public static string Svg(string photoId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(photoId));
        var p = Palettes[hash[0] % Palettes.Length];
        var houseX = 150 + (hash[1] % 180);
        var houseW = 260 + (hash[2] % 120);
        var houseH = 170 + (hash[3] % 90);
        var floors = 1 + (hash[4] % 3);
        var sunX = 80 + (hash[5] % 640);
        var hill = 330 + (hash[6] % 60);
        var baseY = 470;
        var roofY = baseY - houseH;
        var windows = new StringBuilder();
        for (var f = 0; f < floors; f++)
        {
            for (var w = 0; w < 3; w++)
            {
                var wx = houseX + 30 + (w * ((houseW - 60) / 3));
                var wy = roofY + 30 + (f * ((houseH - 60) / floors));
                windows.Append(
                    CultureInfo.InvariantCulture,
                    $"<rect x='{wx}' y='{wy}' width='{(houseW - 120) / 3}' height='{Math.Max(24, (houseH - 90) / floors)}' rx='4' fill='{p.Accent}' opacity='0.85'/>"
                );
            }
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"""
            <svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 800 600' width='800' height='600'>
              <defs>
                <linearGradient id='sky' x1='0' y1='0' x2='0' y2='1'>
                  <stop offset='0' stop-color='{p.Sky}'/><stop offset='1' stop-color='{p.Horizon}'/>
                </linearGradient>
              </defs>
              <rect width='800' height='600' fill='url(#sky)'/>
              <circle cx='{sunX}' cy='110' r='46' fill='#FFF6E0' opacity='0.9'/>
              <path d='M0 {hill} Q 200 {hill - 60} 400 {hill} T 800 {hill
                - 20} V 600 H 0 Z' fill='#8FA98F' opacity='0.55'/>
              <rect x='0' y='{baseY}' width='800' height='130' fill='#5E7F5F'/>
              <rect x='{houseX}' y='{roofY}' width='{houseW}' height='{houseH}' fill='{p.Wall}'/>
              <path d='M{houseX - 24} {roofY + 4} L{houseX + (houseW / 2)} {roofY - 90} L{houseX
                + houseW
                + 24} {roofY + 4} Z' fill='{p.Roof}'/>
              {windows}
              <rect x='{houseX + (houseW / 2) - 26}' y='{baseY
                - 70}' width='52' height='70' rx='6' fill='{p.Roof}'/>
            </svg>
            """
        );
    }
}
