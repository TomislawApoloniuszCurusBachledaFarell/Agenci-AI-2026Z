using System.Net;
using System.Text;
using System.Text.Json.Nodes;

namespace SteamAdvisor.App.Configuration;

public class GeminiThoughtSignatureHandler() : DelegatingHandler(new SocketsHttpHandler
{
    AutomaticDecompression = DecompressionMethods.All
})
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Content != null)
        {
            var body = await request.Content.ReadAsStringAsync(cancellationToken);
            if (body.Contains("tool_calls"))
            {
                var node = JsonNode.Parse(body);
                var messages = node?["messages"]?.AsArray();
                if (messages != null)
                {
                    foreach (var tc in messages.SelectMany(m => m?["tool_calls"]?.AsArray() ?? []))
                    {
                        if (tc is JsonObject tcObj)
                        {
                            tcObj["thought_signature"] = "skip_thought_signature_validator";
                            tcObj["extra_content"] = new JsonObject
                            {
                                ["google"] = new JsonObject
                                {
                                    ["thought_signature"] = "skip_thought_signature_validator"
                                }
                            };
                        }
                    }
                    request.Content = new StringContent(node!.ToJsonString(), Encoding.UTF8, "application/json");
                }
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }
}