using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SteamAdvisor.McpServer.Tools;

public sealed record SteamGame(long AppId, string Name)
{
    public string Header => $"Gra: {Name} (Steam AppID: {AppId})";
}
public abstract class SteamAppDetailsToolBase
{
    protected const string DefaultCountryCode = "PL";

    protected static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };
    protected abstract string OperationName { get; }

    protected abstract string FormatResult(SteamGame game, JsonElement data);

    protected Task<string> ExecuteAsync(string gameName, string? countryCode, CancellationToken cancellationToken)
        => ExecuteAsync(gameName, countryCode, FormatResult, cancellationToken);

    protected async Task<string> ExecuteAsync(
        string gameName,
        string? countryCode,
        Func<SteamGame, JsonElement, string> formatResult,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(gameName))
        {
            return "Podaj nazwę gry.";
        }

        var country = string.IsNullOrWhiteSpace(countryCode)
            ? DefaultCountryCode
            : countryCode.Trim().ToUpperInvariant();

        try
        {
            var (game, searchError) = await FindGameAsync(gameName, country, cancellationToken);
            if (game is null)
            {
                return searchError!;
            }

            var detailsUrl = $"https://store.steampowered.com/api/appdetails?appids={game.AppId}&cc={Uri.EscapeDataString(country)}&l=polish";
            using var detailsResponse = await HttpClient.GetAsync(detailsUrl, cancellationToken);

            if (!detailsResponse.IsSuccessStatusCode)
            {
                return $"Błąd Steam API podczas pobierania {OperationName} gry: HTTP {detailsResponse.StatusCode}";
            }

            using var detailsDoc = JsonDocument.Parse(await detailsResponse.Content.ReadAsStringAsync(cancellationToken));

            if (!detailsDoc.RootElement.TryGetProperty(game.AppId.ToString(), out var appData) ||
                !appData.TryGetProperty("success", out var successProp) ||
                successProp.ValueKind != JsonValueKind.True ||
                !appData.TryGetProperty("data", out var data))
            {
                return $"Nie udało się pobrać {OperationName} dla gry '{game.Name}' (AppID: {game.AppId}).";
            }
            return formatResult(game, data);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return $"Błąd podczas pobierania {OperationName} ze Steam: {ex.Message}";
        }
    }

    private static async Task<(SteamGame? Game, string? Error)> FindGameAsync(
        string gameName, string country, CancellationToken cancellationToken)
    {
        var searchUrl = $"https://store.steampowered.com/api/storesearch/?term={Uri.EscapeDataString(gameName)}&l=polish&cc={Uri.EscapeDataString(country)}";
        using var searchResponse = await HttpClient.GetAsync(searchUrl, cancellationToken);

        if (!searchResponse.IsSuccessStatusCode)
        {
            return (null, $"Błąd Steam API podczas wyszukiwania gry: HTTP {searchResponse.StatusCode}");
        }

        using var searchDoc = JsonDocument.Parse(await searchResponse.Content.ReadAsStringAsync(cancellationToken));

        if (!searchDoc.RootElement.TryGetProperty("items", out var items) ||
            items.ValueKind != JsonValueKind.Array ||
            items.GetArrayLength() == 0)
        {
            return (null, $"Nie znaleziono gry '{gameName}' na platformie Steam. Sprawdź pisownię tytułu.");
        }

        var firstItem = items[0];
        var appId = firstItem.GetProperty("id").GetInt64();
        var name = firstItem.GetProperty("name").GetString() ?? gameName;

        return (new SteamGame(appId, name), null);
    }

    /// <summary>
    /// Czyści formatowanie HTML zwracane przez Steam (wymagania, języki itp.) do zwykłego tekstu.
    /// </summary>
    protected static string CleanHtml(string? html)
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
