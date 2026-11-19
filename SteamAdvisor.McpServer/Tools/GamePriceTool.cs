using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace SteamAdvisor.McpServer.Tools;

[McpServerToolType]
public sealed class GamePriceTool : SteamAppDetailsToolBase
{
    protected override string OperationName => "ceny";

    [McpServerTool(Name = "get_game_price_and_status")]
    [Description("Pobiera bazową i aktualną cenę gry ze Steam oraz procent zniżki, jeśli gra jest na promocji. Jeżeli gra nie istnieje, informuje o tym")]
    public Task<string> GetGamePriceAndStatusAsync(
        [Description("Tytuł gry na platformie Steam, np. 'Cyberpunk 2077', 'The Witcher 3: Wild Hunt', 'Elden Ring'. Jeżeli gra nie istnieje, informuje o tym")] string gameName,
        [Description("Dwuliterowy kod kraju (ISO 3166-1 alpha-2), np. 'PL' (Polska - PLN), 'US' (USA - USD), 'DE' (Niemcy - EUR). Domyślnie 'PL'.")] string countryCode = DefaultCountryCode,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(gameName, countryCode, cancellationToken);

    protected override string FormatResult(SteamGame game, JsonElement data)
    {
        if (data.TryGetProperty("is_free", out var isFree) && isFree.ValueKind == JsonValueKind.True)
        {
            return $"{game.Header}\nCena bazowa: darmowa\nCena aktualna: darmowa\nZniżka: 0%";
        }

        if (!data.TryGetProperty("price_overview", out var price) || price.ValueKind != JsonValueKind.Object)
        {
            return $"{game.Header}\nSteam nie podaje ceny dla tej pozycji.";
        }

        var currentPrice = GetNonEmptyString(price, "final_formatted");
        var basePrice = GetNonEmptyString(price, "initial_formatted");
        var discount = price.TryGetProperty("discount_percent", out var discountProp) && discountProp.TryGetInt32(out var percent)
            ? percent
            : 0;

        if (currentPrice is null)
        {
            return $"{game.Header}\nSteam nie podaje ceny dla tej pozycji.";
        }

        if (basePrice is null)
        {
            basePrice = currentPrice;
        }

        return $"{game.Header}\n" +
               $"Cena bazowa: {basePrice}\n" +
               $"Cena aktualna: {currentPrice}\n" +
               $"Zniżka: {discount}%";
    }
    private static string? GetNonEmptyString(JsonElement obj, string propertyName)
    {
        if (obj.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
        {
            var value = prop.GetString();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        return null;
    }
}

