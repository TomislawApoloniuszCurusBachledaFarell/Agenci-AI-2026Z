using System.ComponentModel;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using ModelContextProtocol.Server;

namespace SteamAdvisor.McpServer.Tools;

[McpServerToolType]
public class SteamTools
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    [McpServerTool(Name = "get_game_requirements")]
    [Description("Pobiera minimalne i zalecane wymagania sprzętowe gry z platformy Steam na podstawie jej nazwy (np. 'Cyberpunk 2077', 'The Witcher 3', 'Elden Ring').")]
    public static async Task<string> GetGameRequirementsAsync(
        [Description("Tytuł gry na platformie Steam, np. 'Cyberpunk 2077', 'The Witcher 3: Wild Hunt', 'Elden Ring'.")] string gameName)
    {
        if (string.IsNullOrWhiteSpace(gameName))
        {
            return "Błąd: Podaj nazwę gry.";
        }

        try
        {
            var searchUrl = $"https://store.steampowered.com/api/storesearch/?term={Uri.EscapeDataString(gameName)}&l=polish&cc=PL";
            using var searchResponse = await HttpClient.GetAsync(searchUrl);

            if (!searchResponse.IsSuccessStatusCode)
            {
                return $"Błąd Steam API podczas wyszukiwania gry: HTTP {searchResponse.StatusCode}";
            }

            var searchJson = await searchResponse.Content.ReadAsStringAsync();
            using var searchDoc = JsonDocument.Parse(searchJson);

            if (!searchDoc.RootElement.TryGetProperty("items", out var itemsElement) || 
                itemsElement.GetArrayLength() == 0)
            {
                return $"Nie znaleziono gry '{gameName}' na platformie Steam. Sprawdź pisownię tytułu.";
            }

            var firstItem = itemsElement[0];
            var appId = firstItem.GetProperty("id").GetInt64();
            var matchedGameName = firstItem.GetProperty("name").GetString() ?? gameName;

            var detailsUrl = $"https://store.steampowered.com/api/appdetails?appids={appId}&l=polish";
            using var detailsResponse = await HttpClient.GetAsync(detailsUrl);

            if (!detailsResponse.IsSuccessStatusCode)
            {
                return $"Błąd Steam API podczas pobierania szczegółów gry: HTTP {detailsResponse.StatusCode}";
            }

            var detailsJson = await detailsResponse.Content.ReadAsStringAsync();
            using var detailsDoc = JsonDocument.Parse(detailsJson);

            var appIdKey = appId.ToString();
            if (!detailsDoc.RootElement.TryGetProperty(appIdKey, out var appData) ||
                !appData.TryGetProperty("success", out var successProp) ||
                !successProp.GetBoolean() ||
                !appData.TryGetProperty("data", out var data))
            {
                return $"Nie udało się pobrać szczegółowych danych dla gry '{matchedGameName}' (AppID: {appId}).";
            }

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
                return $"Gra: {matchedGameName} (Steam AppID: {appId})\n\nSteam nie udostępnia wymagań sprzętowych dla tej gry (może to być aplikacja lub gra niewymagająca dedykowanej konfiguracji PC).";
            }

            var result = $"Gra: {matchedGameName} (Steam AppID: {appId})\n\n";

            if (!string.IsNullOrWhiteSpace(minimum))
            {
                result += "=== WYMAGANIA MINIMALNE ===\n" + minimum + "\n\n";
            }

            if (!string.IsNullOrWhiteSpace(recommended))
            {
                result += "=== WYMAGANIA ZALECANE ===\n" + recommended + "\n";
            }

            return result.TrimEnd();
        }
        catch (Exception ex)
        {
            return $"Błąd podczas pobierania wymagań ze Steam: {ex.Message}";
        }
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

