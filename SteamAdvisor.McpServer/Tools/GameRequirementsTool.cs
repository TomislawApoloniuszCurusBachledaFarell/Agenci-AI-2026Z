using System.ComponentModel;
using System.Text;
using System.Text.Json;
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
}
