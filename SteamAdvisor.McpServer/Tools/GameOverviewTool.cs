using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace SteamAdvisor.McpServer.Tools;

[McpServerToolType]
public sealed class GameOverviewTool : SteamAppDetailsToolBase
{
    private const string DescriptionSection = "description";
    private const string GenresSection = "genres";
    private const string CreatorsSection = "creators";
    private const string ReleaseSection = "release";
    private const string RatingsSection = "ratings";

    private static readonly string[] AllSections =
        [DescriptionSection, GenresSection, CreatorsSection, ReleaseSection, RatingsSection];

    protected override string OperationName => "informacji";

    [McpServerTool(Name = "get_game_overview")]
    [Description("Pobiera podstawowe informacje o grze ze Steam: krótki opis, gatunki, dewelopera i wydawcę, datę premiery " +
                 "oraz oceny (Metacritic, liczba recenzji). Parametrem 'sections' wybierz tylko te sekcje, o które pyta użytkownik; " +
                 "pominięcie parametru zwraca wszystkie. Jeżeli gra nie istnieje, informuje o tym")]
    public Task<string> GetGameOverviewAsync(
        [Description("Tytuł gry na platformie Steam, np. 'Cyberpunk 2077', 'The Witcher 3: Wild Hunt', 'Elden Ring'. " +
                     "Jeżeli gra nie istnieje, informuje o tym")] string gameName,
        [Description("Opcjonalna lista sekcji do pobrania: " +
                     "'description' (krótki opis gry), " +
                     "'genres' (gatunki), " +
                     "'creators' (deweloper i wydawca), " +
                     "'release' (data premiery i czy gra już wyszła), " +
                     "'ratings' (ocena Metacritic i liczba recenzji na Steam). " +
                     "Pomiń, aby pobrać wszystkie sekcje.")] string[]? sections = null,
        CancellationToken cancellationToken = default)
    {
        var selectedSections = NormalizeSections(sections);
        return ExecuteAsync(
            gameName,
            DefaultCountryCode,
            (game, data) => FormatSections(game, data, selectedSections),
            cancellationToken);
    }

    protected override string FormatResult(SteamGame game, JsonElement data)
        => FormatSections(game, data, AllSections);

    private static IReadOnlyCollection<string> NormalizeSections(string[]? sections)
    {
        var known = (sections ?? [])
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim().ToLowerInvariant())
            .Where(AllSections.Contains)
            .ToHashSet();

        return known.Count > 0 ? known : AllSections;
    }

    private static string FormatSections(SteamGame game, JsonElement data, IReadOnlyCollection<string> sections)
    {
        var result = new StringBuilder(game.Header).Append('\n');
        if (sections.Contains(DescriptionSection))
        {
            var description = CleanHtml(GetString(data, "short_description"));
            result.Append("Opis: ").Append(string.IsNullOrEmpty(description) ? "brak" : description).Append('\n');
        }

        if (sections.Contains(GenresSection))
        {
            var genres = GetStringArray(data, "genres", "description");
            result.Append("Gatunki: ").Append(JoinOrMissing(genres)).Append('\n');
        }

        if (sections.Contains(CreatorsSection))
        {
            result.Append("Deweloper: ").Append(JoinOrMissing(GetStringArray(data, "developers"))).Append('\n');
            result.Append("Wydawca: ").Append(JoinOrMissing(GetStringArray(data, "publishers"))).Append('\n');
        }

        if (sections.Contains(ReleaseSection))
        {
            result.Append("Premiera: ").Append(FormatRelease(data)).Append('\n');
        }

        if (sections.Contains(RatingsSection))
        {
            var metacritic = data.TryGetProperty("metacritic", out var mc) &&
                             mc.TryGetProperty("score", out var score) &&
                             score.TryGetInt32(out var scoreValue)
                ? $"{scoreValue}/100"
                : "brak oceny";

            var reviews = data.TryGetProperty("recommendations", out var recs) &&
                          recs.TryGetProperty("total", out var total) &&
                          total.TryGetInt32(out var totalValue)
                ? totalValue.ToString("N0", CultureInfo.GetCultureInfo("pl-PL"))
                : "brak danych";

            result.Append("Metacritic: ").Append(metacritic).Append('\n');
            result.Append("Liczba recenzji na Steam: ").Append(reviews).Append('\n');
        }

        return result.ToString().TrimEnd();
    }

    private static string FormatRelease(JsonElement data)
    {
        if (!data.TryGetProperty("release_date", out var release) || release.ValueKind != JsonValueKind.Object)
        {
            return "brak danych";
        }

        var date = GetString(release, "date");
        var comingSoon = release.TryGetProperty("coming_soon", out var cs) && cs.ValueKind == JsonValueKind.True;

        if (comingSoon)
        {
            return string.IsNullOrWhiteSpace(date)
                ? "gra jeszcze nie została wydana (data nieznana)"
                : $"gra jeszcze nie została wydana, planowana premiera: {date}";
        }

        return string.IsNullOrWhiteSpace(date) ? "brak danych" : date;
    }

    private static string? GetString(JsonElement obj, string propertyName)
        => obj.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;

    private static List<string> GetStringArray(JsonElement obj, string propertyName, string? itemProperty = null)
    {
        if (!obj.TryGetProperty(propertyName, out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return array.EnumerateArray()
            .Select(item => itemProperty is null
                ? (item.ValueKind == JsonValueKind.String ? item.GetString() : null)
                : GetString(item, itemProperty))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .ToList();
    }

    private static string JoinOrMissing(List<string> values)
        => values.Count > 0 ? string.Join(", ", values) : "brak danych";
}
