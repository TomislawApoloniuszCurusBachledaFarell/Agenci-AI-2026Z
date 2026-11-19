using System.ComponentModel;
using System.Text;
using Microsoft.Extensions.AI;

namespace SteamAdvisor.App.Skills;

public sealed class TableFormattingSkill : ISkill
{
    public string Name => "table_formatting";

    public string Instructions =>
        """
        ZAWSZE, gdy w odpowiedzi jest jakakolwiek tabela (porównanie gier, ceny, zniżki, oceny, wymagania, języki, specyfikacja, lista w kolumnach), wywołaj to narzędzie. Nie składaj tabeli sam i nie używaj tabeli Markdown. Wklej zwrócony tekst znak w znak, bez zmiany spacji. Nagłówki i komórki w wierszu rozdzielaj znakiem '|', kolejne wiersze znakiem nowej linii. Używaj jako wartości w tabeli wyłącznie podstawowych znaków ASCII
        """;

    public AITool Tool => AIFunctionFactory.Create(FormatTable, Name, Instructions);

    [Description("Jedyne dozwolone źródło tabel. Wywołaj zawsze, gdy odpowiedź ma zawierać jakąkolwiek tabelę. Nie rysuj tabeli samodzielnie.")]
    public static string FormatTable(
        [Description("Nagłówki kolumn oddzielone znakiem '|', np. 'Gra | Cena | Zniżka | Ocena'.")] string headers,
        [Description("Wiersze tabeli. Każdy wiersz w nowej linii, komórki w wierszu oddzielone znakiem '|'.")] string rows)
        => Build(headers, rows, GetWindowWidth());

    internal static string Build(string headers, string rows, int? maxWidth)
    {
        var headerCells = SplitCells(headers);
        if (headerCells.Count == 0)
            return "Brak nagłówków — tabela nie została zbudowana.";

        var dataRows = SplitRows(rows);
        var columnCount = headerCells.Count;
        foreach (var row in dataRows)
            columnCount = Math.Max(columnCount, row.Count);

        Pad(headerCells, columnCount);
        foreach (var row in dataRows)
            Pad(row, columnCount);

        var widths = new int[columnCount];
        for (var column = 0; column < columnCount; column++)
        {
            var width = headerCells[column].Length;
            foreach (var row in dataRows)
                width = Math.Max(width, row[column].Length);

            widths[column] = Math.Max(width, 1);
        }

        FitToWindow(widths, maxWidth);

        var table = new StringBuilder();
        AppendRow(table, headerCells, widths);
        table.AppendLine(FormatSeparator(widths));
        foreach (var row in dataRows)
            AppendRow(table, row, widths);

        return table.ToString().TrimEnd('\r', '\n');
    }

    private static int? GetWindowWidth()
    {
        try
        {
            if (Console.IsOutputRedirected)
                return null;

            var width = Console.WindowWidth;
            return width > 1 ? width - 1 : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static void FitToWindow(int[] widths, int? maxWidth)
    {
        if (widths.Length == 0)
            return;

        var width = widths.Max();
        if (maxWidth is int limit)
        {
            var separator = Math.Max(0, widths.Length - 1) * 3;
            var available = limit - separator;
            if (available < widths.Length || (long)width * widths.Length > available)
                width = Math.Max(1, Math.Max(available, 0) / widths.Length);
        }

        Array.Fill(widths, Math.Max(width, 1));
    }

    private static void AppendRow(StringBuilder table, IReadOnlyList<string> cells, int[] widths)
    {
        var wrapped = new List<string>[widths.Length];
        var height = 1;
        for (var column = 0; column < widths.Length; column++)
        {
            wrapped[column] = Wrap(cells[column], widths[column]);
            height = Math.Max(height, wrapped[column].Count);
        }

        for (var line = 0; line < height; line++)
        {
            var parts = new string[widths.Length];
            for (var column = 0; column < widths.Length; column++)
            {
                var text = line < wrapped[column].Count ? wrapped[column][line] : string.Empty;
                if (text.Length > widths[column])
                    text = text[..widths[column]];

                parts[column] = text.PadRight(widths[column]);
            }

            table.AppendLine(string.Join(" | ", parts));
        }
    }

    private static List<string> Wrap(string text, int width)
    {
        width = Math.Max(width, 1);
        if (string.IsNullOrEmpty(text))
            return [string.Empty];

        var lines = new List<string>();
        var rest = text.Trim();
        while (rest.Length > width)
        {
            var chunk = rest[..width];
            var breakAt = chunk.LastIndexOf(' ');
            if (breakAt <= 0)
            {
                lines.Add(chunk.Trim());
                rest = rest[width..].TrimStart(' ');
                continue;
            }

            lines.Add(chunk[..breakAt].TrimEnd());
            rest = rest[(breakAt + 1)..].TrimStart(' ');
        }

        if (rest.Length > 0 || lines.Count == 0)
            lines.Add(rest);

        return lines;
    }

    private static List<List<string>> SplitRows(string? rows)
    {
        if (string.IsNullOrWhiteSpace(rows))
            return [];

        return rows.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(SplitCells)
            .ToList();
    }

    private static List<string> SplitCells(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return [];

        return line.Split('|').Select(cell => cell.Trim()).ToList();
    }

    private static void Pad(List<string> cells, int columnCount)
    {
        while (cells.Count < columnCount)
            cells.Add(string.Empty);
    }

    private static string FormatSeparator(int[] widths) =>
        string.Join("-+-", widths.Select(width => new string('-', width)));
}
