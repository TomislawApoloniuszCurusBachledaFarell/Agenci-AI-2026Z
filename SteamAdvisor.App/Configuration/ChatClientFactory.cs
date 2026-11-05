using System.ClientModel;
using System.ClientModel.Primitives;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;

namespace SteamAdvisor.App.Configuration;

public static class ChatClientFactory
{
    private const string envVarName = "GEMINI_API_KEY_PECHA";
    private const string PoznanApiKeyEnvVar = "POZNAN_API_KEY";

    public static IChatClient Create(IConfiguration configuration)
    {
        var provider = configuration["AI:Provider"]
            ?? throw new InvalidOperationException("Brak konfiguracji AI:Provider w appsettings.json.");

        return provider.ToLowerInvariant() switch
        {
            "ollama" => CreateOllamaClient(configuration),
            "gemini" => CreateGeminiClient(configuration),
            "poznan" => CreatePoznanClient(configuration),
            _ => throw new InvalidOperationException($"Nieznany provider: '{provider}'.")
        };
    }

    private static IChatClient CreateOllamaClient(IConfiguration configuration)
    {
        var endpoint = configuration["AI:Ollama:Endpoint"] ?? "http://localhost:11434/v1";
        var model = configuration["AI:Ollama:Model"] ?? "llama3.2:3b";

        var openAiClient = new OpenAIClient(
            new ApiKeyCredential("ollama"),
            new OpenAIClientOptions { Endpoint = new Uri(endpoint) });

        return openAiClient.GetChatClient(model).AsIChatClient();
    }

    private static IChatClient CreateGeminiClient(IConfiguration configuration)
    {
        var apiKey = Environment.GetEnvironmentVariable(envVarName, EnvironmentVariableTarget.User)
            ?? Environment.GetEnvironmentVariable(envVarName)
            ?? configuration[envVarName]
            ?? throw new InvalidOperationException($"Brak zmiennej środowiskowej {envVarName}.");

        var model = configuration["AI:Gemini:Model"] ?? "gemini-2.5-flash";
        var endpoint = configuration["AI:Gemini:Endpoint"] ?? "https://generativelanguage.googleapis.com/v1beta/openai";

        var clientOptions = new OpenAIClientOptions
        {
            Endpoint = new Uri(endpoint),
            NetworkTimeout = TimeSpan.FromSeconds(120),
            Transport = new HttpClientPipelineTransport(new HttpClient(new GeminiThoughtSignatureHandler()))
        };

        var chatClient = new OpenAI.Chat.ChatClient(model, new ApiKeyCredential(apiKey), clientOptions);
        return chatClient.AsIChatClient();
    }

    private static IChatClient CreatePoznanClient(IConfiguration configuration)
    {
        var apiKey = Environment.GetEnvironmentVariable(PoznanApiKeyEnvVar, EnvironmentVariableTarget.User)
            ?? Environment.GetEnvironmentVariable(PoznanApiKeyEnvVar)
            ?? configuration[PoznanApiKeyEnvVar]
            ?? throw new InvalidOperationException($"Brak zmiennej środowiskowej {PoznanApiKeyEnvVar}.");

        var model = configuration["AI:Poznan:Model"] ?? "gpt-oss_120b";
        var endpoint = configuration["AI:Poznan:Endpoint"] ?? "https://llm.hpc.psnc.pl/v1";

        var clientOptions = new OpenAIClientOptions
        {
            Endpoint = new Uri(endpoint),
            NetworkTimeout = TimeSpan.FromSeconds(120)
        };

        var chatClient = new OpenAI.Chat.ChatClient(model, new ApiKeyCredential(apiKey), clientOptions);
        return chatClient.AsIChatClient();
    }

    public static ChatOptions? CreateChatOptions(IConfiguration configuration)
    {
        var provider = configuration["AI:Provider"];
        if (!string.Equals(provider, "gemini", StringComparison.OrdinalIgnoreCase))
            return null;

        var effortText = configuration["AI:Gemini:ReasoningEffort"] ?? "low";
        if (!Enum.TryParse<ReasoningEffort>(effortText, ignoreCase: true, out var effort))
            effort = ReasoningEffort.Low;

        return new ChatOptions
        {
            Reasoning = new ReasoningOptions { Effort = effort }
        };
    }
}