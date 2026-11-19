using System.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using SteamAdvisor.App.Configuration;
using SteamAdvisor.App.Skills;

namespace SteamAdvisor.App;

internal class Program
{
    private const string SystemPrompt = """
        Jesteś SteamAdvisor — ekspertem od gier dostępnych na platformie Steam.
        Pomagasz użytkownikom znaleźć idealną grę dopasowaną do ich preferencji oraz oceniasz, czy gry zadziałają na ich komputerze.

        Zasady:
        - Odpowiadaj po polsku, chyba że użytkownik pisze w innym języku.
        - Bądź konkretny — podawaj tytuły gier, gatunki i krótkie opisy.
        - Jeżeli posiadasz tool uzyskujący informacje ze Steam, który pasuje do zapytania użytkownika zawsze używaj tego toola do pozyskania tych informacji
        - Posiadasz narzędzie 'get_game_requirements'. Używaj go wtedy i tylko wtedy gdy użytkownik pyta o wymagania sprzętowe konkretnej gry lub pyta, czy dana gra zadziała na jego komputerze.
        - Posiadasz narzędzie 'get_game_price_and_status'. Używaj go wtedy i tylko wtedy gdy użytkownik pyta o cenę lub status gry (np. czy jest na promocji, czy jest dostępna w Steam), używaj go też gdy użytkownik pyta o cenę gry w danym kraju.
        - Posiadasz narzędzie 'get_game_languages'. Używaj go wtedy i tylko wtedy gdy użytkownik pyta o języki dostępne w grze, np. czy gra jest po polsku, czy ma polski dubbing lub napisy.
        - Posiadasz narzędzie 'get_game_overview'. Używaj go gdy użytkownik pyta o opis gry, jej gatunek, dewelopera lub wydawcę, datę premiery albo oceny. W parametrze 'sections' podawaj tylko sekcje, o które pyta użytkownik.
        - Z wyników narzędzi przytaczaj tylko informacje, o które pyta użytkownik.
        - Sprawdzanie informacji przez narzędzia i api steam jest priorytetem. Sprawdzanie w innych źródłach jest ostatecznością. Jeśli możesz zawsze używaj toolsów.
        - Gdy pobierzesz wymagania gry, odnieś je do parametrów komputera użytkownika i wskaż, czy spełnia wymagania minimalne lub zalecane.
        - Bądź zwięzły, ale pomocny.
        """;

    static async Task Main(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .Build();

        IChatClient chatClient;
        try
        {
            chatClient = ChatClientFactory.Create(configuration);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Błąd konfiguracji: {ex.Message}");
            Console.ResetColor();
            return;
        }

        var chatOptions = ChatClientFactory.CreateChatOptions(configuration);
        var model = configuration["AI:Provider"] ?? "?";
        var geminiModel = configuration["AI:Gemini:Model"];
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("══════════════════════════════════════════════");
        Console.WriteLine("  Write 'exit' to quit");
        Console.WriteLine($"  Provider: {model}");
        if (string.Equals(model, "gemini", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"  Model: {geminiModel}");
            Console.WriteLine($"  Reasoning: {chatOptions?.Reasoning?.Effort}");
        }
        else if (string.Equals(model, "poznan", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"  Model: {configuration["AI:Poznan:Model"]}");
        }
        Console.WriteLine("══════════════════════════════════════════════");
        Console.ResetColor();
        Console.WriteLine();

        McpClient? mcpClient = null;
        string? userHardware = null;
        try
        {
            var serverDll = configuration["Mcp:ServerPath"]
                 ?? Path.Combine(AppContext.BaseDirectory, "SteamAdvisor.McpServer.dll");

            if (!File.Exists(serverDll))
                throw new FileNotFoundException($"Nie znaleziono serwera MCP: {serverDll}");

            var transportOptions = new StdioClientTransportOptions
            {
                Command = "dotnet",
                Arguments = [serverDll]
            };

            var transport = new StdioClientTransport(transportOptions);
            mcpClient = await McpClient.CreateAsync(transport);
            var resource = await mcpClient.ReadResourceAsync("hardware://user/specs");
            if (resource.Contents.FirstOrDefault() is TextResourceContents textContent)
            {
                userHardware = textContent.Text;
            }
            var mcpTools = await mcpClient.ListToolsAsync();
            if (mcpTools.Count > 0)
            {
                chatOptions ??= new ChatOptions();
                chatOptions.Tools = mcpTools.Cast<AITool>().ToList();
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine($"  [MCP] Uwaga: Nie udało się połączyć z serwerem MCP: {ex.Message}");
            Console.ResetColor();
            Console.WriteLine();
        }

        var skills = new SkillRegistry()
            .Register(new TableFormattingSkill())
            .Register(new MarkdownCleanupSkill())
            .Register(new GameTitleSkill());
        chatOptions ??= new ChatOptions();
        var tools = chatOptions.Tools?.ToList() ?? [];
        tools.AddRange(skills.Tools);
        chatOptions.Tools = tools;

        chatClient = chatClient.AsBuilder()
            .UseFunctionInvocation()
            .Build();

        var fullSystemPrompt = SystemPrompt;
        var skillPrompt = skills.BuildPromptSection();
        if (!string.IsNullOrWhiteSpace(skillPrompt))
        {
            fullSystemPrompt += $"\n\n{skillPrompt}";
        }
        if (!string.IsNullOrWhiteSpace(userHardware))
        {
            fullSystemPrompt += $"\n\nSpecyfikacja komputera użytkownika:\n{userHardware}\n\nUwzględniaj powyższe parametry sprzętowe przy rekomendowaniu gier oraz ocenie, czy dana gra zadziała na tym sprzęcie.";
        }

        var messages = new List<ChatMessage>
        {
             new(ChatRole.System, fullSystemPrompt)
        };

        while (true)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("> ");
            Console.ResetColor();

            var userInput = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(userInput))
                continue;

            if (userInput.Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("\nDo zobaczenia!");
                Console.ResetColor();
                break;
            }

            messages.Add(new ChatMessage(ChatRole.User, userInput));
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("SteamAdvisor: ");
            Console.ResetColor();

            try
            {
                var response = await chatClient.GetResponseAsync(messages, chatOptions);

                // Wypisz użyte narzędzia
                var toolCalls = response.Messages
                    .SelectMany(m => m.Contents)
                    .OfType<FunctionCallContent>()
                    .ToList();

                if (toolCalls.Count > 0)
                {
                    Console.ForegroundColor = ConsoleColor.DarkCyan;
                    foreach (var tc in toolCalls)
                    {
                        if (skills.IsSkill(tc.Name))
                        {
                            Console.WriteLine($"  used: {tc.Name}");
                            continue;
                        }

                        var argsText = tc.Arguments != null
                            ? string.Join(", ", tc.Arguments.Select(kv => $"{kv.Key}=\"{kv.Value}\""))
                            : "";
                        Console.WriteLine($"  used: {tc.Name}({argsText})");
                    }
                    Console.ResetColor();
                }

                Console.WriteLine(WithExactSkillTables(response, toolCalls));
                messages.AddRange(response.Messages);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Błąd komunikacji z modelem: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner: {ex.InnerException.Message}");
                }
                Console.ResetColor();
                messages.RemoveAt(messages.Count - 1);
            }
            Console.WriteLine();
        }
    }

    private static string WithExactSkillTables(ChatResponse response, IReadOnlyList<FunctionCallContent> toolCalls)
    {
        var resultsByCallId = response.Messages
            .SelectMany(message => message.Contents)
            .OfType<FunctionResultContent>()
            .Where(result => result.CallId is not null)
            .GroupBy(result => result.CallId!)
            .ToDictionary(group => group.Key, group => group.Last().Result);

        var tables = new List<string>();
        foreach (var call in toolCalls)
        {
            if (!string.Equals(call.Name, "table_formatting", StringComparison.Ordinal) || call.CallId is null)
                continue;

            if (resultsByCallId.TryGetValue(call.CallId, out var result) && result is not null)
                tables.Add(result.ToString() ?? string.Empty);
        }

        if (tables.Count == 0)
            return response.Text ?? string.Empty;

        return ReplaceTables(response.Text ?? string.Empty, tables);
    }

    private static string ReplaceTables(string answer, IReadOnlyList<string> tables)
    {
        var lines = answer.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        var output = new StringBuilder();
        var tableIndex = 0;
        var lineIndex = 0;

        while (lineIndex < lines.Length)
        {
            if (tableIndex < tables.Count && IsTableLine(lines[lineIndex]))
            {
                while (lineIndex < lines.Length && IsTableLine(lines[lineIndex]))
                    lineIndex++;

                while (lineIndex < lines.Length && string.IsNullOrWhiteSpace(lines[lineIndex])
                       && lineIndex + 1 < lines.Length && IsTableLine(lines[lineIndex + 1]))
                {
                    lineIndex++;
                }

                output.AppendLine(tables[tableIndex].TrimEnd('\r', '\n'));
                tableIndex++;
                continue;
            }

            output.AppendLine(lines[lineIndex]);
            lineIndex++;
        }

        while (tableIndex < tables.Count)
        {
            output.AppendLine();
            output.AppendLine(tables[tableIndex].TrimEnd('\r', '\n'));
            tableIndex++;
        }

        return output.ToString().TrimEnd('\r', '\n');
    }

    private static bool IsTableLine(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.Length == 0)
            return false;

        if (trimmed.Contains('|'))
            return true;

        return trimmed.Length >= 3 && trimmed.All(character => character is '-' or '+' or ' ');
    }
}