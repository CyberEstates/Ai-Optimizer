using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AIGamingOptimizer.Models;
using Microsoft.Extensions.Options;

namespace AIGamingOptimizer.Services;

public sealed class AiAssistantService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly AiOptions _options;
    private readonly SystemToolService _tools;
    private readonly List<JsonObject> _history = [];

    public AiAssistantService(HttpClient http, IOptions<AiOptions> options, SystemToolService tools)
    {
        _http = http;
        _options = options.Value;
        _tools = tools;
    }

    public async Task<string> ChatAsync(string userMessage, CancellationToken cancellationToken = default)
    {
        _history.Add(new JsonObject
        {
            ["role"] = "user",
            ["content"] = userMessage
        });

        var snapshot = _tools.SnapshotJson();
        for (var step = 0; step < 6; step++)
        {
            using var response = await SendAsync(snapshot, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var err = $"AI API error {(int)response.StatusCode}: {Trim(body, 800)}";
                _history.RemoveAt(_history.Count - 1);
                throw new InvalidOperationException(err);
            }

            using var doc = JsonDocument.Parse(body);
            var choice = doc.RootElement.GetProperty("choices")[0].GetProperty("message");
            var assistantMsg = JsonNode.Parse(choice.GetRawText())!.AsObject();
            _history.Add(assistantMsg);

            if (!choice.TryGetProperty("tool_calls", out var toolCalls) ||
                toolCalls.ValueKind != JsonValueKind.Array ||
                toolCalls.GetArrayLength() == 0)
            {
                return choice.TryGetProperty("content", out var content)
                    ? content.GetString() ?? "Done."
                    : "Done.";
            }

            foreach (var call in toolCalls.EnumerateArray())
            {
                var id = call.GetProperty("id").GetString();
                var fn = call.GetProperty("function");
                var name = fn.GetProperty("name").GetString() ?? "";
                var argJson = fn.TryGetProperty("arguments", out var argsEl) ? argsEl.GetString() ?? "{}" : "{}";
                JsonElement args;
                try { args = JsonDocument.Parse(argJson).RootElement.Clone(); }
                catch { args = JsonDocument.Parse("{}").RootElement.Clone(); }

                var result = _tools.Execute(name, args);
                _history.Add(new JsonObject
                {
                    ["role"] = "tool",
                    ["tool_call_id"] = id,
                    ["content"] = result
                });
            }
        }

        return "I ran the system tools but hit the step limit. Ask me to continue and I’ll pick up from the latest metrics.";
    }

    public void Reset() => _history.Clear();

    private async Task<HttpResponseMessage> SendAsync(string snapshot, CancellationToken cancellationToken)
    {
        var payload = new JsonObject
        {
            ["model"] = _options.Model,
            ["temperature"] = 0.4,
            ["messages"] = BuildMessages(snapshot),
            ["tools"] = ToolsSpec()
        };

        var url = BuildChatUrl();
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        ApplyAuth(request);
        request.Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");
        return await _http.SendAsync(request, cancellationToken);
    }

    private JsonArray BuildMessages(string snapshot)
    {
        var messages = new JsonArray
        {
            new JsonObject
            {
                ["role"] = "system",
                ["content"] =
                    "You are the AI assistant inside AI Gaming Optimizer, a Windows desktop utility. " +
                    "Be concise, practical, and specific to this PC. Use tools for live facts instead of guessing. " +
                    "Never recommend killing critical Windows processes (csrss, lsass, wininit, services, smss, dwm, explorer). " +
                    "When the user wants more FPS, use game_boost / close_browsers / apply_gaming_power_plan as appropriate. " +
                    "Current live snapshot JSON:\n" + snapshot
            }
        };

        foreach (var item in _history)
        {
            messages.Add(item.DeepClone());
        }

        return messages;
    }

    private string BuildChatUrl()
    {
        var root = _options.BaseUrl.TrimEnd('/');
        if (root.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
        {
            return root;
        }

        return root + "/chat/completions";
    }

    private void ApplyAuth(HttpRequestMessage request)
    {
        var key = Environment.GetEnvironmentVariable("AI_API_KEY");
        if (string.IsNullOrWhiteSpace(key))
        {
            key = _options.ApiKey;
        }

        var style = _options.AuthStyle;
        if (string.Equals(style, "Auto", StringComparison.OrdinalIgnoreCase))
        {
            style = key.StartsWith("AQ.", StringComparison.Ordinal) ? "ApiKey" : "Bearer";
        }

        if (string.Equals(style, "ApiKey", StringComparison.OrdinalIgnoreCase))
        {
            request.Headers.TryAddWithoutValidation("api-key", key);
        }
        else
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        }
    }

    private static JsonArray ToolsSpec() =>
    [
        Tool("get_system_snapshot", "Read live CPU, GPU, RAM, health score, power plan, and process counts."),
        Tool("get_top_processes", "List the heaviest processes.", new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["metric"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = "cpu, ram, or gpu"
                }
            }
        }),
        Tool("game_boost", "Close safe background apps, raise game priority, trim working sets, apply a gaming power plan."),
        Tool("close_browsers", "Close Chrome, Edge, Firefox, Brave, and Opera."),
        Tool("close_processes", "Close processes whose name or window title contains a hint.", new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["hint"] = new JsonObject { ["type"] = "string" }
            },
            ["required"] = new JsonArray("hint")
        }),
        Tool("clean_temp_files", "Delete Windows temp files, user temp, and common browser caches."),
        Tool("apply_gaming_power_plan", "Switch to Ultimate/High Performance and enable Game Mode."),
        Tool("analyze_system", "Run a combined analysis of health, junk files, and startup load.")
    ];

    private static JsonObject Tool(string name, string description, JsonObject? parameters = null) => new()
    {
        ["type"] = "function",
        ["function"] = new JsonObject
        {
            ["name"] = name,
            ["description"] = description,
            ["parameters"] = parameters ?? new JsonObject { ["type"] = "object", ["properties"] = new JsonObject() }
        }
    };

    private static string Trim(string value, int max) =>
        value.Length <= max ? value : value[..max] + "…";
}
