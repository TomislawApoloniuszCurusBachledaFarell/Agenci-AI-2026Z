using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using SteamAdvisor.McpServer.Services;

namespace SteamAdvisor.McpServer.Resources;

[McpServerResourceType]
public class UserHardwareResource
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    [McpServerResource(
        UriTemplate = "hardware://user/specs",
        Name = "user_hardware_specs",
        Title = "Specyfikacja sprzętowa użytkownika",
        MimeType = "application/json")]
    [Description("Zwraca aktualną specyfikację sprzętową komputera użytkownika (procesor, karta graficzna, RAM, wolne miejsce na dysku, system).")]
    public static string GetSpecs()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "user_hardware.json");

        try
        {
            var specs = HardwareDetector.Detect();
            var json = JsonSerializer.Serialize(specs, JsonOptions);
            File.WriteAllText(path, json);
            var srcPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "user_hardware.json"));
            if (File.Exists(srcPath))
            {
                File.WriteAllText(srcPath, json);
            }

            return json;
        }
        catch
        {
            if (File.Exists(path))
            {
                return File.ReadAllText(path);
            }

            return """{"error": "Nie udało się wykryć ani wczytać specyfikacji sprzętowej"}""";
        }
    }
}