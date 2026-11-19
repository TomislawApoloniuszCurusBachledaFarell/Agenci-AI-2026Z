using System.ComponentModel;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ModelContextProtocol.Server;

namespace SteamAdvisor.McpServer.Tools;

[McpServerToolType]
public sealed class GameRequirementsTool : SteamAppDetailsToolBase
{
    protected override string OperationName => "pobranie wymagań";

    [McpServerTool(Name = "get_game_requirements")]
    [Description("Pobiera minimalne i zalecane wymagania sprzętowe gry z platformy Steam na podstawie jej nazwy " +
                 "(np. 'Cyberpunk 2077', 'The Witcher 3', 'Elden Ring'). Jeżeli gra nie istnieje, informuje o tym")]
    public Task<string> GetGameRequirementsAsync(
        [Description("Tytuł gry na platformie Steam, np. 'Cyberpunk 2077', 'The Witcher 3: Wild Hunt', 'Elden Ring'. " +
                     "Jeżeli gra nie istnieje, informuje o tym")] string gameName,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(gameName, DefaultCountryCode, cancellationToken);

    protected override string FormatResult(SteamGame game, JsonElement data)
    {
        string? minimum = null;
        string? recommended = null;

        if (data.TryGetProperty("pc_requirements", out var pcReqs) && pcReqs.ValueKind == JsonValueKind.Object)
        {
            if (pcReqs.TryGetProperty("minimum", out var minProp) && minProp.ValueKind == JsonValueKind.String)
            {
                minimum = CleanHtml(minProp.GetString());
            }

            if (pcReqs.TryGetProperty("recommended", out var recProp) && recProp.ValueKind == JsonValueKind.String)
            {
                recommended = CleanHtml(recProp.GetString());
            }
        }

        if (string.IsNullOrWhiteSpace(minimum) && string.IsNullOrWhiteSpace(recommended))
        {
            return $"{game.Header}\n\nSteam nie udostępnia wymagań sprzętowych dla tej gry (może to być aplikacja lub gra niewymagająca dedykowanej konfiguracji PC).";
        }

        var result = new StringBuilder($"{game.Header}\n\n");

        if (!string.IsNullOrWhiteSpace(minimum))
        {
            result.Append("Wymagania minimalne:\n").Append(minimum).Append("\n\n");
        }

        if (!string.IsNullOrWhiteSpace(recommended))
        {
            result.Append("Wymagania zalecane:\n").Append(recommended).Append('\n');
        }

        return result.ToString().TrimEnd();
    }

    /// <summary>
    /// Czyści formatowanie HTML zwracane przez Steam w opisach wymagań.
    /// </summary>
    private static string CleanHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        // Zamiana znaczników podziału wiersza i list na czysty tekst
        var text = html
            .Replace("<br>", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("<br/>", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("<br />", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("</li>", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("<li>", "• ", StringComparison.OrdinalIgnoreCase)
            .Replace("<strong>", "", StringComparison.OrdinalIgnoreCase)
            .Replace("</strong>", "", StringComparison.OrdinalIgnoreCase);

        // Usunięcie pozostałych tagów HTML
        text = Regex.Replace(text, @"<[^>]+>", string.Empty);

        // Dekodowanie encji HTML (&quot;, &amp;, itp.)
        text = WebUtility.HtmlDecode(text);

        // Usunięcie nadmiarowych pustych linii
        text = Regex.Replace(text, @"\n{3,}", "\n\n");

        return text.Trim();
    }
}
