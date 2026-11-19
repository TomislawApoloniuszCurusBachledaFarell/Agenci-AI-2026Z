using System.ComponentModel;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace SteamAdvisor.McpServer.Tools;

[McpServerToolType]
public sealed class GameLanguagesTool : SteamAppDetailsToolBase
{
    private const string PolishLanguageName = "Polski";

    protected override string OperationName => "języków";

    [McpServerTool(Name = "get_game_languages")]
    [Description("Pobiera listę języków obsługiwanych przez grę na Steam z podziałem na języki z pełnym udźwiękowieniem (dubbing) " +
                 "oraz języki dostępne tylko w interfejsie i napisach. Osobno informuje, czy i w jakim zakresie gra jest dostępna po polsku. " +
                 "Jeżeli gra nie istnieje, informuje o tym")]
    public Task<string> GetGameLanguagesAsync(
        [Description("Tytuł gry na platformie Steam, np. 'Cyberpunk 2077', 'The Witcher 3: Wild Hunt', 'Elden Ring'. " +
                     "Jeżeli gra nie istnieje, informuje o tym")] string gameName,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(gameName, DefaultCountryCode, cancellationToken);

    protected override string FormatResult(SteamGame game, JsonElement data)
    {
        if (!data.TryGetProperty("supported_languages", out var languagesProp) ||
            languagesProp.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(languagesProp.GetString()))
        {
            return $"{game.Header}\nSteam nie podaje listy obsługiwanych języków dla tej gry.";
        }

        var languages = ParseLanguages(languagesProp.GetString()!);
        if (languages.Count == 0)
        {
            return $"{game.Header}\nSteam nie podaje listy obsługiwanych języków dla tej gry.";
        }

        var withAudio = languages.Where(l => l.HasFullAudio).Select(l => l.Name).ToList();
        var textOnly = languages.Where(l => !l.HasFullAudio).Select(l => l.Name).ToList();
        var polish = languages.FirstOrDefault(l => l.Name.Equals(PolishLanguageName, StringComparison.OrdinalIgnoreCase));

        var result = new StringBuilder($"{game.Header}\n");

        if (polish is null)
        {
            result.Append("Język polski: brak — gra nie jest dostępna po polsku.\n");
        }
        else if (polish.HasFullAudio)
        {
            result.Append("Język polski: interfejs, napisy i pełne udźwiękowienie (polski dubbing).\n");
        }
        else
        {
            result.Append("Język polski: interfejs i/lub napisy, bez polskiego dubbingu.\n");
        }

        result.Append($"\nPełne udźwiękowienie ({withAudio.Count}): ")
              .Append(withAudio.Count > 0 ? string.Join(", ", withAudio) : "brak")
              .Append('\n');

        result.Append($"Tylko interfejs i napisy ({textOnly.Count}): ")
              .Append(textOnly.Count > 0 ? string.Join(", ", textOnly) : "brak");

        return result.ToString();
    }

    /// <summary>
    /// Parsuje pole supported_languages ze Steam, np.
    /// </summary>
    private static List<GameLanguage> ParseLanguages(string supportedLanguagesHtml)
    {
        var footnoteIndex = supportedLanguagesHtml.IndexOf("<br", StringComparison.OrdinalIgnoreCase);
        var languagesHtml = footnoteIndex >= 0
            ? supportedLanguagesHtml[..footnoteIndex]
            : supportedLanguagesHtml;

        return CleanHtml(languagesHtml)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(entry => new GameLanguage(
                Name: entry.TrimEnd('*').Trim(),
                HasFullAudio: entry.EndsWith('*')))
            .Where(l => l.Name.Length > 0)
            .ToList();
    }

    private sealed record GameLanguage(string Name, bool HasFullAudio);
}
