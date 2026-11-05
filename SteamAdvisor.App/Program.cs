using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using SteamAdvisor.App.Configuration;

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
        - Sprawdzanie informacji przez narzędzia i api steam jest priorytetem. Sprawdzanie w innych źródłach jest ostatecznością. Jeśli możesz zawsze używaj toolsów.
        - Gdy pobierzesz wymagania gry, odnieś je do parametrów komputera użytkownika i wskaż, czy spełnia wymagania minimalne lub zalecane. Wynik formatuj w czytelny sposób w tabeli.
        - Wynik będzie widoczny w konsoli, więc nie używaj formatowania HTML ani Markdown. Tabele sklejaj ze znaków ascii takich jak | lub -
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
            var serverDll = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "SteamAdvisor.McpServer", "bin", "Debug", "net10.0", "SteamAdvisor.McpServer.dll"));
            var serverProj = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "SteamAdvisor.McpServer"));

            var transportOptions = new StdioClientTransportOptions
            {
                Command = "dotnet",
                Arguments = File.Exists(serverDll)
                    ? new List<string> { serverDll }
                    : new List<string> { "run", "--project", serverProj, "--no-build" }
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
            chatClient = chatClient.AsBuilder()
                .UseFunctionInvocation()
                .Build();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine($"  [MCP] Uwaga: Nie udało się połączyć z serwerem MCP: {ex.Message}");
            Console.ResetColor();
            Console.WriteLine();
        }

        var fullSystemPrompt = SystemPrompt;
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
                        var argsText = tc.Arguments != null
                            ? string.Join(", ", tc.Arguments.Select(kv => $"{kv.Key}=\"{kv.Value}\""))
                            : "";
                        Console.WriteLine($"  used: {tc.Name}({argsText})");
                    }
                    Console.ResetColor();
                }

                Console.WriteLine(response.Text);
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
}